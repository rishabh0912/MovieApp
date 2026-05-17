# Staff Engineer Bar Raiser — Interview Prep

_Based on MovieApp: .NET 9 microservices (Identity, Movie, Rating), RabbitMQ/MassTransit, PostgreSQL, Next.js, deployed on AWS EC2 + ECS._

---

## 1. System Design — Rate Limiter

### Q: How would you add rate limiting to this system? Where does it live?

**Current state:** No rate limiting exists. Auth endpoints (`POST /auth/login`, `POST /auth/register`) are wide open.

**Answer:**

Rate limiting belongs at the **entry point**, not inside each service — otherwise you repeat logic and burn compute after the damage is done.

Three layers to consider:

```
Client → API Gateway (AWS API GW / Kong / NGINX) → Rate Limiter → Service
```

**Algorithms:**
- **Token Bucket** — allows short bursts, good for APIs (smooth traffic)
- **Fixed Window** — simple, but boundary spikes (all requests at :59 and :00)
- **Sliding Window** — more accurate, uses Redis sorted sets
- **Leaky Bucket** — enforces constant output rate, no bursts

**For this project specifically:**

| Endpoint | Strategy | Limit |
|---|---|---|
| `POST /auth/login` | Sliding window by IP | 5 req/min (brute force protection) |
| `POST /auth/register` | Token bucket by IP | 3 req/min |
| `GET /movies` | Token bucket by userId (JWT sub) | 100 req/min |
| `POST /rating` | Token bucket by userId | 10 req/min |

**Implementation options:**
1. **AWS API Gateway** — built-in throttling, usage plans per API key
2. **Redis + sliding window** — store `{userId}:{endpoint}:{window}` → counter in Redis with TTL
3. **ASP.NET Core middleware** — `Microsoft.AspNetCore.RateLimiting` (good for single service, not cross-service)

**Follow-up: What if the rate limiter becomes a bottleneck?**
Use Redis Cluster with consistent hashing. Rate limit state is per-key so sharding is natural. For extreme scale, approximate counting with Redis HyperLogLog or probabilistic counters.

**Follow-up: How do you handle distributed rate limiting across multiple instances?**
Centralised Redis store. Each instance checks/updates the same key. Use Lua scripts for atomic check-and-increment to avoid race conditions.

Rate limiting can be implemented either at the API Gateway or as a separate service. Gateways are ideal for enforcing coarse-grained limits close to the edge with low latency, while a dedicated rate limiter service is useful for complex, dynamic policies like per-user or per-tenant limits. In large-scale systems, a hybrid approach is often used.

---

## 2. System Design — API Gateway

### Q: This project has 3 services each on different ports. How would you design an API Gateway in front of them?

**Current state:** Browser calls `localhost:5001`, `5002`, `5003` directly. Frontend hardcodes service URLs baked at build time.

**What an API gateway gives you:**
- Single entry point — one domain, one port
- JWT validation at the edge (not duplicated in each service)
- Rate limiting, request logging, tracing correlation IDs
- Path-based routing (already proven in ECS deployment with ALB rules)
- SSL termination

**Design for this project:**

```
Client: api.movieapp.com
    ↓
[API Gateway]
  /auth/*     → Identity Service :5001
  /movies/*   → Movie Service :5002
  /genres/*   → Movie Service :5002
  /rating/*   → Rating Service :5003
    ↓
[JWT validation at gateway]
    ↓
[Forward user claims as headers: X-User-Id, X-User-Role]
    ↓
[Services trust gateway, don't re-validate JWT]
```

Yes, typically a load balancer sits in front of the API Gateway to distribute traffic across multiple gateway instances for high availability. While API Gateways can also perform load balancing across backend services, they primarily handle higher-level concerns like routing, authentication, and rate limiting. In production systems, both are used together with clear separation of responsibilities.

**Options:**
- **AWS API Gateway** — managed, integrates with ALB/ECS, usage plans, WAF
- **Kong** — plugin-based, self-hosted, rich ecosystem
- **NGINX** — lightweight reverse proxy, manual config
- **YARP (Yet Another Reverse Proxy)** — .NET native, can build a gateway service in ASP.NET Core

**Trade-off to discuss:** Gateway as single point of failure → deploy multiple instances behind a load balancer, circuit breakers on downstream calls.

**Follow-up: JWT validation — should services still validate tokens if the gateway does?**
Defence in depth says yes for external-facing services. For internal service-to-service calls (Rating → Movie), use mTLS or a service mesh instead of JWT.

---

## 3. Event Processing — Deep Dive on RatingUpdatedEvent

### Q: Walk me through how the rating update event flows end to end. What can go wrong?

**Current implementation:**
```
RatingService.AddRating()
  → _bus.Publish(RatingUpdatedEvent)   ← fire and forget
  → RabbitMQ exchange
  → MovieService.RatingUpdatedConsumer.Consume()
  → _movieRepository.UpdatedAverageRating()
```

**What can go wrong:**

