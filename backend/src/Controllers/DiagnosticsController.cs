using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureStorage.Domain.Services;
using SecureStorage.Middleware;

namespace SecureStorage.Controllers;

[AllowAnonymous]
[ApiController]
public class DiagnosticsController(
    HttpMetricsRegistry _metricsRegistry,
    StorageMetricsRegistry _storageMetricsRegistry
) : ControllerBase
{
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        var uptimeSeconds = _metricsRegistry.UptimeSeconds;
        long memoryUsed = GC.GetTotalMemory(forceFullCollection: false);
        ThreadPool.GetAvailableThreads(out int workerThreads, out int iocpThreads);

        var storageMetrics = _storageMetricsRegistry.Metrics;

        var registry = new PrometheusRegistry();

        // Technical metrics
        registry.Gauge("secure_storage_uptime_seconds", "Uptime of the application in seconds.")
            .AddValue(uptimeSeconds);

        registry.Gauge("secure_storage_memory_usage_bytes", "Memory usage of the application.")
            .AddValue(memoryUsed);

        registry.Gauge("secure_storage_threadpool_available_worker_threads", "Available worker threads in thread pool.")
            .AddValue(workerThreads);

        registry.Gauge("secure_storage_threadpool_available_iocp_threads", "Available I/O completion port threads in thread pool.")
            .AddValue(iocpThreads);

        // Business metrics
        registry.Gauge("secure_storage_total_quota_bytes", "Sum of storage quotas for all registered users.")
            .AddValue(storageMetrics.TotalQuotaBytes);

        registry.Gauge("secure_storage_total_used_bytes", "Sum of current storage consumed by all users.")
            .AddValue(storageMetrics.TotalUsedBytes);

        registry.Gauge("secure_storage_config_quota_bytes", "Configured default quota limit per user.")
            .AddValue(storageMetrics.ConfigQuotaBytes);

        registry.Gauge("secure_storage_user_count", "Total number of registered users.")
            .AddValue(storageMetrics.UserCount);

        registry.Gauge("secure_storage_average_used_bytes", "Average storage consumed per user.")
            .AddValue(storageMetrics.AverageUsedBytes);

        registry.Gauge("secure_storage_free_quota_percent", "Free storage quota percentage by percentile.")
            .AddValue(storageMetrics.P50, ("percentile", "50"))
            .AddValue(storageMetrics.P90, ("percentile", "90"))
            .AddValue(storageMetrics.P95, ("percentile", "95"))
            .AddValue(storageMetrics.P99, ("percentile", "99"));

        // HTTP request stats
        var httpMetrics = _metricsRegistry.Metrics.ToList();

        var httpRequestsTotal = registry.Counter("secure_storage_http_requests_total", "Total number of HTTP requests.");
        var httpDurationSum = registry.Counter("secure_storage_http_request_duration_ms_sum", "Cumulative sum of HTTP request durations in milliseconds.");
        var httpDurationCount = registry.Counter("secure_storage_http_request_duration_ms_count", "Cumulative count of HTTP requests for duration calculations.");

        foreach (var kv in httpMetrics)
        {
            foreach (var sc in kv.Value.StatusCodes)
            {
                httpRequestsTotal.AddValue(sc.Value, 
                    ("method", kv.Key.Method), 
                    ("route", kv.Key.Route), 
                    ("status", sc.Key.ToString())
                );
            }

            httpDurationSum.AddValue(kv.Value.TotalDurationMs, 
                ("method", kv.Key.Method), 
                ("route", kv.Key.Route)
            );

            httpDurationCount.AddValue(kv.Value.TotalRequests, 
                ("method", kv.Key.Method), 
                ("route", kv.Key.Route)
            );
        }

        return Content(registry.ToString(), "text/plain; version=0.0.4; charset=utf-8");
    }
}

public class PrometheusRegistry
{
    private readonly List<PrometheusMetric> _metrics = new();

    public PrometheusMetric AddMetric(string name, string type, string help)
    {
        var metric = new PrometheusMetric(name, type, help);
        _metrics.Add(metric);
        return metric;
    }

    public PrometheusMetric Gauge(string name, string help) => AddMetric(name, "gauge", help);
    public PrometheusMetric Counter(string name, string help) => AddMetric(name, "counter", help);

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var metric in _metrics)
        {
            sb.Append(metric.ToString());
        }
        return sb.ToString();
    }
}

public class PrometheusMetric(string name, string type, string help)
{
    public string Name { get; } = name;
    public string Type { get; } = type;
    public string Help { get; } = help;
    private readonly List<string> _values = new();

    public PrometheusMetric AddValue(object value)
    {
        _values.Add($"{Name} {FormatValue(value)}");
        return this;
    }

    public PrometheusMetric AddValue(Dictionary<string, string> labels, object value)
    {
        var labelStr = string.Join(",", labels.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
        _values.Add($"{Name}{{{labelStr}}} {FormatValue(value)}");
        return this;
    }

    public PrometheusMetric AddValue(object value, params (string Key, string Value)[] labels)
    {
        if (labels == null || labels.Length == 0)
        {
            return AddValue(value);
        }
        var labelStr = string.Join(",", labels.Select(l => $"{l.Key}=\"{l.Value}\""));
        _values.Add($"{Name}{{{labelStr}}} {FormatValue(value)}");
        return this;
    }

    private static string FormatValue(object val)
    {
        if (val is double d)
            return d.ToString(CultureInfo.InvariantCulture);
        if (val is float f)
            return f.ToString(CultureInfo.InvariantCulture);
        return val?.ToString() ?? string.Empty;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# HELP {Name} {Help}");
        sb.AppendLine($"# TYPE {Name} {Type}");
        foreach (var val in _values)
        {
            sb.AppendLine(val);
        }
        return sb.ToString();
    }
}
