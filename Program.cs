using Monitoring.Blazor.Components;
using Monitoring.Blazor.Models;
using Monitoring.Blazor.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient(nameof(SystemInfoCollector));
builder.Services.AddSingleton<SystemInfoCollector>();
builder.Services.AddSingleton<AlertEvaluator>();
builder.Services.AddSingleton<AlertDispatcher>();
builder.Services.AddSingleton<AlertSuppressor>();
builder.Services.AddSingleton<AlertSettingsRepository>();
builder.Services.AddSingleton<MonitoringSnapshotQueue>();
builder.Services.AddSingleton<MonitorStateService>();
builder.Services.AddDbContextFactory<MonitoringDbContext>(options =>
{
    var connString = builder.Configuration.GetConnectionString("MonitoringDb");
    if (string.IsNullOrWhiteSpace(connString))
    {
        throw new InvalidOperationException("ConnectionStrings:MonitoringDb is required for MSSQL.");
    }

    options.UseSqlServer(connString);
});
builder.Services.AddHostedService<ServerMonitorWorker>();
builder.Services.AddHostedService<MonitoringSnapshotWorker>();
builder.Services.AddHostedService<AlertWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MonitoringDbContext>>();
    using var db = dbFactory.CreateDbContext();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapGet("/api/monitor/all", (MonitorStateService state) =>
{
    var data = state.GetSnapshot(TimeSpan.FromSeconds(15));
    return Results.Ok(data);
});

app.MapPost("/api/monitor/client-message", async (HttpRequest request, MonitorStateService state) =>
{
    using var reader = new StreamReader(request.Body);
    var json = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(json))
    {
        return Results.BadRequest("Empty payload.");
    }

    return state.TryUpdateClientFromJson(json)
        ? Results.Ok()
        : Results.BadRequest("Invalid payload.");
});

app.MapPost("/api/monitor/trigger-refresh", (MonitorStateService state) =>
{
    var data = state.GetSnapshot(TimeSpan.FromSeconds(15)).Select(x => x.Info).ToList();
    return Results.Ok(new { data });
});
app.MapPost("/trigger-refresh", (MonitorStateService state) =>
{
    var data = state.GetSnapshot(TimeSpan.FromSeconds(15)).Select(x => x.Info).ToList();
    return Results.Ok(new { data });
});

app.MapPost("/api/monitor/parse-logs", async (HttpRequest request, IDbContextFactory<MonitoringDbContext> dbFactory) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Expected multipart form.");
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;
    var rows = new List<ParsedLogRow>();

    await using var db = await dbFactory.CreateDbContextAsync();
    foreach (var file in files)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        var parsed = ApacheLogParser.ParseLines(text.Split(Environment.NewLine));
        rows.AddRange(parsed);

        db.LogEntries.AddRange(parsed.Select(row => new LogEntryEntity
        {
            Date = row.Date,
            Time = row.Time,
            Ip = row.Ip,
            Method = row.Method,
            Uri = row.Uri,
            Status = row.Status,
            Referrer = row.Referrer,
            UserAgent = row.UserAgent,
            SourceFile = file.FileName,
            CreatedUtc = DateTime.UtcNow
        }));
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { logs = rows });
});
app.MapPost("/parse-logs", async (HttpRequest request, IDbContextFactory<MonitoringDbContext> dbFactory) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Expected multipart form.");
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;
    var rows = new List<ParsedLogRow>();

    await using var db = await dbFactory.CreateDbContextAsync();
    foreach (var file in files)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        var parsed = ApacheLogParser.ParseLines(text.Split(Environment.NewLine));
        rows.AddRange(parsed);

        db.LogEntries.AddRange(parsed.Select(row => new LogEntryEntity
        {
            Date = row.Date,
            Time = row.Time,
            Ip = row.Ip,
            Method = row.Method,
            Uri = row.Uri,
            Status = row.Status,
            Referrer = row.Referrer,
            UserAgent = row.UserAgent,
            SourceFile = file.FileName,
            CreatedUtc = DateTime.UtcNow
        }));
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { logs = rows });
});

