1. Core Design
    - Why microservices instead of monolith?

        Independent deploy/scale and team autonomy when domain complexity justifies it.
    - Why database per service?

        Preserves autonomy and avoids tight coupling.
    - How do you handle consistency without distributed transactions?

        Eventual consistency with events, outbox pattern, and idempotent consumers.
2. API and Communication
    - REST vs messaging?

        REST for synchronous request-response; messaging for decoupled async workflows.
    - How do you version APIs?

        Version in route/header from day one and maintain backward compatibility.
3. Security
    - How do services trust users?

        JWT validation with issuer/audience/signing key checks.
    - How do you revoke tokens?

        Short TTL access tokens and refresh token rotation/revocation list.
4. Reliability and Scale
    - How do you prevent cascading failures?

        Timeouts, retries with backoff, circuit breaker, bulkhead isolation.
    - How do you make writes retry-safe?

        Idempotency keys and unique constraints.
5. Ops and Cloud
    - Why ECS Fargate over EKS initially?

        Faster setup and lower operational overhead for small teams.
    - Where do secrets live in AWS?

        Secrets Manager or SSM Parameter Store, accessed via IAM roles.
    - How do you do zero-downtime deploy?

        Rolling deployments with health checks and traffic shift.