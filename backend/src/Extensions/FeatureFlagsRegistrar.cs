using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace SecureStorage.Extensions;

public class HttpTargetingContextAccessor : ITargetingContextAccessor
{
    public readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTargetingContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask<TargetingContext> GetContextAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return ValueTask.FromResult(new TargetingContext());
        }

        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var groups = httpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        var targetingContext = new TargetingContext
        {
            UserId = userId,
            Groups = groups
        };

        return ValueTask.FromResult(targetingContext);
    }
}

public static class FeatureFlagsRegistrar
{
    public static void ConfigureFeatureFlags(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddFeatureManagement()
                        .AddFeatureFilter<TargetingFilter>();
        builder.Services.AddSingleton<ITargetingContextAccessor, HttpTargetingContextAccessor>();

    }
}