1. **Publish succeeds, DB write fails (or vice versa)** — the rating is saved but the event is never published, or the event is published but the rating write fails. Average rating in MovieService diverges from actual ratings.
   - Fix: **Outbox Pattern** — write the event to a local `OutboxMessages` table in the same DB transaction as the rating. A background worker polls and publishes. Guarantees at-least-once delivery.

2. **Consumer fails halfway** — movie DB write fails after consuming. MassTransit will redeliver (message not acknowledged). If the handler isn't idempotent, you get double-counted averages.
   - Fix: Make `UpdatedAverageRating` idempotent — store `LastProcessedEventId` on the movie row, skip if already processed.

3. **RabbitMQ is down** — `_bus.Publish` throws, rating write may or may not have committed depending on transaction order.
   - Fix: Outbox pattern again. Or retry with circuit breaker around the publish.

4. **Message ordering** — RatingCreated then RatingUpdated arrive out of order (unlikely with single queue but possible with parallel consumers).
   - Fix: Include sequence number or timestamp on event. Consumer discards stale events.

### Q: What's the difference between at-least-once, at-most-once, and exactly-once delivery?

| | Duplicate risk | Message loss risk | Use case |
|---|---|---|---|
| At-most-once | None | Yes | Metrics, telemetry |
| At-least-once | Yes | None | This project (idempotent handlers needed) |
| Exactly-once | None | None | Financial transactions (expensive, Kafka transactions) |

**MassTransit + RabbitMQ gives at-least-once.** Your `RatingUpdatedConsumer` needs to be idempotent.

---

## 4. Backpressure

### Q: If users submit 10,000 ratings per second, what happens to your system? How do you handle backpressure?

**Current state:** No backpressure mechanism. RatingService publishes to RabbitMQ synchronously. MovieService consumer processes one at a time via MassTransit's default concurrency.

**What happens without backpressure:**
1. RabbitMQ queue grows unboundedly → RAM exhaustion → RabbitMQ crashes
2. MovieService DB gets hammered with `UPDATE` statements → connection pool exhausted → timeouts
3. Rating API response times spike as DB contention grows

**Backpressure strategies:**

**1. Consumer concurrency limits (MassTransit config):**
```csharp
x.UsingRabbitMq((ctx, cfg) => {
    cfg.ReceiveEndpoint("rating-updated", e => {
        e.PrefetchCount = 16;           // only pull 16 messages at a time
        e.ConcurrentMessageLimit = 4;   // process max 4 simultaneously
    });
});
```

**2. Queue-level flow control in RabbitMQ:**
- Set `x-max-length` on the queue → drops or dead-letters oldest messages when full
- Set memory/disk alarms → RabbitMQ stops accepting publishes when threshold crossed (built-in backpressure)

**3. Batch the average rating update:**
Instead of one DB write per event, batch updates every N events or every T milliseconds:
```
Queue → Buffer (Channel<T>) → Batch writer (every 500ms or 100 events)
       → single UPDATE with recalculated average
```

**4. Separate the hot path:**
Rating write → returns immediately (async, event published)
Average rating update → eventually consistent, behind the queue
This is already your design — good. The issue is the consumer keeping up.

**Follow-up: How would you monitor queue depth?**
RabbitMQ Management UI (`/api/queues`), or publish queue depth as a CloudWatch metric and auto-scale ECS consumer tasks when depth > threshold.

---

## 5. Scenario — Token Revocation

### Q: A user's account is compromised. They report it. How do you invalidate their sessions immediately? Your current JWT expiry is 15 minutes.

**Current design:** Short-lived JWT (15 min) + refresh token stored in DB. Revoking the refresh token stops the user from getting new access tokens. But the current access token lives for up to 15 minutes even after revocation.

**Options to handle immediate revocation:**

1. **Refresh token revocation (already have this):** Works for new logins. Doesn't kill in-flight access tokens.

2. **Token blocklist in Redis:**
   - On logout/revoke: write `jti` (JWT ID claim — already in your token) to Redis with TTL = remaining token lifetime
   - Identity Service / API Gateway checks Redis on every request
   - Trade-off: Redis becomes a hot path on every API call

3. **Shorten access token TTL:** 2-5 minutes instead of 15. Reduces the window. Simple change.

4. **Push revocation event:** Identity publishes `UserRevoked` event. Each service maintains a local in-memory blocklist (loaded on startup, updated via event). Fast lookup, no Redis dependency.

**For a staff engineer answer:** Discuss the CAP theorem tradeoff — perfect revocation requires a centralised check (sacrifices availability if Redis is down). Most production systems accept a short window (≤ 5 min TTL) as the acceptable risk rather than paying the latency cost of a Redis check on every request.

---

## 6. Scenario — Service-to-Service Synchronous Call

### Q: RatingService calls MovieService synchronously (HTTP) to get movie details. What happens if MovieService is down?

**Current code:** `MovieServiceClient` uses `HttpClient` with `BaseAddress` from config. No timeout configured, no retry, no circuit breaker.

**What happens:** Rating Service hangs waiting for MovieService. Thread pool threads are held. Under load, Rating Service becomes unresponsive too — **cascading failure**.

**Fix — three layers of resilience:**

