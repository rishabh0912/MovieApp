// instrumentation.ts — loaded by Next.js on server startup (not in the browser)
// Enabled via experimental.instrumentationHook in next.config.ts

export async function register() {
  // Only run in Node.js (server-side). Skip in Edge runtime and browser.
  if (process.env.NEXT_RUNTIME !== 'nodejs') return;

  const { NodeSDK } = await import('@opentelemetry/sdk-node');
  const { OTLPTraceExporter } = await import('@opentelemetry/exporter-trace-otlp-grpc');
  const { getNodeAutoInstrumentations } = await import('@opentelemetry/auto-instrumentations-node');
  const { Resource } = await import('@opentelemetry/resources');

  const sdk = new NodeSDK({
    resource: new Resource({
      'service.name': 'frontend',
    }),
    traceExporter: new OTLPTraceExporter({
      // Reads OTEL_EXPORTER_OTLP_ENDPOINT env var automatically,
      // falls back to localhost for local dev
      url: process.env.OTEL_EXPORTER_OTLP_ENDPOINT ?? 'http://localhost:4317',
    }),
    instrumentations: [
      getNodeAutoInstrumentations({
        // Instrument outbound HTTP calls (fetch to IdentityService, MovieService, RatingService)
        '@opentelemetry/instrumentation-http': { enabled: true },
        // Instrument Next.js internals
        '@opentelemetry/instrumentation-fs': { enabled: false }, // too noisy
      }),
    ],
  });

  sdk.start();
}
