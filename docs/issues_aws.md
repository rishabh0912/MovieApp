# AWS Deployment - Issues Faced & Resolutions

---

## How We Deployed to AWS (Summary)

**Architecture:**
- 3 .NET 9 microservices (Identity, Movie, Rating) running as ECS Fargate tasks
- 1 RDS PostgreSQL instance shared across 3 databases (IdentityDb, MovieDb, RatingsDb)
- 1 Application Load Balancer routing traffic by path prefix to each service
- Images stored in ECR (Elastic Container Registry)

**Deployment Flow:**
```
Local Code → Docker Build → ECR Push → ECS Task Definition → ECS Service → ALB
```

**Steps followed:**
1. Created 3 ECR repositories (movieapp/identity-service, movieapp/movie-service, movieapp/rating-service)
2. Created RDS PostgreSQL (db.t3.micro, public access enabled for migrations)
3. Connected to RDS via psql, created 3 databases manually
4. Ran EF Core migrations against RDS using environment variable override for connection string
5. Created Security Group (movieapp-ecs-sg) allowing port 8080 from internet, port 5432 from ECS
6. Built Docker images locally, tagged with ECR URIs, pushed to ECR
7. Created ECS Cluster (movieapp-cluster, Fargate)
8. Created 3 Task Definitions (one per service) with ECR image URI, env vars, health check
9. Created 3 ECS Services attached to the cluster
10. Created ALB with path-based routing rules:
    - /auth/* → Identity Service
    - /movies/* → Movie Service
    - /genre/* → Movie Service
    - /rating/* → Rating Service

**Live URL:** `http://movieapp-alb-597644296.ap-south-1.elb.amazonaws.com`

---

## Issues Faced & Resolutions

---

### Issue 1: EF Core Migration Ignoring Custom Connection String

**Problem:**
Running `dotnet ef database update -- --connectionString "Host=..."` silently ignored the flag
and fell back to `appsettings.json` local connection string, showing "Already up to date"
even though RDS had no tables yet.

**Root Cause:**
`dotnet ef database update` does not have a built-in `--connectionString` argument.
The `--` separator passes args to the app, not to EF CLI itself.

**Resolution:**
Use environment variable override instead — ASP.NET Core config system reads env vars
with `__` as section separator, which overrides `appsettings.json`:
```bash
ConnectionStrings__DefaultConnection="Host=<rds-endpoint>;Port=5432;Database=IdentityDb;Username=postgres;Password=postgres" \
dotnet ef database update \
  --project ./src/IdentityService/IdentityService.Infrastructure \
  --startup-project ./src/IdentityService/IdentityService.Api
```

---

### Issue 2: Docker Compose Environment Variable Format Error

**Problem:**
`docker-compose.services.yml` used YAML mapping format for env vars:
```yaml
environment:
  ConnectionStrings__DefaultConnection: "Host=..."
```
This caused containers to fail to start.

**Root Cause:**
When `environment` is a list (not a mapping), values must use `=` not `:`.

**Resolution:**
Changed to list format with `=`:
```yaml
environment:
  - ConnectionStrings__DefaultConnection=Host=...
```

---

### Issue 3: Swashbuckle v10 Breaking Change (OpenApiReference Not Found)

**Problem:**
Build error: `The type or namespace name 'OpenApiReference' does not exist`
after adding Swagger to the services.

**Root Cause:**
`Swashbuckle.AspNetCore` v10 dropped support for `Microsoft.OpenApi` v2.x.
`OpenApiReference` was removed in `Microsoft.OpenApi` v3.x which v10 depends on.

**Resolution:**
Downgraded to `Swashbuckle.AspNetCore` v6.9.0 which works with `Microsoft.OpenApi` v2.x:
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.9.0" />
```
Also removed any explicit `Microsoft.OpenApi` package reference.

---

### Issue 4: ECS Tasks - CannotPullContainerError (Image Not Found)

**Problem:**
All 3 ECS tasks failed with:
```
CannotPullContainerError: failed to resolve ref .../movieapp/movie-service:latest: not found
```
Even though images existed in ECR.

**Root Cause:**
Task definitions were created with image URI missing the `:latest` tag:
```
203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/movie-service   ← wrong
203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/movie-service:latest  ← correct
```
ECR requires an explicit tag — it does not default to `:latest`.

**Resolution:**
Registered new task definition revisions with `:latest` appended to the image URI:
```bash
aws ecs register-task-definition --cli-input-json "<updated json with :latest>"
aws ecs update-service --force-new-deployment --task-definition service:2
```

---

### Issue 5: ECS Tasks - exec format error (Wrong CPU Architecture)

**Problem:**
Tasks pulled the image successfully but crashed immediately with exit code 255.
CloudWatch logs showed:
```
exec /usr/bin/dotnet: exec format error
```

**Root Cause:**
Docker images were built on an Apple Silicon Mac (ARM64 / M-series chip).
ECS Fargate by default uses x86_64 (amd64) runners.
ARM64 binaries cannot run on x86_64.

**Resolution - Option A (chosen): Switch ECS to ARM64 / Graviton**
Instead of rebuilding images for amd64 (which takes hours via QEMU emulation on Mac),
updated all task definitions to use ARM64 architecture — AWS Graviton processors.
This also costs ~20% less than x86 Fargate.
```bash
# In task definition JSON
"runtimePlatform": {
  "cpuArchitecture": "ARM64",
  "operatingSystemFamily": "LINUX"
}
```
Then registered new task definition revision and forced new deployment.

**Resolution - Option B (future, via CI/CD): Build on native amd64**
GitHub Actions `ubuntu-latest` runners are native amd64.
Phase 2 CI/CD pipeline will build and push images from GitHub Actions,
eliminating the architecture mismatch entirely.

---

### Issue 6: AWS CLI Account Mismatch

**Problem:**
`aws sts get-caller-identity` returned account `637423485973` but
all ECR URIs, task definitions, and ECS resources used account `203193248552`.

**Root Cause:**
Two separate AWS accounts — resources were created in one account,
but the local AWS CLI was configured with credentials for a different account.

**Resolution:**
Ran `aws configure` to reconfigure CLI with the correct account's Access Key ID
and Secret Access Key (from IAM → Users → Security credentials → Create access key).

---

### Issue 7: Movie Service Image Missing from ECR at Deploy Time

**Problem:**
Movie service kept failing with `CannotPullContainerError: not found`
even after the architecture fix, while Identity and Rating started successfully.

**Root Cause:**
The movie-service image was pushed to ECR *after* ECS had already attempted
and failed the deployment. ECS does not automatically retry once it marks a
deployment as failed.

**Resolution:**
Forced a new deployment after confirming the image was in ECR:
```bash
aws ecs update-service --cluster movieapp-cluster \
  --service movie-service-service-chlvluax \
  --task-definition movie-service:3 \
  --force-new-deployment
```

---

---

## Resuming Testing (How to Bring Everything Back Up)

**Context:** ALB was deleted to save ~$18/mo. ECS tasks scaled to 0. RDS stopped.
All ECS services, task definitions, cluster, ECR images, and security groups are still intact.

**Estimated time to restore: ~15-20 minutes**

---

### Step 1: Start RDS (~5 min to become available)
```bash
aws rds start-db-instance --region ap-south-1 --db-instance-identifier movieapp-db
```
Wait until status is `available`:
```bash
aws rds describe-db-instances --region ap-south-1 --db-instance-identifier movieapp-db \
  --query "DBInstances[0].DBInstanceStatus" --output text
```

---

### Step 2: Recreate Target Groups

VPC ID: `vpc-0a566d081b13e66a5`
Security Group for ECS: `sg-00f95ac8ad24e4bf6`

```bash
# Identity target group
aws elbv2 create-target-group --region ap-south-1 \
  --name identity-tg --protocol HTTP --port 8080 \
  --vpc-id vpc-0a566d081b13e66a5 --target-type ip \
  --health-check-path /health --health-check-interval-seconds 30

# Movie target group
aws elbv2 create-target-group --region ap-south-1 \
  --name movie-tg --protocol HTTP --port 8080 \
  --vpc-id vpc-0a566d081b13e66a5 --target-type ip \
  --health-check-path /health --health-check-interval-seconds 30

# Rating target group
aws elbv2 create-target-group --region ap-south-1 \
  --name rating-tg --protocol HTTP --port 8080 \
  --vpc-id vpc-0a566d081b13e66a5 --target-type ip \
  --health-check-path /health --health-check-interval-seconds 30
```

Save the 3 ARNs returned — you'll need them in Step 4.

---

### Step 3: Recreate ALB

Subnets: `subnet-0c6f4ac537cf37555`, `subnet-0fc969f575d6be2cf`
Security Group: `sg-079a9c01ee2c9bfa5`

```bash
aws elbv2 create-load-balancer --region ap-south-1 \
  --name movieapp-alb --scheme internet-facing --type application \
  --subnets subnet-0c6f4ac537cf37555 subnet-0fc969f575d6be2cf \
  --security-groups sg-079a9c01ee2c9bfa5
```

Save the new ALB ARN and DNS name from the output.

---

### Step 4: Create Listener with Routing Rules

Replace `<IDENTITY-TG-ARN>`, `<MOVIE-TG-ARN>`, `<RATING-TG-ARN>` with ARNs from Step 2.
Replace `<ALB-ARN>` with ARN from Step 3.

```bash
# Create listener with default rule → identity-tg
aws elbv2 create-listener --region ap-south-1 \
  --load-balancer-arn <ALB-ARN> \
  --protocol HTTP --port 80 \
  --default-actions Type=forward,TargetGroupArn=<IDENTITY-TG-ARN>
```

Save the listener ARN, then add path rules:

```bash
# /auth/* → identity-tg  (priority 10)
aws elbv2 create-rule --region ap-south-1 --listener-arn <LISTENER-ARN> \
  --priority 10 \
  --conditions Field=path-pattern,Values='/auth/*' \
  --actions Type=forward,TargetGroupArn=<IDENTITY-TG-ARN>

# /movies/* → movie-tg  (priority 20)
aws elbv2 create-rule --region ap-south-1 --listener-arn <LISTENER-ARN> \
  --priority 20 \
  --conditions Field=path-pattern,Values='/movies/*' \
  --actions Type=forward,TargetGroupArn=<MOVIE-TG-ARN>

# /genre/* → movie-tg  (priority 30)
aws elbv2 create-rule --region ap-south-1 --listener-arn <LISTENER-ARN> \
  --priority 30 \
  --conditions Field=path-pattern,Values='/genre/*' \
  --actions Type=forward,TargetGroupArn=<MOVIE-TG-ARN>

# /rating/* → rating-tg  (priority 40)
aws elbv2 create-rule --region ap-south-1 --listener-arn <LISTENER-ARN> \
  --priority 40 \
  --conditions Field=path-pattern,Values='/rating/*' \
  --actions Type=forward,TargetGroupArn=<RATING-TG-ARN>
```

---

### Step 5: Attach Target Groups to ECS Services

Replace target group ARNs with new ones from Step 2.

Go to **AWS Console → ECS → movieapp-cluster → each service → Update service**
- Under Load balancing, attach the corresponding target group
- OR update via CLI (re-register task def with new TG in service config)

> Tip: Easiest via console — Edit each service, add load balancer, select the ALB and
> matching target group, port 8080.

---

### Step 6: Scale ECS Services Back Up

```bash
aws ecs update-service --cluster movieapp-cluster --region ap-south-1 \
  --service identity-service-service-b5fc88mx --desired-count 1

aws ecs update-service --cluster movieapp-cluster --region ap-south-1 \
  --service movie-service-service-chlvluax --desired-count 1

aws ecs update-service --cluster movieapp-cluster --region ap-south-1 \
  --service rating-service-service-hy4lu1go --desired-count 1
```

---

### Step 7: Verify

Wait ~2 minutes then test:
```bash
curl http://<new-alb-dns>/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username": "testuser2", "password": "test@1234"}'
```

---

### To Pause Again (Quick Shutdown)
```bash
# Scale ECS to 0
aws ecs update-service --cluster movieapp-cluster --region ap-south-1 --service identity-service-service-b5fc88mx --desired-count 0
aws ecs update-service --cluster movieapp-cluster --region ap-south-1 --service movie-service-service-chlvluax --desired-count 0
aws ecs update-service --cluster movieapp-cluster --region ap-south-1 --service rating-service-service-hy4lu1go --desired-count 0

# Stop RDS
aws rds stop-db-instance --region ap-south-1 --db-instance-identifier movieapp-db

# Delete ALB (to avoid $18/mo)
aws elbv2 delete-load-balancer --region ap-south-1 --load-balancer-arn <ALB-ARN>
```

---

## Key Lessons for Interviews

1. **EF Core migrations against remote DB**: Always use env var override (`ConnectionStrings__DefaultConnection=...`) not `--connectionString` flag.

2. **Docker on Apple Silicon for cloud**: Always pass `--platform linux/amd64` when building for AWS x86, OR configure ECS/Fargate to use ARM64 (Graviton) which supports Apple Silicon images natively and is cheaper.

3. **ECR image tags**: Always use explicit tags (`:latest` or a version). ECR does not fall back to `:latest` automatically unlike Docker Hub.

4. **ECS 503 from ALB**: Almost always means 0 healthy targets. Check: task running count → if 0, check CloudWatch logs for crash reason → common causes: wrong arch, missing env var, DB unreachable.

5. **Microservice JWT validation**: Services can validate JWT tokens without calling the Identity Service — just share the same signing secret and validate locally using `IssuerSigningKey`. No inter-service call needed for auth.

6. **No cross-DB foreign keys**: Each microservice owns its DB. References across services (e.g. MovieId in RatingService) are stored as plain GUIDs with no FK constraint. Data consistency is handled at the application layer.

7. **ALB path-based routing**: One ALB can front multiple microservices using listener rules. Each rule matches a path prefix (`/auth/*`, `/movies/*`) and forwards to a separate target group backed by a different ECS service.