```csharp
// With Polly (Microsoft.Extensions.Http.Resilience)
builder.Services.AddHttpClient<MovieServiceClient>()
    .AddStandardResilienceHandler(options => {
        options.Retry.MaxRetryAttempts = 3;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.Timeout.Timeout = TimeSpan.FromSeconds(5);
    });
```

1. **Timeout** — fail fast after 5s, don't hold threads
2. **Retry with exponential backoff** — 3 retries, jitter to avoid thundering herd
3. **Circuit breaker** — after N failures, open circuit for T seconds. Stops hammering a struggling downstream.

Service discovery is a mechanism that allows microservices to dynamically locate each other without hardcoding IP addresses. Services register themselves with a registry, and other services query this registry to find available instances. It enables scalability, fault tolerance, and dynamic environments like containers. There are two patterns: client-side discovery, where the client queries the registry directly, and server-side discovery, where a gateway or load balancer handles it

**Follow-up: Can you eliminate this synchronous call entirely?**
Yes — when `GetMoviesByUserId` is called, movie data could be **cached locally in RatingService** (populated via events). When MovieService publishes `MovieCreated`/`MovieUpdated` events, RatingService maintains a local read model. Eliminates the synchronous dependency entirely. Trade-off: eventual consistency on movie metadata.

---

## 7. Scenario — Database Bottleneck

### Q: All 3 services share one PostgreSQL instance. Movie table grows to 100M rows. Ratings to 1B. What breaks first and how do you fix it?

**What breaks first:** `GetRatingsByMovieId` → full table scan without index. `UpdatedAverageRating` → row-level lock contention on the Movies table under high write load.

**Immediate fixes (no architecture change):**
- Index `ratings(movie_id)` and `ratings(user_id)` — already implied by EF but verify with `EXPLAIN ANALYZE`
- Partial indexes for active records
- Connection pooling via PgBouncer (currently no pooler — each container opens connections directly)

**Medium-term:**
- **Read replica** for `GET /movies`, `GET /ratings` queries
- **Separate databases per service** (already the design intent — IdentityDb, MovieDb, RatingDb — but all on one Postgres instance)
- **Caching layer** (Redis): cache `GET /movies/{id}` responses, invalidate on `MovieUpdated` event

**Long-term:**
- Move ratings to a **time-series or wide-column store** (Cassandra, DynamoDB) — ratings are append-heavy, query by movieId or userId
- Pre-compute average ratings with a **materialized view** or denormalize into Movies table (already doing this with `AverageRating` column)
- **CQRS** — separate write model (Rating table) from read model (pre-aggregated per movie)

---

## 8. System Design — Observability at Scale

### Q: You have OpenTelemetry set up. In production with 100 services, how do you use traces effectively? What's missing in your current setup?

**Current setup:** OTLP traces exported to a local collector. No sampling configured. No metrics. No alerting.

**What's missing:**

1. **Sampling** — tracing every request at scale is expensive. Use **head-based sampling** (decision at trace start, e.g., 1%) or **tail-based sampling** (keep traces that are slow or errored, Jaeger supports this)

2. **Metrics** — OpenTelemetry metrics are separate from traces:
   - Request rate, error rate, latency (p50/p95/p99) per endpoint
   - Queue depth (RabbitMQ → OTLP metrics)
   - DB query duration (already have EF Core instrumentation)

3. **Structured logging correlation** — logs should include `trace_id` and `span_id` so you can go from a log line to the full distributed trace

4. **Alerting** — SLO-based alerts: error rate > 0.1%, p99 latency > 2s → PagerDuty/SNS

5. **Correlation across async boundaries** — when RatingService publishes to RabbitMQ, the trace context needs to propagate in the message headers so MovieService consumer continues the same trace. MassTransit supports W3C TraceContext propagation.

---

## 9. Staff Engineer Mindset Questions

### Q: If you were joining a team that owns this system with 50 engineers depending on it, what are the top 3 things you'd fix first?

**Answer framework (prioritise by blast radius × likelihood):**

1. **Outbox pattern on RatingUpdatedEvent** — current publish is fire-and-forget. Under any failure, movie average ratings silently diverge. Silent data corruption is the worst kind of bug at scale.

2. **API Gateway + remove hardcoded service URLs in frontend** — currently the frontend has service IPs baked at build time. Adding a gateway eliminates this, enables SSL, centralises auth, and lets you change infrastructure without rebuilding the frontend.

3. **PgBouncer connection pooler** — at scale, each .NET service instance opens its own connection pool to Postgres. With 10 instances × 100 pool size = 1000 connections. Postgres struggles above ~500. PgBouncer sits in between and multiplexes.

### Q: How would you handle a major incident where ratings are showing wrong averages in production?

**Incident response:**
1. **Detect** — alert on `AverageRating` drift (compare sum of raw ratings vs stored average via a nightly reconciliation job)
2. **Mitigate** — disable the consumer temporarily, freeze the average
3. **Fix** — recalculate all averages from source: `SELECT movie_id, AVG(score) FROM ratings GROUP BY movie_id` → bulk update Movies table
4. **Prevent** — add the outbox pattern + idempotency check on consumer
5. **Post-mortem** — blameless, 5-whys, add reconciliation job as ongoing health check

