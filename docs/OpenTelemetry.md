1. Add Nuget Packages
    dotnet add package OpenTelemetry.Extensions.Hosting
    dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
    dotnet add package OpenTelemetry.Instrumentation.AspNetCore
    dotnet add package OpenTelemetry.Instrumentation.Http
    dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore

2. Add OTEL in program.cs
   builder.Services.AddOpenTelemetry()
        .WithTracing(traceProviderBuilder =>
        {
            traceProviderBuilder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri("http://localhost:4317");
                });
        });

3. Run OTEL Collector via Docker


4. docker-compose for tracing stack