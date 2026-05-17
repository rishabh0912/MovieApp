# AWS Staff Engineer Interview Guide

_For 15+ years experience — architectural decisions, trade-offs, and production battle stories._

---

## 1. Multi-Region Architecture & Disaster Recovery

### Q: Design a multi-region architecture for a critical financial application. What are the key considerations?

**Answer Framework:**
```
Data Residency:
  - Regulatory requirements (GDPR, data sovereignty)
  - Active-active vs active-passive based on compliance
  - Cross-region data replication strategy

Traffic Routing:
  - Route 53 health checks + latency-based routing
  - Failover policies (primary/secondary region)
  - Global accelerator for static IPs + DDoS protection

Data Consistency:
  - RDS cross-region read replicas (eventual consistency)
  - Aurora Global Database (1-second RPO)
  - DynamoDB Global Tables (multi-master, <1s replication)
  - S3 Cross-Region Replication (CRR)

State Management:
  - ElastiCache Global Datastore (Redis across regions)
  - Avoid storing critical state in local caches
  - Session management: DynamoDB Global or JWT (stateless)

Cost Trade-offs:
  - Data transfer: ~$0.02/GB outbound per region
  - Idle standby resources in passive region
  - Global Accelerator: $0.025/hour + $0.10/GB processed
```

**Key Point:** At Staff level, emphasize **RPO/RTO requirements drive architecture**:
- RPO = 0, RTO < 1 min → Active-active, Aurora Global, ~$100k+/month
- RPO < 5 min, RTO < 15 min → Active-passive with warm standby
- RPO < 1 hour, RTO < 4 hours → Backup/restore from S3 snapshots

**Follow-up:** "How would you test failover without impacting production?"
- Chaos engineering with AWS Fault Injection Simulator
- Blue/green deployments rehearse failover
- Route53 weighted routing to gradually shift 1% traffic to DR region

---

## 2. Cost Optimization at Scale

### Q: Your company's AWS bill jumped from $50k to $200k/month. Walk me through your investigation and optimization strategy.

**Answer Framework:**
```
1. Immediate Triage (Day 1):
   - Cost Explorer: Group by Service, then by Tag
   - CloudWatch billing alarms (should've existed!)
   - Check for obvious spikes: data transfer, EBS snapshots, NAT Gateway

2. Data Collection (Week 1):
   - Enable Cost Allocation Tags (enforce via AWS Organizations SCPs)
   - AWS Cost & Usage Reports → Athena queries
   - Trusted Advisor checks (Reserved Instance recommendations)
   - Identify top 10 cost drivers (usually 80% of spend)

3. Common Culprits & Fixes:
   
   Data Transfer ($):
     - NAT Gateway: $0.045/GB processed (~$30k/month for 20TB)
     - Fix: VPC Endpoints for S3/DynamoDB (free private access)
     - Inter-AZ: $0.01/GB (can be huge in multi-AZ RDS)
   
   Oversized Instances ($$$):
     - EC2 running 24/7 at 5% CPU
     - Fix: Rightsizing (t3.2xlarge → t3.large = 50% savings)
     - CloudWatch Agent + Compute Optimizer recommendations
   
   EBS Volumes $$:
     - Unused volumes from terminated instances ($0.10/GB-month)
     - Over-provisioned IOPS: gp3 vs io2 (10x cost difference)
     - Fix: Lambda to auto-delete unattached volumes >7 days
   
   RDS ($$$):
     - Multi-AZ production instances running 24/7 for dev/test
     - Fix: Stop/start non-prod RDS on schedule (70% savings)
     - Aurora Serverless v2 for variable workloads
   
   S3 Lifecycle ($$):
     - Objects in Standard when they should be in Glacier
     - Fix: Lifecycle policies (30d → IA, 90d → Glacier, 365d → Deep Archive)
     - S3 Intelligent-Tiering for uncertain access patterns

4. Long-term Strategy:
   - Reserved Instances: 1-year no-upfront for predictable workloads (40% savings)
   - Savings Plans: Compute SP for flexibility across EC2/Fargate/Lambda
   - Spot Instances: For fault-tolerant batch jobs (70-90% savings)
   - FinOps culture: Engineers see costs in dashboards, monthly reviews
```

**Staff Engineer Perspective:**
"I wouldn't just optimize — I'd establish a **cost governance framework**:
- Tag enforcement via AWS Config rules (no tags = auto-stop)
- Budget alerts per team/project via Cost Allocation Tags
- Quarterly FinOps reviews comparing cost per transaction/user
- Architecture review checklist includes cost estimates"