### Q: How do you decide between synchronous and asynchronous communication between services?

| Use sync (HTTP) when | Use async (events) when |
|---|---|  
| Response needed immediately for client | Caller doesn't need to wait |
| Simple query (Rating → Movie lookup) | State change that others should react to |
| Consistency required in same request | Eventual consistency acceptable |
| Low latency SLA | Decoupling more important than latency |

**In this project:** Rating → Movie lookup for UI display = sync (user is waiting). Rating created → update movie average = async (user doesn't need to wait for average to update).

---

## 10. SQL vs NoSQL

### Q: You're using PostgreSQL for all 3 services. When would you switch to NoSQL, and which data fits which model?

**Current design:** Every service has its own PostgreSQL DB. This is fine for OLTP workloads with structured, relational data.

**SQL (PostgreSQL) — keep for:**
- Identity data: users, refresh tokens, roles — strong consistency required, ACID guarantee on token rotation
- Movie metadata: movies, genres, cast — relational (movie has many genres, cast members), JOIN-heavy queries
- Ratings themselves: user+movie FK relationship, need `AVG()` aggregations

**NoSQL — when to consider, and which type:**

| Store | Type | Use Case in This Project |
|---|---|---|
| **Redis** | Key-Value | Session cache, rate limit counters, movie detail cache, JWT blocklist |
| **DynamoDB / Cassandra** | Wide-Column | Ratings at scale — append-only, query by movieId or userId, no complex JOINs needed |
| **Elasticsearch** | Document/Search | Full-text movie search (`/movies?q=inception`) — Postgres `LIKE '%inception%'` is a full-table scan |
| **MongoDB** | Document | Flexible movie metadata (different fields per genre: sports movies have teams, docs movies have sources) |
| **Neo4j** | Graph | Recommendation engine: "users who liked X also liked Y" — graph traversal, not SQL-friendly |

### Q: What are the main differences between SQL and NoSQL? When should you NOT use NoSQL?

| | SQL (PostgreSQL) | NoSQL |
|---|---|---|
| **Data model** | Tables, rows, strict schema | Flexible (document, key-value, graph, column) |
| **ACID** | Full ACID, multi-table transactions | Varies — eventual consistency by default |
| **Scaling** | Vertical (scale-up) + read replicas | Horizontal (scale-out) — Cassandra, Mongo |
| **Query power** | JOINs, aggregations, window functions | Limited joins, denormalization required |
| **Schema changes** | Migrations (controlled, versioned) | Schema-free but migrations still needed in practice |
| **Consistency** | Strong by default | Configurable (CAP theorem) |

**Don't use NoSQL when:**
- You need multi-table ACID transactions (e.g., deduct from account A, credit account B)
- Your data is highly relational with complex query patterns
- Your team lacks operational experience with the specific NoSQL store
- You're prematurely optimising — Postgres handles millions of rows per table fine

### Q: Explain CAP theorem in the context of this project.

Distributed system can only guarantee 2 of 3:
- **C**onsistency — every read gets the latest write
- **A**vailability — every request gets a response
- **P**artition tolerance — system works despite network splits

**PostgreSQL = CP** — when a node fails, reads may be stale or unavailable rather than returning wrong data.
**Cassandra = AP** — during partition, returns available (possibly stale) data rather than failing.
**Redis (default) = AP** — prioritises availability, async replication means replicas can lag.

**In this project:** After `RatingUpdatedEvent` is consumed, the movie's average rating in MovieDB is updated asynchronously. Between publish and consume, the data is inconsistent across services. This is the deliberate **AP choice** — availability of the rating write is more important than immediate consistency of the displayed average.

---

## 11. Reliability

### Q: Rate the reliability of this system and what you'd change.

**SPOF inventory (single points of failure):**
- EC2 instance — entire system goes down if it dies → Fix: ECS multi-AZ or multi-instance
- RabbitMQ container — all events stop → Fix: RabbitMQ cluster or Amazon MQ
- PostgreSQL container — all 3 services lose their DB → Fix: RDS Multi-AZ
- Frontend hardcoded IPs — IP changes break the app → Fix: DNS + API gateway

**Reliability patterns to add:**

### Circuit Breaker
RatingService calls MovieService synchronously. No timeout, no circuit breaker.
```csharp
// Current: will hang indefinitely if MovieService is down
var movies = await _movieClient.GetMoviesByIds(movieIds);

// With circuit breaker (Polly):
services.AddHttpClient<MovieServiceClient>()
    .AddStandardResilienceHandler(); // timeout + retry + circuit breaker
```
Circuit breaker states: **Closed** (normal) → **Open** (stop calling, fail fast) → **Half-Open** (test one request).

### Retry with Exponential Backoff + Jitter
```csharp
options.Retry.BackoffType = DelayBackoffType.Exponential;
options.Retry.MaxRetryAttempts = 3;
options.Retry.UseJitter = true; // prevents thundering herd
```

### Health Checks
```csharp
builder.Services.AddHealthChecks()
    .AddNpgsql(connectionString)    // DB reachable
    .AddRabbitMQ(rabbitUri);        // MQ reachable
    
app.MapHealthChecks("/health/live");   // is process alive?
app.MapHealthChecks("/health/ready");  // can it serve traffic?
```
Already partially in place (Docker healthcheck pings `/health`). ALB/ECS uses `/health/ready` to decide if a task receives traffic.

### Graceful Shutdown
Ensure in-flight requests complete before container stops:
```csharp
app.Lifetime.ApplicationStopping.Register(() => {
    // stop accepting new requests, finish current ones
    Thread.Sleep(5000); // drain time
});
```

### Idempotency
`RatingUpdatedConsumer` processes the same event twice → average rating double-counted. Fix with server-side idempotency key:
```csharp
if (await _db.ProcessedEvents.AnyAsync(e => e.EventId == context.Message.EventId))
    return; // already processed
```

### Q: What is the difference between availability, durability, and reliability?
- **Availability** — system responds to requests right now (99.9% = 8.7 hours downtime/year)
- **Durability** — data not lost even if the system fails (S3: 11 nines)
- **Reliability** — system does what it's supposed to do correctly, consistently

You can be available but unreliable (returns wrong data). You can be durable but unavailable (data safe but system down).

---

## 12. Performance

### Q: How do you identify performance problems in this system?

**Step 1 — Measure before optimising (always):**
- OpenTelemetry traces already in place — check span durations in Jaeger/Tempo
- Database: `EXPLAIN ANALYZE` on slow queries via EF Core logging
- Load test with k6 or Apache Bench before making changes

**Known bottlenecks in this codebase:**

### N+1 Query in RatingService
```csharp
// Current: GetRatingsByMovieId loops per movieId — N+1 DB queries
foreach (var movieId in movieIds) {
    var ratings = await _ratingRepo.GetByMovieId(movieId);
}

// Fix: single query
var ratings = await _db.Ratings
    .Where(r => movieIds.Contains(r.MovieId))
    .ToListAsync();
```

### Missing Database Indexes
```sql
-- Every GET /ratings?movieId=X does a full table scan without this:
CREATE INDEX CONCURRENTLY idx_ratings_movie_id ON "Ratings"("MovieId");
-- Every login does a full scan without this:
CREATE INDEX CONCURRENTLY idx_users_email ON "Users"("Email");
```

### No Caching on Movie Reads
Movie data is read-heavy, write-rarely. Every `GET /movies/{id}` hits PostgreSQL.
Fix: Redis cache with 5-minute TTL. Cache hit rate on movie detail pages should be > 90%.

### Connection Pool Exhaustion
Without PgBouncer, each container holds open connections. At 10 ECS tasks × 20 pool size = 200 connections. Postgres degrades around 500.

**Performance benchmarks to target:**
| Endpoint | Target p99 | Current (single instance estimate) |
|---|---|---|
| `GET /movies` | < 100ms | ~200ms (no cache, no index) |
| `POST /auth/login` | < 300ms | ~300ms (bcrypt) |
| `GET /ratings/{movieId}` | < 50ms | ~150ms (N+1 if many) |
| `POST /rating` | < 200ms | ~200ms (DB write + MQ publish) |

### Q: How does bcrypt affect performance and what would you do about it?
bcrypt in `IdentityService` uses a work factor (cost). Each login hashes the password on the CPU. At default cost=12: ~300ms per hash. This is intentional (slows brute force) but becomes a bottleneck under concurrent login load.

**Mitigation:** bcrypt is CPU-bound — scale horizontally (more Identity Service instances). Don't reduce the cost factor below 10. Consider Argon2id for new projects (memory-hard, more resistant to GPU cracking).

---

## 13. Distributed Tracing

### Q: You have OpenTelemetry set up. Explain how a request is traced end to end in this system.

**Current instrumentation in Program.cs:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()   // HTTP request spans
        .AddHttpClientInstrumentation()   // outbound HTTP spans (RatingService → MovieService)
        .AddEntityFrameworkCoreInstrumentation()  // SQL query spans
        .AddOtlpExporter());
