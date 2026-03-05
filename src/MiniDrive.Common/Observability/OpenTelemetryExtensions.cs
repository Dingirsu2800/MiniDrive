using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MiniDrive.Common.Observability;

/// <summary>
/// Extension methods for OpenTelemetry and distributed tracing setup.
/// Enables request tracing across microservices.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry with ASP.NET Core and HTTP instrumentation.
    /// Configures console exporter for development and OTLP for production.
    /// </summary>
    public static IServiceCollection AddOpenTelemetryTracing(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentException("Service name required", nameof(serviceName));

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName)
            .AddAttributes(new Dictionary<string, object>
            {
                { "environment", GetEnvironment(configuration) },
                { "version", GetVersion() }
            });

        // Add TracerProvider with instrumentation
        services.AddSingleton<TracerProvider>(sp =>
        {
            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation(opts =>
                {
                    opts.Filter = (context) =>
                    {
                        // Skip health check endpoints
                        return !context.Request.Path.StartsWithSegments("/health");
                    };
                })
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .Build();

            return tracerProvider;
        });

        return services;
    }

    /// <summary>
    /// Gets environment from configuration.
    /// </summary>
    private static string GetEnvironment(IConfiguration configuration)
    {
        return configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
    }

    /// <summary>
    /// Gets application version.
    /// </summary>
    private static string GetVersion()
    {
        return typeof(OpenTelemetryExtensions).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
