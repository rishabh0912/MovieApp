1. Domain and Boundaries
    - One business capability per service
    - Clear ownership (Identity, Movie, Rating)
    - No overlap in responsibility

2. Data Ownership
    - Database per service
    - No shared schema across service
    - Cross-service consistency via eventual consistency

3. API Contract
    - Define endpoints, request/response models, error contract
    - Add API versioning from start
    - Validate input and proper status code

4. Security
    - AuthN/AuthZ strategy (JWT/OAuth2)
    - Short lived access tokens + refresh token rotation
    - Secrets outside code

5. Reliability 
    - Timeouts, retries, circuit breaker
    - Idempotency for write operations
    - Health checks and readiness probe

6. Observability
    - Structured Logs
    - Correlation Id across services
    - Tracing and basic metrices

7. Deployment and Ops
    - Dockerfile per service
    - Environment based config
    - CI/CD with rollback strategy