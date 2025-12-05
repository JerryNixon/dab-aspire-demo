using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Web.Library.Diagnostics;

public static class TelemetryExtensions
{
    public static IServiceCollection AddWebTelemetry(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: environment.ApplicationName,
                serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(Telemetry.ActivitySourceName);

                ConfigureOtlpExporter(configuration, tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                ConfigureOtlpExporter(configuration, metrics);
            });

        return services;
    }

    private static void ConfigureOtlpExporter(IConfiguration configuration, TracerProviderBuilder tracing)
    {
        if (TryGetOtlpEndpoint(configuration, out var endpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = endpoint!);
        }
        else
        {
            tracing.AddOtlpExporter();
        }
    }

    private static void ConfigureOtlpExporter(IConfiguration configuration, MeterProviderBuilder metrics)
    {
        if (TryGetOtlpEndpoint(configuration, out var endpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = endpoint!);
        }
        else
        {
            metrics.AddOtlpExporter();
        }
    }

    private static bool TryGetOtlpEndpoint(IConfiguration configuration, out Uri? endpoint)
    {
        var endpointValue = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"];

        if (!string.IsNullOrWhiteSpace(endpointValue) && Uri.TryCreate(endpointValue, UriKind.Absolute, out var uri))
        {
            endpoint = uri;
            return true;
        }

        endpoint = null;
        return false;
    }
}