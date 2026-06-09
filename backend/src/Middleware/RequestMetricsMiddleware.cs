using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SecureStorage.Middleware;

public class RouteMetricKey(string method, string route)
{
    public string Method { get; } = method;
    public string Route { get; } = route;

    public override bool Equals(object? obj) =>
        obj is RouteMetricKey other && Method == other.Method && Route == other.Route;

    public override int GetHashCode() => HashCode.Combine(Method, Route);
}

public class RouteStats
{
    private long _totalRequests;
    private long _totalDurationMs;
    private long _minDurationMs = long.MaxValue;
    private long _maxDurationMs;
    public ConcurrentDictionary<int, long> StatusCodes { get; } = new();

    public long TotalRequests => Volatile.Read(ref _totalRequests);
    public long TotalDurationMs => Volatile.Read(ref _totalDurationMs);
    public long MinDurationMs => TotalRequests == 0 ? 0 : Volatile.Read(ref _minDurationMs);
    public long MaxDurationMs => Volatile.Read(ref _maxDurationMs);
    public double AverageDurationMs => TotalRequests == 0 ? 0 : (double)TotalDurationMs / TotalRequests;

    public void Record(long durationMs, int statusCode)
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Add(ref _totalDurationMs, durationMs);
        
        long currentMin;
        do
        {
            currentMin = Volatile.Read(ref _minDurationMs);
            if (durationMs >= currentMin) break;
        } while (Interlocked.CompareExchange(ref _minDurationMs, durationMs, currentMin) != currentMin);

        long currentMax;
        do
        {
            currentMax = Volatile.Read(ref _maxDurationMs);
            if (durationMs <= currentMax) break;
        } while (Interlocked.CompareExchange(ref _maxDurationMs, durationMs, currentMax) != currentMax);

        StatusCodes.AddOrUpdate(statusCode, 1, (_, count) => count + 1);
    }
}

public class HttpMetricsRegistry
{
    private readonly DateTime _startTime = DateTime.UtcNow;
    public ConcurrentDictionary<RouteMetricKey, RouteStats> Metrics { get; } = new();

    public DateTime StartTime => _startTime;
    public double UptimeSeconds => (DateTime.UtcNow - _startTime).TotalSeconds;

    public void RecordRequest(string method, string route, long durationMs, int statusCode)
    {
        var key = new RouteMetricKey(method, route);
        var stats = Metrics.GetOrAdd(key, _ => new RouteStats());
        stats.Record(durationMs, statusCode);
    }
}

public class RequestMetricsMiddleware(RequestDelegate next, HttpMetricsRegistry registry)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;
            
            var method = context.Request.Method;
            var endpoint = context.GetEndpoint();
            var route = endpoint is RouteEndpoint routeEndpoint 
                ? routeEndpoint.RoutePattern.RawText 
                : context.Request.Path.Value;

            route ??= "unknown";
            
            // Normalize leading slash for routing consistency
            if (!route.StartsWith('/'))
            {
                route = "/" + route;
            }

            registry.RecordRequest(method, route, durationMs, context.Response.StatusCode);
        }
    }
}
