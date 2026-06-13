using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SecureStorage.Controllers;
using SecureStorage.Domain.Services;
using SecureStorage.Middleware;
using Xunit;

namespace SecureStorage.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void GetMetrics_ShouldReturnEmptyMetrics_AtStartup()
    {
        // Arrange
        var registry = new HttpMetricsRegistry();
        var storageRegistry = new StorageMetricsRegistry();
        var controller = new DiagnosticsController(registry, storageRegistry);

        // Act
        var result = controller.GetMetrics();

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("text/plain; version=0.0.4; charset=utf-8", contentResult.ContentType);
        Assert.NotNull(contentResult.Content);
        Assert.Contains("secure_storage_uptime_seconds", contentResult.Content);
        Assert.Contains("secure_storage_memory_usage_bytes", contentResult.Content);
    }

    [Fact]
    public void GetMetrics_ShouldAggregateRouteStatistics()
    {
        // Arrange
        var registry = new HttpMetricsRegistry();
        var storageRegistry = new StorageMetricsRegistry();
        
        // Record some dummy requests
        registry.RecordRequest("GET", "/api/secrets/{id}", 120, 200);
        registry.RecordRequest("GET", "/api/secrets/{id}", 80, 200);
        registry.RecordRequest("GET", "/api/secrets/{id}", 150, 404);
        registry.RecordRequest("POST", "/api/invites", 35, 201);

        var controller = new DiagnosticsController(registry, storageRegistry);

        // Act
        var result = controller.GetMetrics();

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("text/plain; version=0.0.4; charset=utf-8", contentResult.ContentType);
        Assert.NotNull(contentResult.Content);
        
        Assert.Contains("secure_storage_http_requests_total{method=\"GET\",route=\"/api/secrets/{id}\",status=\"200\"} 2", contentResult.Content);
        Assert.Contains("secure_storage_http_requests_total{method=\"GET\",route=\"/api/secrets/{id}\",status=\"404\"} 1", contentResult.Content);
        Assert.Contains("secure_storage_http_requests_total{method=\"POST\",route=\"/api/invites\",status=\"201\"} 1", contentResult.Content);
        Assert.Contains("secure_storage_http_request_duration_ms_sum{method=\"GET\",route=\"/api/secrets/{id}\"} 350", contentResult.Content);
        Assert.Contains("secure_storage_http_request_duration_ms_count{method=\"GET\",route=\"/api/secrets/{id}\"} 3", contentResult.Content);
    }
}
