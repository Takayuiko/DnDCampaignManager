using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace DnDCampingManager.Api.Services;

public static class AuthenticationRateLimits
{
    public static void Configure(RateLimiterOptions options)
    {
        options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        options.AddPolicy("auth-refresh", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    }
}
