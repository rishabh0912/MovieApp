1. Identity Service
   - Create Signed JWT
     public string generatetoken(user user)
    {
        var securityKey = this will be the key stored in appsettings.json
        var issuer = get from appsettings.json
        var audience = get from appsettings.json
        var expiry = get from appsettings.json

        1. build claims
        Here Claim - is defined in System.Security.Claims.Claim
        JwtRegisteredClaimNames - this is also predefined
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UseName),
            new Claim(ClaimTypes.Role, user.Roles),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        2. Create signing key and credentials
        Create key using SymmetricSecurityKey and secret-key from appsettings.json 
        SigningCredentials - thsi is for signing 
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBtes(secretKey))
        var creds = new SigningCredentials(key, SecurityAlgortihtm.HmacSha256)

        3. Build the token
        for build we will pass issuer, audience, claim, expryr and signingcredentials
        var token = new JwtSecurityToken(

        )

    }
    - Concept of Refresh Token
      - Stored in a table, will be updated once used or revoked.

2. Movie Service
    - Concept of join tables - We used two join tables, instead of having join while querying - for normalization
    - Composite Key in AppDbContext - OnModelCreating - Build composite keys
    - While building the table, since we have ICollection - this tells EF Core about the relationship in object model
            public class Movie
        {
            public Guid Id { get; set; }
            public string Title { get; set; }

            public ICollection<MovieCast> MovieCasts { get; set; } = new List<MovieCast>();
        }
        This means:

        👉 One Movie → many MovieCast rows
        👉 It is a navigation property

        It allows EF to:

        ✔ Load related data
         ✔ Track relationships
        ✔ Perform joins automatically

        Include tells EF:

        👉 “Load related data along with the main entity”
        var movies = await _context.Movies
            .Include(m => m.MovieCasts)
            .ToListAsync();

        This loads:

        ✔ Movies
        ✔ MovieCasts rows for each movie

        ThenInclude is used for:

        👉 Loading the next level navigation

        Example:
        var movies = await _context.Movies
            .Include(m => m.MovieCasts)
                .ThenInclude(mc => mc.Cast)
                .ToListAsync();

                Now EF loads:

                ✔ Movies
                ✔ MovieCasts
                ✔ Cast

        How it reads in english
        .Include(m => m.MovieCasts)
        ➡ For each Movie, load its MovieCasts
        .ThenInclude(mc => mc.Cast)
        ➡ For each MovieCast, load the Cast

3. Rating Service
    - Authentication
        - Add Nuget Package
            dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 9.0.2
        -  Add to appsettings.json — same JwtSettings block as Identity Service (same secret key):
        - Register services in Program.cs — add these before var app = builder.Build():
        - JWT Has 3 Parts
            HEADER.PAYLOAD.SIGNATURE
        - In Identity Service JWT Is created
                        var key = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(secretKey));

                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer,
                    audience,
                    claims,
                    expires: DateTime.UtcNow.AddMinutes(60),
                    signingCredentials: creds);
            This creates a signature using the secret key
                                signature = HMACSHA256(
                    base64Url(header) + "." + base64Url(payload),
                    secretKey
                )
           How Validation Works (MovieService / RatingService)

                        IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]))

            What happens internally:

                1️⃣ API receives JWT from request header
                2️⃣ Middleware extracts token
                3️⃣ It recomputes the signature using the same secret key
                4️⃣ Compares:
                computedSignature == tokenSignature ?
                ✔ If match → token is valid
                ❌ If not → 401 Unauthorized
                So all services must share = 
                JwtSettings:SecretKey
                JwtSettings:Issuer
                JwtSettings:Audience
    - Authorization
        When we put
        [Authorize]
        The authorization middleware checks:
        HttpContext.User.Identity.IsAuthenticated
        That property is set by JwtBearerHandler after successful validation.
                    UseAuthentication()
                        → JwtBearerHandler
                            → JwtSecurityTokenHandler.ValidateToken()
                                → compares signature using SecretKey

                    It adds:
                •	Authorization handlers
                •	Policy engine
                •	Role/claim evaluators
                •	Default policy (requires authenticated user)

    - HttpContext
        - This is used to get the userId