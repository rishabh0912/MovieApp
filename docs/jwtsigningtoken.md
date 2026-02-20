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

Two types of JWT signing:

Type	Algorithm	What you store	Who verifies
Symmetric (what we're using)	HMAC-SHA256	One shared secret key	Anyone with the same secret
Asymmetric (enterprise)	RS256 / ES256	Private key (signs) + Public key (verifies)	Anyone with the public key

Our Approach
Identity Service                    API Gateway
──────────────                      ───────────
SecretKey = "abc123..."             SecretKey = "abc123..."  ← same key
     ↓                                    ↓
Signs JWT with key              Verifies JWT with key

Asymmetric - enterprise approach

Identity Service                    API Gateway / Any Service
──────────────                      ────────────────────────
Private Key (secret, never shared)  Public Key (downloadable by anyone)
     ↓                                    ↓
Signs JWT                           Verifies JWT

Private key signs, public key verifies. Services never need the private key. This is safer at scale because:

Compromise of a service only exposes the public key (useless for signing)
Private key stays in one place only