```

**Trace flow for `POST /rating`:**
```
[Browser] → POST /rating/{movieId}
  [SPAN: RatingService HTTP handler]
    [SPAN: EF Core INSERT INTO Ratings]
    [SPAN: IBus.Publish RatingUpdatedEvent]
      → RabbitMQ
        [SPAN: RatingUpdatedConsumer.Consume - MovieService]
          [SPAN: EF Core UPDATE Movies SET AverageRating]
```

**Key concepts:**
- **Trace** — the entire journey of one request across all services
- **Span** — a single operation within a trace (one DB call, one HTTP request)
- **TraceId** — propagated in HTTP headers (`traceparent: 00-{traceId}-{spanId}-01`) — W3C format
- **Baggage** — key-value pairs propagated across the trace (e.g., userId)

**What's missing in the current setup:**

1. **Trace context across RabbitMQ messages** — when `IBus.Publish()` is called, the current trace context must be injected into message headers. MassTransit supports this via OpenTelemetry propagation, but must be explicitly enabled:
```csharp
.AddSource("MassTransit") // add MassTransit as a trace source
```
Without this, the MovieService consumer starts a new disconnected trace rather than continuing the same trace.

2. **Sampling** — no sampling configured. 100% of requests traced at scale = expensive. For 10k req/s, this generates 10k traces/s. Use tail-based sampling: keep 100% of error traces, 1% of success traces.

3. **Custom spans for business operations:**
```csharp
using var span = ActivitySource.StartActivity("ProcessRatingUpdate");
span?.SetTag("movie.id", movieId.ToString());
span?.SetTag("rating.score", score.ToString());
```

4. **Metrics vs Traces** — tracing shows WHAT happened, metrics show HOW OFTEN and HOW FAST. Need both. Add:
```csharp
.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddRuntimeInstrumentation()
    .AddOtlpExporter());
