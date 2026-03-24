using Monitoring.Blazor.Services;

namespace Monitoring.Blazor.Models;

public sealed class HostSnapshotEntity
{
    public long Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public int? Status { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double DiskUsage { get; set; }
    public double SentMbps { get; set; }
    public double RecvMbps { get; set; }
    public long BytesSent { get; set; }
    public long BytesRecv { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class AlertEventEntity
{
    public long Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Threshold { get; set; }
    public string Message { get; set; } = string.Empty;
    public AlertType Type { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public sealed class LogEntryEntity
{
    public long Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Referrer { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string? SourceFile { get; set; }
    public DateTime CreatedUtc { get; set; }
}
