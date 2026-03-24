using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Monitoring.Blazor.Models;

namespace Monitoring.Blazor.Services;

public sealed class AlertWorker(
    AlertDispatcher dispatcher,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IDbContextFactory<MonitoringDbContext> dbFactory,
    ILogger<AlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in dispatcher.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SaveAlertAsync(message, stoppingToken);
                await SendDoorayAsync(message, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send alert.");
            }
        }
    }

    private async Task SaveAlertAsync(AlertMessage message, CancellationToken ct)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.AlertEvents.Add(new AlertEventEntity
            {
                Hostname = message.Hostname,
                Ip = message.Ip,
                Os = message.Os,
                Metric = message.Metric,
                Value = message.Value,
                Threshold = message.Threshold,
                Message = message.Message,
                Type = message.Type,
                TimestampUtc = message.TimestampUtc
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist alert event.");
        }
    }

    private async Task SendDoorayAsync(AlertMessage message, CancellationToken ct)
    {
        var enabled = configuration.GetValue("Monitoring:Dooray:Enabled", false);
        var channelId = configuration["Monitoring:Dooray:ChannelId"];
        var apiToken = configuration["Monitoring:Dooray:ApiToken"];

        if (!enabled || string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(apiToken))
        {
            logger.LogInformation("Dooray alert skipped (not configured).");
            return;
        }

        var url = $"https://api.gov-dooray.com/messenger/v1/channels/{channelId}/logs";
        var title = message.Type switch
        {
            AlertType.Recovery => "[Monitoring Recovery]",
            AlertType.Anomaly => "[Monitoring Anomaly]",
            _ => "[Monitoring Alert]"
        };

        var text = $"{title}\n" +
                   $"Host: {message.Hostname}\n" +
                   $"IP: {message.Ip}\n" +
                   $"OS: {message.Os}\n" +
                   $"Metric: {message.Metric}\n" +
                   $"Value: {message.Value:0.0}%\n" +
                   $"Threshold: {message.Threshold:0.0}%\n" +
                   $"Time(UTC): {message.TimestampUtc:yyyy-MM-dd HH:mm:ss}\n" +
                   $"Reason: {message.Message}";

        var payload = JsonSerializer.Serialize(new { text });

        var client = httpClientFactory.CreateClient(nameof(AlertWorker));
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("Authorization", $"dooray-api {apiToken}");
        request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