```

### Q: A request is slow. How do you use distributed tracing to find the cause?

1. Find the slow request in Jaeger/Grafana Tempo by TraceId or by filtering spans with duration > 1s
2. Open the trace waterfall — identify which span has the longest duration
3. Common culprits: DB query span (missing index), external HTTP span (downstream slow), consumer span (queue backlog)
4. Cross-reference with the span tags: `db.statement` shows the SQL, `http.url` shows which service was called
5. For queue lag: compare `span.start_time` on the consumer vs publish timestamp in the event headers — that gap is queue wait time

---

## 14. DevSecOps

### Q: If you were setting up DevSecOps for this project, what would the pipeline look like?

**Current CI/CD:** GitHub Actions builds Docker images, pushes to Docker Hub, SSH deploys to EC2. No security scanning.

**DevSecOps pipeline — shift security left:**

```
Code Push
  ↓
[1. Pre-commit hooks]
  - Detect secrets (git-secrets, truffleHog) — block API keys committed to repo
  - Lint / format check
  ↓
[2. SAST — Static Application Security Testing]
  - Semgrep or SonarCloud — scan .NET code for injection, insecure config, hardcoded secrets
  - npm audit / OWASP Dependency Check — vulnerable NuGet/npm packages
  ↓
[3. Container Security]
  - Trivy or Snyk — scan Docker images for CVEs before push to Docker Hub/ECR
  - Run containers as non-root user (Dockerfile: USER appuser)
  ↓
[4. IaC Security]
  - Checkov or tfsec on any Terraform/CloudFormation — misconfigured S3 buckets, open security groups
  ↓
[5. Deploy]
  ↓
[6. DAST — Dynamic Application Security Testing]
  - OWASP ZAP against staging environment — test running app for XSS, SQLi, auth bypass
  ↓
[7. Runtime]
  - AWS GuardDuty — threat detection in AWS account
  - CloudTrail — audit log of all AWS API calls
  - Secrets in AWS Secrets Manager / Parameter Store (not env vars in docker-compose)
```

### Specific Issues in This Project to Fix

**1. Secrets in docker-compose / GitHub Secrets — use Secrets Manager:**
```yaml
# Current (bad — secret in env var in compose file)
environment:
  - JWT_SECRET=my-super-secret

# Better — inject from AWS Secrets Manager at container start
# ECS Task Definition: secretsManagerArn reference
```

**2. Docker images running as root:**
```dockerfile
# Current — no USER directive = runs as root
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Add to Dockerfile:
RUN addgroup --system appgroup && adduser --system appuser --ingroup appgroup
USER appuser
```

**3. NuGet/npm dependency scanning in CI:**
```yaml
# Add to GitHub Actions workflow
- name: Scan NuGet dependencies
  run: dotnet list package --vulnerable --include-transitive

- name: Scan npm dependencies
  run: cd frontend/web-app && npm audit --audit-level=high
```

**4. Container image scanning with Trivy:**
```yaml
- name: Scan Docker image
  uses: aquasecurity/trivy-action@master
  with:
    image-ref: rishabh0912/identity-service:latest
    severity: CRITICAL,HIGH
    exit-code: 1  # fail pipeline on critical CVE
```

**5. No HTTPS in current setup:**
- EC2 deployment: services on HTTP ports. Fix: reverse proxy (NGINX + Let's Encrypt) or ALB with ACM cert.
- Hardcoded `http://` in CORS allowed origins and frontend URLs.

### What comes up in DevSecOps interviews

| Topic | What They Ask | Answer |
|---|---|---|
| **OWASP Top 10** | Name them | Broken Access Control, Cryptographic Failures, Injection, Insecure Design, Security Misconfiguration, Vulnerable Components, Auth Failures, Software Integrity Failures, Logging Failures, SSRF |
| **Shift left** | What does it mean? | Find security bugs earlier (dev time) rather than later (production) — cheaper to fix |
| **SAST vs DAST** | Difference? | SAST = code analysis (no running code needed); DAST = test live running app |
| **Secrets management** | How to not expose secrets? | Vault/Secrets Manager, never in repo, never in env vars in prod, rotate regularly |
| **Container hardening** | Minimal images | Use `mcr.microsoft.com/dotnet/aspnet:9.0-alpine`, non-root user, read-only filesystem |
| **SQL Injection** | How to prevent? | Parameterised queries (EF Core does this by default), never string-concat SQL |
| **JWT security** | What can go wrong? | Algorithm confusion (`alg: none`), short secret, no expiry, sensitive data in payload (payload is base64, not encrypted) |
| **Zero trust** | Explain | Never trust by default, verify always — service-to-service also needs auth, not just client-to-gateway |

