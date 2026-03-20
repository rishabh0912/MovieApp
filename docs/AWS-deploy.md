Phase 1 - Step by Step

Step 1: Setup by ECR

1. In ECR - Create Repository
   Create 3 repositories 
   - movieapp/identity-service
   - movieapp/movie-service
   - movieapp/rating-service
2. Registry URI Created
   - <account-id>.dkr.ecr.<region>.amazonaws.com
   - 203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/identity-service
   - 203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/movie-service
   - 203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/rating-service


Step 2: Set up RDS PostgresSQL

1. RDS -> Create Database
2. DB Instance Identifier - movieapp-db
3. Username - postgres
4. Password - postgres
5. Instance - db.t3.micro
6. Public Access - Yes - to run migrations
7. Endpoint - movieapp-db.cnfwzivjzbee.ap-south-1.rds.amazonaws.com
8. Add inbound rule like below - to allow my local machine'IP so I can run migrations from my laptop
   Type - PostgreSQL
   Port - 5432
   Source - My IP
9. Run below command to connect AWS Postgres from local terminal
   psql -h <rds endpoint> -U postgres -p 5432
   psql -h movieapp-db.cnfwzivjzbee.ap-south-1.rds.amazonaws.com -U postgres -p 5432
10. Run below commands to create database 
    CREATE DATABASE "IdentityDb";
    CREATE DATABASE "MovieDb";
    CREATE DATABASE "RatingsDb";
11. Run migrations for all Dbs

    Movie Service
    ConnectionStrings__DefaultConnection="Host=<rds endpoint>;Port=5432;Database=MovieDb;Username=postgres;password=postgres" \
    dotnet ef database update \
        --project ./src/MovieService/MovieService.Infrastructure \
        --startup-project ./src/MovieService/MovieService.Api
    
    Identity Service
    ConnectionStrings__DefaultConnection="Host=<rds endpoint>;Port=5432;Database=IdentityDb;Username=postgres;password=postgres" \
    dotnet ef database update \
        --project ./src/IdentityService/IdentityService.Infrastructure \
        --startup-project ./src/IdentityService/IdentityService.Api

    Rating Service
    ConnectionStrings__DefaultConnection="Host=<rds endpoint>;Port=5432;Database=RatingDb;Username=postgres;password=postgres" \
    dotnet ef database update \
        --project ./src/RatingService/RatingService.Infrastructure \
        --startup-project ./src/RatingService/RatingService.Api

Step 3: VPC + Security Groups
    1. Create new Security Group movieapp-ecs-sg - sg-00f95ac8ad24e4bf6
       Create a new inbound rule
       Custom TCP    8080 (port)   anywhere (0.0.0.0/0) 
       This rule will basically allow traffic to ECS from anywhere on port 8080
       As container is running on 8080
       Port 8080 inbound -> From internet (Your API access)
       Internet -> Port 8080 -> ECS API
    2. Now attach this sg to existing RDS security group as inbound
       PostgreSQL     5432.       sg-00f95ac8ad24e4bf6
       ECS API -> Port 5432 -> RDS

Step 4: Build & Push Docker Images to ECR

    0. Verify the account
       aws sts get-caller-identity

    1. Authenticate Docker with ECR (token expires after 12h, re-run if needed)
       aws ecr get-login-password --region ap-south-1 | \
         docker login --username AWS --password-stdin 203193248552.dkr.ecr.ap-south-1.amazonaws.com

    2. Build images (run from project root)
       docker build -t movieapp/identity-service ./src/IdentityService
       docker build -t movieapp/movie-service     ./src/MovieService
       docker build -t movieapp/rating-service    ./src/RatingService

    3. Tag images with ECR URIs
       docker tag movieapp/identity-service:latest 203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/identity-service:latest
       docker tag movieapp/movie-service:latest    203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/movie-service:latest
       docker tag movieapp/rating-service:latest   203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/rating-service:latest

    4. Push images to ECR
       docker push 203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/identity-service:latest
       docker push 203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/movie-service:latest
       docker push 203193248552.dkr.ecr.ap-south-1.amazonaws.com/movieapp/rating-service:latest

    5. Verify images are visible in ECR console
       AWS Console -> ECR -> each repo -> should show a "latest" image with a push timestamp

Step 5: ECS Cluster
    1. Create cluster
       Name - movieapp-cluster
       Infrastructure - Fargate
    2. Create role - AWSServiceRoleForECS
       This allows ECS to
        - Create ENIs (network interfaces)
        - attach load balancer
        - pull images
        - manage tasks
    3. Create Task Definition (one per service)
        - Launch Type - Fargate
        - Add Container - Image URI - Created in Step 1 for each service
        - Health Check command: CMD-SHELL,curl -f http://localhost:8080/health || exit 1
          (interval 30s, timeout 5s, retries 3, start period 60s)
        - Add below Environment Variables
          
          ConnectionStrings__DefaultConnection = Host=<rds-endpoint>;Port=5432;Database=IdentityDb;Username=postgres;Password=postgres

          JwtSettings__SecretKey = SecretForMovieAppRequiredToCreateJwt
          
          JwtSettings__Issuer = movieApp
          
          JwtSettings__Audience = movieApp-clients
    
    4. Create ECS Service (one per task definition)
       - This will be created in ECS Cluster page
       - Launch Type - Fargate
       - Attach the sg already created - movieapp-ecs-sg
       - Assign public IP

Step 6: Application Load Balancer

    1. Go to EC2 → Load Balancers → Create ALB
        Name: movieapp-alb
        Scheme: Internet-facing
        Listener: HTTP port 80
    2. Create 3 Target Groups:
        identity-tg → port 8080, health check /health
        movie-tg → port 8080, health check /health
        rating-tg → port 8080, health check /health
    3. Add Listener Rules on the ALB:
        /auth/* → identity-tg
        /movies/* → movie-tg
        /genres/* → movie-tg
        /rating/* → rating-tg