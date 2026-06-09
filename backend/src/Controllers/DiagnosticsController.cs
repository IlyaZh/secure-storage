using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureStorage.Data;
using SecureStorage.Middleware;

namespace SecureStorage.Controllers;

[AllowAnonymous]
[ApiController]
public class DiagnosticsController(
    HttpMetricsRegistry _metricsRegistry
) : ControllerBase
{

    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        var uptimeSeconds = _metricsRegistry.UptimeSeconds;
        var routes = _metricsRegistry.Metrics.Select(kv => new
        {
            method = kv.Key.Method,
            route = kv.Key.Route,
            totalRequests = kv.Value.TotalRequests,
            minDurationMs = kv.Value.MinDurationMs,
            maxDurationMs = kv.Value.MaxDurationMs,
            averageDurationMs = Math.Round(kv.Value.AverageDurationMs, 2),
            statusCodes = kv.Value.StatusCodes.ToDictionary(k => k.Key.ToString(), k => k.Value)
        }).ToList();

        var totalRequests = routes.Sum(r => r.totalRequests);
        var rps = uptimeSeconds > 0 ? Math.Round(totalRequests / uptimeSeconds, 2) : 0;

        // Collect basic system metrics
        long memoryUsed = GC.GetTotalMemory(forceFullCollection: false);
        ThreadPool.GetAvailableThreads(out int workerThreads, out int iocpThreads);

        return Ok(new
        {
            uptimeSeconds = Math.Round(uptimeSeconds, 2),
            totalRequests,
            averageRps = rps,
            memoryUsageBytes = memoryUsed,
            threadPool = new
            {
                availableWorkerThreads = workerThreads,
                availableCompletionPortThreads = iocpThreads
            },
            routes
        });
    }
}
