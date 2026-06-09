using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureStorage.Controllers;
using SecureStorage.Data;
using SecureStorage.Middleware;
using Xunit;

namespace SecureStorage.Tests;

public class DiagnosticsTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void GetMetrics_ShouldReturnEmptyMetrics_AtStartup()
    {
        // Arrange
        var registry = new HttpMetricsRegistry();
        var controller = new DiagnosticsController(registry);

        // Act
        var result = controller.GetMetrics();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        var totalRequestsProp = okResult.Value.GetType().GetProperty("totalRequests");
        Assert.Equal(0L, totalRequestsProp!.GetValue(okResult.Value));
    }

    [Fact]
    public void GetMetrics_ShouldAggregateRouteStatistics()
    {
        // Arrange
        var registry = new HttpMetricsRegistry();
        
        // Record some dummy requests
        registry.RecordRequest("GET", "/api/secrets/{id}", 120, 200);
        registry.RecordRequest("GET", "/api/secrets/{id}", 80, 200);
        registry.RecordRequest("GET", "/api/secrets/{id}", 150, 404);
        registry.RecordRequest("POST", "/api/invites", 35, 201);

        var controller = new DiagnosticsController(registry);

        // Act
        var result = controller.GetMetrics();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = okResult.Value;
        Assert.NotNull(data);

        var totalRequestsProp = data.GetType().GetProperty("totalRequests");
        Assert.Equal(4L, totalRequestsProp!.GetValue(data));

        var routesProp = data.GetType().GetProperty("routes");
        var routes = (System.Collections.IEnumerable)routesProp!.GetValue(data)!;
        var routesList = routes.Cast<object>().ToList();
        
        Assert.Equal(2, routesList.Count);

        var secretsRoute = routesList.FirstOrDefault(r => 
            (string)r.GetType().GetProperty("route")!.GetValue(r)! == "/api/secrets/{id}" &&
            (string)r.GetType().GetProperty("method")!.GetValue(r)! == "GET");
        Assert.NotNull(secretsRoute);

        Assert.Equal(3L, secretsRoute.GetType().GetProperty("totalRequests")!.GetValue(secretsRoute));
        Assert.Equal(80L, secretsRoute.GetType().GetProperty("minDurationMs")!.GetValue(secretsRoute));
        Assert.Equal(150L, secretsRoute.GetType().GetProperty("maxDurationMs")!.GetValue(secretsRoute));
        Assert.Equal(116.67, secretsRoute.GetType().GetProperty("averageDurationMs")!.GetValue(secretsRoute));
    }
}
