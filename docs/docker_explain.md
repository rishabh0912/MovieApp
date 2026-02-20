Step 1
Create Dockerfile for each microservice
    - Create for identity service
    - Create for movie service
    - Create for rating service
    - Each service must listen on a fixed port inside the container
     builder.WebHost.ConfigureKestrel(options =>
     {
        options.ListenAnyIP(8080);
     });
      So every container exposes http://<container>:8080
      Each container has its own network namespace
      So port 8080 in Identity ≠ port 8080 in Movie ≠ port 8080 in Rating
      "Start the web server (Kestrel) and listen on port 8080 on all network interfaces”"
      By default, ASP.NET runs on:
      http://localhost:5000
      But inside a container, localhost means:

        👉 only inside the container
        ❌ not accessible from other containers
        ❌ not accessible from host
        So we change it to:
        ListenAnyIP(8080)
        Which means:

        ✔ Listen on 0.0.0.0 (all interfaces)
        ✔ Other containers can call it
        ✔ Host port mapping works
    - Test One Service First (Identity)
    docker build -t identity-service -f src/IdentityService/Dockerfile src/IdentityService

    Run it 
    docker run -p 5001:8080 identity-service

    Test
    http://localhost:5001/swagger