---

## 3. Migration Strategy — Legacy Monolith to AWS

### Q: How would you migrate a 10-year-old .NET monolith (on-premises, 500 req/s, 2TB SQL Server DB) to AWS?

**Answer (6 R's of Migration):**

```
Phase 1: Assessment (Month 1)
  - AWS Application Discovery Service: map dependencies
  - Database size, growth rate, IOPS requirements
  - Network bandwidth between on-prem and AWS (latency, egress cost)
  - Licensing: SQL Server EE → RDS or bring your own license?
  - Compliance: any data that cannot leave on-prem?

Phase 2: Choose Strategy

Rehost (Lift & Shift) — Fastest:
  ✓ EC2 Windows instances (same OS, IIS)
  ✓ RDS SQL Server (or SQL on EC2 if you need SA access)
  ✓ AWS DMS for database migration (minimal downtime)
  ✓ Minimal code changes
  ✗ No cloud-native benefits (still paying for always-on instances)
  Timeline: 2-3 months

Replatform (Lift & Reshape):
  ✓ Containerize: .NET → Docker → ECS Fargate or App Runner
  ✓ Database: RDS SQL Server with Multi-AZ
  ✓ Load balancer: ALB (replaces hardware LB)
  ✓ Moderate code changes (connection strings, secrets in Parameter Store)
  ✓ Auto-scaling, better resource utilization
  Timeline: 4-6 months

Refactor (Strangler Pattern):
  ✓ Monolith stays on-prem initially
  ✓ Carve out microservices one at a time (API Gateway → Lambda/ECS)
  ✓ Database: Start with read replicas in RDS, eventual DynamoDB for new services
  ✓ Hybrid cloud: AWS Direct Connect or VPN for inter-connectivity
  ✓ Significant code changes, but gradual risk
  Timeline: 12-18 months

Recommended for 500 req/s monolith: Replatform
  - Gets you to cloud quickly (3-4 months)
  - Cloud-native enough (auto-scaling, managed DB)
  - Refactor later once you understand AWS usage patterns
```

**Database Migration Deep Dive:**
```
1. AWS DMS (Database Migration Service):
   - Full load + CDC (change data capture) = near-zero downtime
   - On-prem SQL Server → RDS SQL Server
   - Verify replication lag < 5 seconds before cutover
   - Dry-run failover: point app connection string to RDS temporarily

2. Cutover Weekend:
   - Friday 10 PM: Enable maintenance mode
   - Stop writes to on-prem DB
   - Verify DMS replication complete (lag = 0)
   - Redirect ALB to AWS instances
   - Monitor for 4 hours (rollback window)
   - Sunday: Declare success or rollback

3. Rollback Plan:
   - Keep on-prem DB as read-only for 2 weeks
   - Reverse DMS connection (RDS → on-prem) for worst-case revert
```

**Staff Level Answer:** "I'd form a **migration factory**:
- Dedicated migration team (2-3 engineers)
- Proven playbook: replicate success across services
- Automated testing in AWS environment (pre-migration validation)
- Business stakeholder involvement: no surprise outages"

---

## 4. Security & Compliance at Scale

### Q: We're SOC 2 certified and expanding to EU (GDPR). What AWS security controls would you implement?

**Answer:**

```
1. Identity & Access Management:
   
   AWS Organizations:
     - Separate AWS accounts per environment (dev, staging, prod, security)
     - SCPs (Service Control Policies): block regions outside US/EU
     - Centralized CloudTrail in audit account
   
   IAM Best Practices:
     - No root account usage (CloudWatch alarm on root login)
     - SSO via AWS IAM Identity Center (formerly SSO) + Okta/Azure AD
     - MFA enforced for console access (SCPs enforce this)
     - Service roles for EC2/Lambda (no hardcoded credentials)
     - IAM Access Analyzer: detect overly permissive policies
   
   Secrets Management:
     - AWS Secrets Manager for DB credentials (auto-rotation)
     - Parameter Store for config (non-sensitive)
     - KMS customer-managed keys (CMK) for encryption
     - No secrets in code, environment variables, or Lambda env vars

2. Network Security:
   
   VPC Design:
     - Private subnets for compute (no internet route)
     - Public subnets only for ALB/NLB
     - NAT Gateway for outbound (or VPC Endpoints to avoid egress costs)
     - Flow logs → CloudWatch → Athena for threat detection
   
   Security Groups:
     - Default deny, explicit allow only necessary ports
     - No 0.0.0.0/0 ingress except ALB (and even then, CloudFront IP ranges preferred)
     - Prefix lists for internal CIDR blocks
   
   WAF (Web Application Firewall):
     - Protect ALB/CloudFront
     - Managed rules: OWASP Top 10, SQL injection, XSS
     - Rate limiting: 2000 req/5min per IP
     - Geo-blocking: allow only US/EU traffic

3. Data Protection (GDPR):
   
   Encryption:
     - At rest: S3 (SSE-KMS), RDS (KMS), EBS (encrypted volumes)
     - In transit: TLS 1.2+ enforced (ALB listener policy)
     - KMS CMK with key rotation enabled (annual)
   
   Data Residency:
     - AWS Regions: us-east-1, eu-west-1 only (SCP enforces)
     - S3 bucket policies: deny PutObject if not in allowed region
     - RDS/DynamoDB: explicitly set region, block snapshots to other regions
   
   Right to Erasure:
     - Automated deletion pipeline (Lambda triggered by API request)
     - S3 Object Lock for immutable audit logs (WORM compliance)
     - DynamoDB TTL for auto-expiring PII data

4. Monitoring & Incident Response:
   
   GuardDuty:
     - Threat detection (compromised instances, unusual API calls)
     - Alerts to SNS → PagerDuty for 24/7 on-call
   
   Security Hub:
     - Aggregates findings from GuardDuty, Macie, Inspector, Config
     - Compliance frameworks: CIS AWS Foundations, PCI-DSS
   
   AWS Config:
     - Continuous compliance checks (e.g., S3 buckets must not be public)
     - Auto-remediation: Lambda triggered to fix non-compliant resources
   
   CloudTrail:
     - All API calls logged (who did what, when)
     - Immutable logs in S3 with MFA Delete enabled
     - Athena queries for forensics: "show all IAM changes in last 7 days"

5. Audit Trail (SOC 2 Type II):
   - CloudTrail → S3 → Athena → QuickSight dashboard for auditors
   - CloudWatch Logs retention = 1 year minimum
   - VPC Flow Logs: detect data exfiltration patterns
   - AWS Artifact: download compliance reports (SOC 2, ISO 27001, GDPR)
```

**Staff Engineer Addition:** "I'd establish a **security baseline via Terraform modules**:
- Reusable VPC module with Flow Logs, GuardDuty enabled
- S3 bucket module with encryption, versioning, access logging enforced
- Config rules deployed to all accounts via CloudFormation StackSets
- Quarterly AWS Well-Architected Review (Security Pillar)"

---

## 5. Observability & Operational Excellence

### Q: Your ECS service is experiencing intermittent 504 Gateway Timeout errors. How do you debug it?

**Answer (Systematic Debugging):**

```
1. Clarify the Problem:
   - 504 = timeout from load balancer waiting for backend
   - Is it all requests or specific endpoints?
   - Started when? (correlate with recent deployments)
   - Percentage of requests affected? (1% or 50%?)

2. Check Application Logs:
   - CloudWatch Logs Insights query:
     ```
     fields @timestamp, @message
     | filter @message like /ERROR|Exception|timeout/
     | sort @timestamp desc
     | limit 100
     ```
   
   - ECS Task logs: Are tasks crashing/restarting?
   - Application metrics: What was response time before timeout?

3. Check Load Balancer Metrics (CloudWatch):
   - TargetResponseTime: Is it spiking above ALB timeout (default 60s)?
   - UnHealthyHostCount: Are targets failing health checks?
   - HTTPCode_Target_5XX_Count: Increasing?
   - ActiveConnectionCount: Connection pool exhaustion?

4. Check ECS Service Metrics:
   - CPUUtilization: Maxed at 100%? (need to scale up)
   - MemoryUtilization: Swap thrashing?
   - Service events: Deployment in progress? Auto-scaling triggering?
   - Task count: Not enough tasks to handle load?

5. Check Target Health:
   - ALB → Target Groups → Targets tab
   - Are targets marked unhealthy? (failed health checks)
   - Health check path: /health returning 200?
   - Health check interval: 30s, timeout: 5s (reasonable?)

6. Check Database (If applicable):
   - RDS CloudWatch: DatabaseConnections spiking?
   - ReadLatency / WriteLatency: Disk I/O bottleneck?
   - FreeableMemory: Low memory = disk swapping
   - Slow query log: Any queries taking >10 seconds?

7. Network Issues:
   - VPC Flow Logs: Packet loss? Rejected connections?
   - Security Groups: Are health checks blocked?
   - NAT Gateway: Bandwidth limit hit? (45 Gbps per NAT)

8. Root Cause Example:
   "Found: RDS connection pool exhausted (100/100 connections in use)
    Tasks waiting indefinitely for DB connection → 60s timeout → 504"
   
   Fix:
     - Increase RDS max_connections (requires instance restart)
     - Tune app connection pool: maxPoolSize=50, timeout=10s
     - Add RDS read replica for read-heavy queries
     - Implement connection pooling with PgBouncer (for Postgres)
```

**Observability Stack (Staff Engineer Level):**
```
Metrics:
  - CloudWatch Container Insights (ECS task-level metrics)
  - Prometheus + Grafana for custom app metrics
  - X-Ray for distributed tracing (see full request path)

Logs:
  - CloudWatch Logs with structured JSON (parsable)
  - Log sampling: 100% errors, 1% success (cost optimization)
  - Log retention: 7 days hot, 90 days in S3, 365 days Glacier

Tracing:
  - AWS X-Ray: trace ID propagated through ALB → ECS → RDS
  - Segments show exactly where latency occurs (DB query? external API?)
  - Annotations for custom metadata (user_id, feature_flag)

Alerting:
  - CloudWatch Alarms → SNS → PagerDuty
  - Composite alarms: Only alert if BOTH high latency AND high error rate
  - Runbooks linked in alert (Confluence/Notion): "How to debug 504s"

Dashboards:
  - CloudWatch dashboard: P50/P95/P99 latency, error rate, throughput
  - RED metrics: Rate, Errors, Duration (Golden Signals)
  - Auto-refresh every 1 minute, displayed on TV in NOC
```

**Staff Engineer Insight:** "I'd implement **Service Level Objectives (SLOs)**:
- SLO: 99.9% of requests complete successfully within 500ms
- Error budget: 0.1% = 43 minutes downtime/month
- If budget exhausted → freeze deployments until RCA complete
- This shifts culture from 'move fast' to 'move fast AND measure reliability'"

---

## 6. When NOT to Use AWS Services

### Q: When would you choose NOT to use Lambda, and stick with ECS/EC2?

**Answer:**
```
Lambda is GREAT for:
  ✓ Event-driven workloads (S3 upload → process image)
  ✓ Scheduled jobs (CloudWatch Events every 5 minutes)
  ✓ API Gateway backends (low-moderate traffic <100 req/s)
  ✓ Short-lived tasks (<15 minutes execution limit)

Lambda is TERRIBLE for:
  ❌ Long-running processes (>15 min = hard limit)
  ❌ High sustained traffic (1000+ req/s) — cold starts kill latency
  ❌ Stateful workloads (no shared memory between invocations)
  ❌ Large dependencies (500 MB unzipped limit — ML models often exceed this)
  ❌ Predictable baseline traffic (you're paying $0.20 per 1M requests even if idle)

Example: Video Encoding Service
  - Lambda: Timeout at 15 min, 10 GB RAM max → can't encode 4K video
  - ECS Fargate: Run for hours, 30 GB RAM, GPU support via EC2 instances
  - Cost: Lambda = $0.20/1M + $0.0000166667/GB-second
         ECS Spot = $0.03/hour (t3.medium) = 10x cheaper for sustained workload

Example: High-Throughput API (5000 req/s)
  - Lambda: Cold start = 1-2 seconds, provisioned concurrency = $$$ expensive
  - ECS: Warm containers, predictable latency, auto-scaling based on CPU
```

**When NOT to use RDS, use EC2:**
```
❌ Need root/superuser access (install extensions, custom pg_hba.conf)
❌ Database engine not supported (CockroachDB, TimescaleDB custom build)
❌ Licensing edge case (Oracle RAC)
❌ Must run on-premises-like setup for compliance
```

**When NOT to use DynamoDB:**
```
❌ Complex queries (JOIN, GROUP BY, subqueries) — DynamoDB has no SQL
❌ Strong consistency across multiple items (no ACID transactions beyond single partition)
❌ Unpredictable access patterns (RCU/WCU costs explode if you guess wrong)
❌ Ad-hoc analytics (can't run "SELECT * WHERE age > 30" without scanning entire table)

Use Aurora/RDS instead for:
  ✓ Relational data with complex queries
  ✓ ACID transactions across multiple rows
  ✓ SQL familiarity (easier to hire engineers)
```

---

## 7. Designing for Failure (Chaos Engineering)

### Q: How do you ensure your architecture can survive an AZ failure?

**Answer:**
```
Design Principles:
1. No single point of failure
2. Auto-healing (replace failed instances automatically)
3. Graceful degradation (serve stale cache if DB down)
4. Test failure scenarios regularly

Multi-AZ Deployment:
  ALB: Automatically distributes across AZs
  ECS: Tasks spread across 3 AZs (AZ affinity = disabled)
  RDS: Multi-AZ = synchronous replication (30-60s failover)
  Aurora: 6 copies across 3 AZs (4 of 6 quorum for writes)
  ElastiCache: Redis cluster mode with replicas in each AZ

Testing AZ Failure:
  1. AWS Fault Injection Simulator (FIS):
     - Simulate: Stop 100% of EC2 instances in us-east-1a
     - Duration: 15 minutes
     - Observe: Does traffic reroute to 1b and 1c?
     - Rollback: Automatically restart instances after test

  2. Manual Test (Controlled):
     - Saturday 2 AM: Modify ECS service to exclude us-east-1a
     - Monitor CloudWatch: Latency, error rate, target health
     - Verify: No user-facing impact
     - Duration: 30 minutes, then restore

  3. GameDay Exercise:
     - Quarterly drill: Simulate AZ failure + RDS failover
     - Incident commander runs scenario
     - Team practices runbooks in real-time
     - Debrief: What went wrong? Update runbooks.

Metrics to Watch:
  - ALB TargetResponseTime: Should be unchanged
  - ECS RunningTaskCount: Should drop in failed AZ, rise in others
  - RDS Failover: CloudWatch event "DB instance has failed over"
```

**Staff Engineer Perspective:** "I'd codify resilience testing:
- Monthly automated FIS experiments (AZ failure, instance termination)
- SRE team owns GameDay calendar (4x per year)
- New services must pass '1 AZ down' test before prod launch
- Architecture review checklist: 'How does this survive AZ failure?'"

---

## 8. AWS Well-Architected Framework (Staff Level)

### Q: Walk me through how you'd conduct a Well-Architected Review for a critical service.

**Answer:**
```
5 Pillars:

1. Operational Excellence:
   ✓ Infrastructure as Code (Terraform/CloudFormation) — no manual changes
   ✓ CI/CD: Automated tests, blue/green deployments
   ✓ Runbooks for common incidents (link in PagerDuty alert)
   ✓ Regular GameDays to practice incident response
   ✗ Risk: No chaos testing → failures surprise us
   Action: Implement AWS FIS quarterly

2. Security:
   ✓ IAM roles (no long-lived access keys)
   ✓ Encryption at rest (KMS) and in transit (TLS)
   ✓ WAF on ALB, GuardDuty for threat detection
   ✗ Risk: Security groups have 0.0.0.0/0 on port 443 (too permissive)
   Action: Restrict to CloudFront IP ranges only

3. Reliability:
   ✓ Multi-AZ: RDS, ECS tasks, ALB
   ✓ Auto-scaling: CPU-based scaling for ECS
   ✗ Risk: No multi-region failover (RPO = ∞ if us-east-1 down)
   Action: Evaluate Aurora Global Database (cost vs risk)
   ✗ Risk: Single ALB (no failover if ALB control plane issue)
   Action: CloudFront in front of ALB (caches + alternate origin)

4. Performance Efficiency:
   ✓ Right-sized instances (Compute Optimizer recommendations applied)
   ✓ CloudFront CDN for static assets
   ✗ Risk: No caching layer (every request hits DB)
   Action: Add ElastiCache (Redis) for frequently accessed data
   ✗ Risk: N+1 query problem (app fetches related records in loop)
   Action: Code review + query optimization

5. Cost Optimization:
   ✓ Spot instances for batch workloads
   ✓ S3 lifecycle policies (30d → IA, 90d → Glacier)
   ✗ Risk: Running t3.2xlarge 24/7 at 10% CPU (over-provisioned)
   Action: Rightsize to t3.large (50% cost savings)
   ✗ Risk: Data transfer cost = $10k/month (NAT Gateway egress)
   Action: VPC Endpoints for S3/DynamoDB (eliminates NAT)

Review Process:
1. Pre-work: Engineers fill out questionnaire (AWS WAR Tool)
2. Workshop: 2-hour session with architects + engineers
3. Output: Risk register with prioritized action items
4. Follow-up: Quarterly check-in on progress
```

---

## 9. Organization-Wide Technical Decisions

### Q: As a Staff Engineer, how would you influence the adoption of AWS across a 500-person engineering org?

**Answer:**
```
1. Start with a Pilot (Prove Value):
   - Choose 1-2 non-critical services to migrate first
   - Measure: Cost, deployment speed, developer happiness
   - Document: Architecture patterns, gotchas, cost surprises
   - Present results to leadership (data-driven case)

2. Build a Platform Team:
   - Landing Zone: AWS Organizations, SSO, baseline security
   - Self-service: Terraform modules for common patterns (VPC, ECS, RDS)
   - Golden path: "Here's how to deploy a service in 1 day"
   - Inner-sourcing: GitHub repo with reusable modules + docs

3. Establish Guardrails (Not Gates):
   - SCPs: No one can delete CloudTrail, launch in unapproved regions
   - AWS Config: Auto-remediation for non-compliant resources
   - Cost alerts: $5k per team per month, alerts at 80%
   - Security baseline: All accounts get GuardDuty, Security Hub

4. Training & Enablement:
   - AWS Immersion Days (hands-on workshops)
   - Internal certification: "MovieApp AWS Certified" (company-specific)
   - Office hours: Staff Engineers available 2x/week for questions
   - Champions: 1 per team, they attend monthly AWS sync

5. Conway's Law:
   - Org structure follows architecture
   - Don't force microservices if teams aren't ready
   - Start with modular monolith (boundaries = future services)
   - Extract services when team structure supports it

6. Metrics of Success:
   - Lead time: Commit to prod (30 days → 2 days)
   - Deployment frequency: Monthly → daily
   - MTTR: 4 hours → 30 minutes (auto-scaling + ECS task replacement)
   - Cost per transaction: Track monthly (should decrease with scale)
```

**Staff Engineer Mindset:** "I don't push tech for tech's sake. I'd ask:
- What business problem does AWS solve? (Faster shipping? Lower cost? Compliance?)
- What's the risk if we stay on-prem? (Slow provisioning? Maintenance burden?)
- What's the migration cost? (Engineering time, downtime, training)
- Is now the right time, or should we wait?"

---

## 10. Trade-offs & Real-World Constraints

### Q: You need to choose between Aurora Serverless v2, Aurora Provisioned, or RDS Postgres. How do you decide?

**Answer:**
```
Aurora Serverless v2:
  ✓ Scales up/down based on load (0.5 ACUs to 128 ACUs)
  ✓ Pay only for what you use (per-second billing)
  ✓ Great for variable workloads (e.g., B2B app idle nights/weekends)
  ✗ Cold start: None (always has min 0.5 ACU running)
  ✗ Cost: More expensive than RDS for sustained baseline load
  ✗ ~15% more expensive than Aurora Provisioned at steady-state

Aurora Provisioned:
  ✓ Predictable performance (you choose instance type)
  ✓ Lower cost for sustained 24/7 workload (Reserved Instances)
  ✓ Read replicas scale reads (up to 15 replicas)
  ✗ Manual scaling: Must change instance type manually (5-10 min downtime)
  ✗ Over-provision for peak, pay 24/7 even when idle

RDS Postgres:
  ✓ Cheapest option (Standard pricing)
  ✓ Most compatible with standard Postgres (if you use extensions)
  ✓ Easiest migration from on-prem Postgres (dump/restore)
  ✗ No serverless scaling (manual instance resize)
  ✗ Slower failover than Aurora (60s vs 30s)
  ✗ No Global Database (multi-region = manual setup)

Decision Matrix:

| Scenario | Best Choice | Why |
|---|---|---|
| Startup, unpredictable load | Aurora Serverless v2 | Scales to zero, no capacity planning |
| E-commerce (peak = 10x baseline) | Aurora Serverless v2 | Auto-scales during Black Friday |
| Financial SaaS (24/7, steady) | Aurora Provisioned | Reserved Instance = 40% savings |
| Internal tool (used 9-5, M-F) | Aurora Serverless v2 | Scales down nights/weekends |
| Lift-and-shift from on-prem | RDS Postgres | Easiest migration, full control |
| Strict Postgres compatibility | RDS Postgres | Aurora has minor differences |

Cost Example (100 GB storage, 2 vCPU, 16 GB RAM):
  - RDS Postgres: ~$300/month (db.r6g.large, on-demand)
  - Aurora Provisioned: ~$450/month (db.r6g.large, on-demand)
  - Aurora Serverless v2: ~$350-600/month (depends on utilization)

Reserved Instance Savings:
  - 1-year no-upfront: 40% discount
  - 3-year all-upfront: 60% discount
  - Only buy RI if workload is proven stable for 12+ months
```

---

## 11. Advanced Networking

### Q: Explain VPC peering vs Transit Gateway vs VPN. When do you use each?

**Answer:**
```
VPC Peering:
  What: 1-to-1 connection between two VPCs
  Use case: Prod VPC ↔ Shared Services VPC (Active Directory, monitoring)
  Pros:
    ✓ No data transfer charges within same region
    ✓ Low latency (direct connection)
    ✓ Simple setup (request/accept)
  Cons:
    ❌ No transitive routing (if A↔B and B↔C, A cannot reach C)
    ❌ Mesh complexity: 50 VPCs = 1,225 peering connections (n²/2)
    ❌ Manual route table updates per peering

Transit Gateway:
  What: Hub-and-spoke model (all VPCs connect to TG)
  Use case: 50+ VPCs, need centralized routing
  Pros:
    ✓ Transitive routing (A→TG→B→TG→C works)
    ✓ One route table entry per VPC (scalable)
    ✓ Supports VPN, Direct Connect attachments
  Cons:
    ❌ Data transfer cost: $0.02/GB processed (can add $10k+/month)
    ❌ Added latency: ~1-2ms per hop through TG
    ❌ Cost: $0.05/hour per attachment (~$36/month per VPC)

VPN (Site-to-Site):
  What: IPsec tunnel from on-prem to AWS VPC
  Use case: Hybrid cloud, temporary migration path
  Pros:
    ✓ Encrypted tunnel over internet
    ✓ Fast setup (30 minutes)
    ✓ $0.05/hour + $0.09/GB outbound
  Cons:
    ❌ Limited bandwidth: 1.25 Gbps per tunnel (2 tunnels = 2.5 Gbps max)
    ❌ Variable latency (internet-based)
    ❌ Not suitable for high-throughput workloads (DB replication)

AWS Direct Connect:
  What: Dedicated fiber connection (1 Gbps or 10 Gbps)
  Use case: Production hybrid cloud, large data transfers
  Pros:
    ✓ Consistent latency (<10ms typical)
    ✓ Higher bandwidth (10/100 Gbps)
    ✓ Lower data transfer cost ($0.02/GB vs $0.09/GB)
  Cons:
    ❌ Expensive: $0.30/hour (1 Gbps) = $2,160/year minimum
    ❌ Long setup: 1-3 months (physical fiber installation)
    ❌ Single point of failure (need 2x DX for HA)

Decision Tree:
  - 2-3 VPCs → VPC Peering (simple)
  - 10+ VPCs, need transitive routing → Transit Gateway
  - On-prem to AWS, <1 Gbps → VPN
  - On-prem to AWS, >1 Gbps, latency-sensitive → Direct Connect
  - Multi-region: Transit Gateway Peering (cross-region)
```

---

## 12. Event-Driven Architecture on AWS

### Q: Design an event-driven order processing system (orders → inventory → shipping → notifications).

**Answer:**
```
Architecture:

1. Event Bus: Amazon EventBridge (or SNS/SQS)
   - Central nervous system of the architecture
   - 100+ GB/day, 10M events/day = EventBridge
   - Simple pub/sub = SNS + SQS

2. Services:
   Order Service:
     - API Gateway + Lambda (or ECS)
     - Writes to RDS (order details)
     - Publishes: OrderCreated event → EventBridge
   
   Inventory Service:
     - Listens: OrderCreated
     - Checks stock in DynamoDB
     - Publishes: InventoryReserved or InventoryFailed
   
   Shipping Service:
     - Listens: InventoryReserved
     - Calls 3rd party API (FedEx)
     - Publishes: ShippingScheduled
   
   Notification Service:
     - Listens: OrderCreated, ShippingScheduled
     - Sends email via SES, push via SNS

3. Event Schema:
   {
     "source": "order-service",
     "detail-type": "OrderCreated",
     "detail": {
       "orderId": "12345",
       "userId": "67890",
       "items": [{"sku": "ABC", "qty": 2}],
       "timestamp": "2026-03-27T10:30:00Z"
     }
   }

4. Failure Handling:
   - Dead Letter Queue (DLQ): If Inventory Service fails 3x, move to DLQ
   - Retry policy: Exponential backoff (1s, 2s, 4s, 8s, 16s)
   - Alerting: CloudWatch alarm if DLQ depth > 10 messages
   - Manual intervention: Lambda to replay DLQ messages after fix

5. Idempotency:
   - Each event has unique event ID
   - Services store processed event IDs in DynamoDB
   - If duplicate received → skip processing (already done)

6. Observability:
   - X-Ray: Trace event through entire flow (Order → Inventory → Shipping)
   - CloudWatch Logs: Each service logs event received + processed
   - EventBridge Archive: Replay events for debugging (7-day retention)

Cost:
  - EventBridge: $1 per million events
  - SQS: $0.40 per million requests
  - Lambda: $0.20 per million invocations
  - 10M orders/month = ~$50/month for event infrastructure
```

**Why EventBridge over SNS/SQS:**
```
Use EventBridge when:
  ✓ Need event filtering (route OrderCreated with amount>$1000 to fraud-check)
  ✓ Need schema registry (typed events, versioning)
  ✓ Need event replay (archive + replay for testing)
  ✓ 100+ consumers (SNS has 12.5M subs limit, EventBridge has no practical limit)

Use SNS/SQS when:
  ✓ Simple pub/sub (less overhead)
  ✓ FIFO ordering required (SQS FIFO)
  ✓ Very high throughput (SQS = unlimited, EventBridge = 10k events/sec default)
```

---

## Key Takeaways for Staff Engineer Interviews

1. **Think in Trade-offs:** "We could use Aurora, but RDS is 40% cheaper and meets our latency SLO."

2. **Business Context:** "This architecture costs $50k/month but reduces deployment time from 2 weeks to 2 hours — worth it?"

3. **Failure Scenarios:** Always ask "What breaks in this design? How do we recover?"

4. **Cost Awareness:** You're not just building systems, you're managing TCO (Total Cost of Ownership).

5. **Organizational Impact:** How does this decision affect 500 engineers? Training? Hiring? Culture?

6. **Well-Architected Lens:** Every design review should touch all 5 pillars (Ops, Security, Reliability, Performance, Cost).

7. **When to Say No:** "Lambda won't work here because 15-min timeout." Show you know the limits.

8. **Real Production Experience:** Reference "When X went down in prod, we did Y" — interviewer wants war stories.

9. **Humility:** "I'd need to research X" or "I haven't worked with Y at scale" — honesty > bluffing.

10. **Ask Clarifying Questions:** "What's the current request volume? What's the budget? What's the compliance requirement?" — constraints matter.

---

## Bonus: AWS Services You Should Know (15 Years Experience)

**Core Compute:**
EC2, ECS, EKS, Fargate, Lambda, App Runner

**Storage:**
S3, EBS, EFS, FSx, Glacier, Storage Gateway

**Database:**
RDS (Postgres, MySQL, SQL Server), Aurora, DynamoDB, DocumentDB, ElastiCache (Redis, Memcached), MemoryDB

**Networking:**
VPC, ALB/NLB, Route 53, CloudFront, API Gateway, Transit Gateway, Direct Connect, VPN, PrivateLink

**Security:**
IAM, STS, Organizations, KMS, Secrets Manager, GuardDuty, Security Hub, WAF, Shield, Macie, Inspector

**Observability:**
CloudWatch (Logs, Metrics, Alarms), X-Ray, CloudTrail, Config, Systems Manager

**DevOps:**
CodePipeline, CodeBuild, CodeDeploy, CloudFormation, CDK, Elastic Beanstalk

**Messaging:**
SQS, SNS, EventBridge, Kinesis (Data Streams, Firehose), MSK (Kafka)

**Serverless:**
Lambda, API Gateway, Step Functions, EventBridge, DynamoDB

**Analytics:**
Athena, Glue, Redshift, QuickSight, EMR, OpenSearch

**ML/AI:**
SageMaker, Bedrock, Rekognition, Comprehend, Kendra

**Migration:**
DMS, Application Discovery Service, Migration Hub, Snow Family

**Cost Management:**
Cost Explorer, Budgets, Compute Optimizer, Trusted Advisor

---

_This guide reflects Staff-level thinking: not just "what" but "why", "when", and "at what cost"._
