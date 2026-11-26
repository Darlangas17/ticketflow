using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketFlow.Infrastructure.Auth;

namespace TicketFlow.Api.Security;

/// <summary>Rate limiting, security headers, proxy and HTTPS settings (OWASP ASVS V4, V8, V11, V14).</summary>
public static class SecuritySetup
{
    /// <summary>Policy for the credential endpoints (login, register): strict per-client limit against brute force.</summary>
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddTicketFlowSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection("RateLimiting");
        var authPermits = limits.GetValue("AuthPermitLimit", 10);
        var authWindow = TimeSpan.FromSeconds(limits.GetValue("AuthWindowSeconds", 60));
        var globalPermits = limits.GetValue("GlobalPermitLimit", 300);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Login/register: N attempts per window per client IP. Slows down password guessing
            // and credential stuffing; legitimate users rarely need more than a couple of tries.
            options.AddPolicy(AuthPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermits,
                    Window = authWindow,
                    QueueLimit = 0,
                }));

            // Every request: generous per-user (or per-IP when anonymous) limit against abuse/scraping.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(JwtClaimNames.Subject) is { } userId ? $"user:{userId}" : ClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = globalPermits,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Too many attempts. Please wait a moment and try again.",
                    },
                });
            };
        });

        // Behind a reverse proxy (nginx in Docker) the client IP arrives in X-Forwarded-For. Only
        // proxies inside the configured networks are trusted, so clients cannot spoof their IP.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
            {
                options.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse(network));
            }
        });

        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        return services;
    }

    /// <summary>
    /// Kestrel hardening: no "Server" header (do not advertise the stack) and a 1 MB body limit
    /// (the API only receives small JSON documents).
    /// </summary>
    public static void ConfigureTicketFlowKestrel(this IWebHostBuilder webHost) =>
        webHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = 1024 * 1024;
        });

    public static WebApplication UseTicketFlowSecurity(this WebApplication app)
    {
        app.UseForwardedHeaders();

        // TLS is normally terminated by the reverse proxy. Turn these on where the API itself serves HTTPS.
        if (app.Configuration.GetValue<bool>("Https:Enabled"))
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";

                // API responses are data, never pages: nothing may load or frame them. Swagger UI
                // is an HTML page with scripts, so it keeps its own defaults.
                if (!context.Request.Path.StartsWithSegments("/swagger"))
                {
                    headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                }

                // Responses contain personal data and tickets: never store them in shared caches.
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });
            await next();
        });

        return app;
    }

    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } ip ? $"ip:{Normalize(ip)}" : "ip:unknown";

    // IPv4-mapped IPv6 addresses (::ffff:1.2.3.4) count as the same client as 1.2.3.4.
    private static string Normalize(IPAddress ip) => (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
}
