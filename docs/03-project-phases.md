1. Phase 0: Scope Freeze
    Output:

        Service boundaries
        Core entities and endpoints
        Auth flow and MVP definition
    Done when:
        You can explain architecture in 2 minutes.

2. Phase 1: Solution Skeleton
    Output:

        movieApp.sln
        Api/Application/Infrastructure/Domain projects per service
        Basic health endpoints
    Done when:
        All projects build and run.

3. Phase 2: Identity Service
    Output:

        Register/Login
        JWT + refresh token
        Protected endpoint test
    Done when:
        Valid token can access secured API.

4. Phase 3: Movie + Rating Services
    Output:

        Movie CRUD
        Rating add/update with userId+movieId uniqueness
        Average rating endpoint
    Done when:
        End-to-end movie rating flow works.

5. Phase 4: API Gateway
    Output:

        YARP routes to all services
        Auth and correlation propagation
    Done when:
        Client calls only gateway.

6. Phase 5: Containers (Local)
    Output:

        Dockerfile per service
        docker-compose for gateway + 3 services + DBs
    Done when:
        One command starts full stack locally.

7. Phase 6: CI/CD + AWS
    Output:

        GitHub Actions pipeline
        ECR image push
        ECS Fargate deploy (+ RDS, secrets)
    Done when:
        Merge to main deploys to dev automatically.