---

## 15. How to Design a Microservice — Staff Engineer Answer Framework

### Q: Walk me through how you would design a new microservice from scratch.

This question is testing whether you think **end-to-end** — not just "write code". At staff level, the interviewer wants to see you cover boundaries, contracts, data ownership, failure modes, and operability — not just the happy path.

**The framework to follow (say this out loud in the interview):**

---

### Step 1 — Clarify the Problem Before Drawing Anything

Ask questions first. Never jump straight to boxes and arrows.

```
- What is the single responsibility of this service?
  (If you need "and" to describe it, it's two services)
- Who are the consumers? Internal services, external clients, or both?
- What is the expected read/write ratio?
- What are the SLA/SLO requirements? (99.9% uptime? p99 < 200ms?)
- Is strong consistency required, or is eventual consistency acceptable?
- What is the expected data volume and request rate?
  (100 req/day vs 10,000 req/sec changes everything)
- Are there regulatory requirements? (GDPR, PCI, HIPAA)
```

Spending 2 minutes asking these in an interview signals maturity. Junior engineers skip this.

---

### Step 2 — Define the Service Boundary (Domain-Driven Design)

A microservice boundary should align with a **bounded context** — a cohesive domain concept that owns its own data and language.

**Questions to determine the boundary:**
- What data does this service own exclusively? ← It should own exactly one DB
- What does it never need to know about? ← That belongs in another service
- Can it operate independently if all other services are down?

**Red flags that your boundary is wrong:**
- Service needs to JOIN data from another service's DB → shared DB anti-pattern
- Every operation requires a synchronous call to another service → too tight coupling
- Two teams constantly need to coordinate releases → boundary is wrong, Conway's Law at work

**Example (this project):**
```
RatingService owns: Ratings table
  - It knows: userId, movieId, score, timestamp
  - It does NOT know: movie title, genre, cast ← that's MovieService's domain
  - It communicates movie average changes via event, not direct write
```

---

### Step 3 — Design the API Contract

Define the interface before writing any code. This is the **contract** all consumers depend on.

**For REST:**
```
Decide: Resources, HTTP verbs, status codes, pagination, versioning

GET    /ratings/{movieId}         → 200 list, 404 not found
POST   /ratings/{movieId}         → 201 created, 400 validation, 409 conflict
PUT    /ratings/{movieId}/{id}    → 200 updated, 404 not found
DELETE /ratings/{movieId}/{id}    → 204 no content

Versioning: /v1/ratings or Accept: application/vnd.api+json;version=1
```

**For Events (async):**
```
Define the event contract before publishing:
{
  "eventType": "RatingCreated",
  "movieId": "guid",
  "userId": "guid",
  "newScore": 8,
  "oldScore": 0,
  "occurredAt": "2026-01-01T00:00:00Z",
  "eventId": "guid"   ← idempotency key for consumers
}
```

Staff engineer point: **API contracts are public commitments**. Once consumers exist, you can add fields (backward compatible) but never remove them without a versioned migration plan.

---

### Step 4 — Choose the Data Store

Not every service needs PostgreSQL. Choose based on access patterns.

```
What queries will be run most?
  → Key lookups by ID                → PostgreSQL, DynamoDB, Redis
  → Full-text search                 → Elasticsearch
  → Time-series / append-only        → Cassandra, DynamoDB, TimescaleDB
  → Highly relational with JOINs     → PostgreSQL
  → Graph traversal (recommendations)→ Neo4j
  → Read-heavy, rarely changes       → PostgreSQL + Redis cache in front

What consistency level is needed?
  → Financial/transactional          → PostgreSQL (ACID)
  → Eventually consistent OK         → Cassandra, DynamoDB (AP)
```

**In this project:** Ratings are append-heavy (write once, rarely update), query by movieId or userId — a wide-column store like DynamoDB or Cassandra would scale better than PostgreSQL at billions of rows. But PostgreSQL is fine to start.

**Principle:** Start simple (PostgreSQL), migrate when you have real data proving you need something different. Avoid premature optimisation.

---

### Step 5 — Define Communication Patterns

For each interaction, decide: **synchronous or asynchronous?**

```
Rule of thumb:
  Sync  → when the caller needs the result to continue (user is waiting)
  Async → when state has changed and others should react (fire and forget)
```

| Interaction | Pattern | Why |
|---|---|---|
| User requests movie list | Sync REST | User is waiting for the page to load |
| Rating created → update average | Async event | User doesn't wait for average to update |
| Service needs data owned by another service | Async (read own copy via events) | Avoids runtime dependency |
| Payment confirmation | Sync + async | User sees "pending", email sent async |