app.MapGet("/api/monitor/history", async (string hostname, int? minutes, IDbContextFactory<MonitoringDbContext> dbFactory) =>
{
    if (string.IsNullOrWhiteSpace(hostname))
    {
        return Results.BadRequest("hostname required");
    }

    var windowMinutes = minutes is null or <= 0 ? 60 : minutes.Value;
    var since = DateTime.UtcNow.AddMinutes(-windowMinutes);

    await using var db = await dbFactory.CreateDbContextAsync();
    var data = await db.HostSnapshots
        .Where(x => x.Hostname == hostname && x.CreatedUtc >= since)
        .OrderBy(x => x.CreatedUtc)
        .Select(x => new
        {
            x.CreatedUtc,
            x.CpuUsage,
            x.MemoryUsage,
            x.DiskUsage,
            x.SentMbps,
            x.RecvMbps,
            x.Status
        })
        .ToListAsync();

    return Results.Ok(new { hostname, windowMinutes, data });
});

app.MapGet("/api/alerts/history", async (string? hostname, int? take, IDbContextFactory<MonitoringDbContext> dbFactory) =>
{
    var limit = take is null or <= 0 ? 200 : Math.Min(take.Value, 1000);

    await using var db = await dbFactory.CreateDbContextAsync();
    var query = db.AlertEvents.AsQueryable();
    if (!string.IsNullOrWhiteSpace(hostname))
    {
        query = query.Where(x => x.Hostname == hostname);
    }

    var data = await query
        .OrderByDescending(x => x.TimestampUtc)
        .Take(limit)
        .ToListAsync();

    return Results.Ok(new { hostname, take = limit, data });
});

app.MapGet("/api/alerts/suppressions", (AlertSuppressor suppressor) =>
{
    return Results.Ok(suppressor.List());
});

app.MapPost("/api/alerts/mute", async (HttpRequest request, AlertSuppressor suppressor) =>
{
    var payload = await request.ReadFromJsonAsync<SuppressionRequest>();
    if (payload is null || string.IsNullOrWhiteSpace(payload.Hostname) || string.IsNullOrWhiteSpace(payload.Metric))
    {
        return Results.BadRequest("hostname and metric required");
    }

    var minutes = payload.Minutes <= 0 ? 60 : payload.Minutes;
    suppressor.Set(payload.Hostname, payload.Metric, DateTime.UtcNow.AddMinutes(minutes), payload.Reason ?? "mute", "mute");
    return Results.Ok();
});

app.MapPost("/api/alerts/ack", async (HttpRequest request, AlertSuppressor suppressor) =>
{
    var payload = await request.ReadFromJsonAsync<SuppressionRequest>();
    if (payload is null || string.IsNullOrWhiteSpace(payload.Hostname) || string.IsNullOrWhiteSpace(payload.Metric))
    {
        return Results.BadRequest("hostname and metric required");
    }

    var minutes = payload.Minutes <= 0 ? 60 : payload.Minutes;
    suppressor.Set(payload.Hostname, payload.Metric, DateTime.UtcNow.AddMinutes(minutes), payload.Reason ?? "ack", "ack");
    return Results.Ok();
});

app.MapPost("/api/alerts/unmute", async (HttpRequest request, AlertSuppressor suppressor) =>
{
    var payload = await request.ReadFromJsonAsync<SuppressionRequest>();
    if (payload is null || string.IsNullOrWhiteSpace(payload.Hostname) || string.IsNullOrWhiteSpace(payload.Metric))
    {
        return Results.BadRequest("hostname and metric required");
    }

    return suppressor.Clear(payload.Hostname, payload.Metric) ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/alerts/settings", (AlertSettingsRepository repo, IConfiguration config) =>
{
    var baseSettings = config.GetSection("Monitoring:Alerts").Get<AlertSettings>() ?? new AlertSettings();
    return Results.Ok(repo.Load(baseSettings));
});

app.MapPost("/api/alerts/settings", async (HttpRequest request, AlertSettingsRepository repo) =>
{
    var settings = await request.ReadFromJsonAsync<AlertSettings>();
    if (settings is null)
    {
        return Results.BadRequest("Invalid settings");
    }

    repo.Save(settings);
    return Results.Ok();
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

internal sealed record SuppressionRequest(string Hostname, string Metric, int Minutes, string? Reason);