**Staff engineer point:** If you're calling 3 services synchronously to build one response, that's a red flag. Consider API composition at a Gateway/BFF layer, or denormalising data via events.

---

### Step 6 — Design for Failure

Every external call can and will fail. Design this before writing happy path code.

```
For each synchronous call, define:
  - Timeout: how long before giving up? (5s rule of thumb)
  - Retry: how many times, with what backoff? (3 retries, exponential + jitter)
  - Circuit breaker: after N failures, stop calling for T seconds
  - Fallback: what do you return when the call fails?
    → Cached stale data? Default value? Error 503?

For each event consumer, define:
  - Retry policy: 3 retries with 1s/5s/15s intervals (MassTransit config)
  - Dead letter queue: after all retries, where does the message go?
  - Idempotency: what if the same message arrives twice?
    → Store processed eventIds, skip duplicates
```

**At staff level:** You should mention **Chaos Engineering** — deliberately injecting failures in staging to verify your failure handling actually works (Netflix Chaos Monkey approach).

---

### Step 7 — Security

```
Authentication:   Who is calling?
  → Internal service-to-service: mTLS or JWT with service identity
  → External user: JWT (your IdentityService)
  → External partner: mTLS, OAuth client credentials, or API key

Authorization:    Are they allowed to do this?
  → RBAC (role-based): Admin, User, ReadOnly
  → ABAC (attribute-based): owner of the resource can edit

Input validation: At the boundary, validate everything
  → Never trust HTTP body/query params — validate schema before processing
  → Use FluentValidation or DataAnnotations before hitting the DB

Secrets:
  → Connection strings → AWS Secrets Manager or Vault
  → Never in source code or environment variables committed to repo
```

---

### Step 8 — Observability (Build It In, Not Bolted On)

Before deploying, define:

```
Logs:   Structured JSON (not Console.WriteLine)
        Always include: traceId, userId, service name, environment
        Never log: passwords, tokens, PII

Traces: OpenTelemetry spans for every DB call, HTTP call, queue publish/consume
        Propagate TraceContext headers across service boundaries

Metrics: The four golden signals:
  - Latency (p50/p95/p99 per endpoint)
  - Traffic (requests per second)
  - Errors (error rate %)
  - Saturation (CPU, memory, queue depth)

Health checks:
  GET /health/live  → is the process running?
  GET /health/ready → can it serve traffic? (DB connected, queue connected)

Alerts:
  - Error rate > 1% for 5 minutes → page on-call
  - p99 latency > 2s → warning
  - Queue depth > 10,000 → scale out consumer
```

---

### Step 9 — Data Migration and Versioning Strategy

Often skipped in interviews. Brings you to staff level.

```
Schema migrations:
  → EF Core Migrations — versioned, applied at startup (db.Database.Migrate())
  → Never delete a column before removing all code that uses it (backwards compat)
  → Add columns as nullable first, backfill, then add NOT NULL constraint

API versioning:
  → How will you handle breaking changes?
  → URL versioning (/v1/, /v2/) or header versioning
  → Old version deprecated but kept live for 6 months with sunset header

Event versioning:
  → Events stored in queues/logs are forever — consumers must handle old formats
  → Use upcasters: transform v1 event to v2 shape before consumer processes it
```

---

### Step 10 — Deployment and Operability

```
Containerisation:
  → Dockerfile: multi-stage build (build image → runtime image)
  → Non-root user
  → Health check endpoint

CI/CD:
  → Build → Test → SAST scan → Container scan → Deploy to staging → Smoke test → Prod

Scaling:
  → Stateless service → horizontal scale with load balancer
  → What's the scale trigger? CPU? Request rate? Queue depth?
  → What's the minimum instance count for HA? (always ≥ 2 across AZs)

Runbooks:
  → What does on-call do if this service goes down?
  → How do you roll back a bad deployment?
  → How do you drain traffic without dropping in-flight requests?
```

---

### Putting It All Together — What to Say in the Interview

**Opening (10 seconds):**
> "Before I jump to the design, let me ask a few questions to understand the requirements and constraints..."
> _(Ask 3-4 targeted questions from Step 1)_

**Structure your answer with this narrative:**
> "I'd start by defining the service boundary using domain-driven design principles — what data it owns and what it never needs to know about. Then I'd define the API contract before writing any code — both the REST interface and any events it publishes or consumes. From there, I'd choose the data store based on access patterns, design for failure at every integration point, bake in observability from day one, and think through how it deploys and scales."

**Signal staff-level thinking by mentioning:**
- Conway's Law (team structure should mirror service boundaries)
- Eventual consistency trade-off — acknowledged, not avoided
- The outbox pattern when you have both a DB write and an event publish
- Idempotency on consumers
- API contract as a public commitment — backward compat strategy
- Chaos engineering / failure injection testing
- The four golden signals for observability

**What NOT to do:**
- Don't jump straight to a tech stack ("I'd use .NET and PostgreSQL")
- Don't skip failure scenarios ("...and then the service handles the request and returns 200")
- Don't ignore security until asked
- Don't design a monolith disguised as a microservice (one service that does everything)

