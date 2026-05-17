# External Partner Authentication — B2B Security Guide

_How to authenticate external partners when you don't have ADFS or a shared identity provider._

---

## The Problem

When your own users log in, you control the identity provider (IdP) — you issue JWTs, manage passwords, handle sessions.

With **external partners** (another company's system calling your API), you have none of that:
- You don't manage their users or credentials
- They don't trust your IdP and you don't trust theirs
- You can't share a database or internal network
- ADFS (Active Directory Federation Services) only works when both sides are in a Windows/Azure AD ecosystem — useless if the partner has a completely different stack

**The core question:** How do you know the incoming request is really from Partner A and not someone pretending to be Partner A?

---

## Option 1 — API Keys (Simplest, Weakest)

### What it is
Issue each partner a long random secret string. They include it in every request header.

```
GET /api/movies HTTP/1.1
X-Api-Key: pk_live_a3f9b2c1d4e5...
```

### How it works
```
Partner A ──── X-Api-Key: abc123 ──── Your API Gateway
                                          │
                                    Lookup key in DB
                                    Match → Partner A
                                    Allow/deny by permissions
```

### Pros
- Simple to implement
- Easy to rotate (issue new key, revoke old)
- Works for any tech stack

### Cons
- Key is a bearer token — stolen key = attacker can impersonate partner
- Key travels in every request (exposure risk in logs, TLS termination proxies)
- No proof of *who* sent it, only *that someone with the key* sent it
- No built-in expiry unless you enforce it

### When to use
Low-risk, read-only APIs. Internal tooling between trusted systems. Quick integrations.

---

## Option 2 — mTLS (Mutual TLS) ← The Gold Standard for B2B

### What is TLS normally?

Normal HTTPS (one-way TLS):
```
Client → Server: "Here's my request"
Server → Client: "Here's my certificate, verify I'm real"
Client:           Validates server cert against trusted CA
                  Connection established — data encrypted
```

Only the **server** proves its identity. The client (partner) is anonymous.

### What mTLS adds

mTLS = **both sides present certificates**:
```
Client → Server: "Here's my certificate" + "Here's my request"
Server → Client: "Here's my certificate"
Both sides:       Validate each other's cert against a trusted CA
                  Connection established — both identities proven
```

### Concrete flow for external partners

```
┌─────────────────────────────────────────────────────┐
│  Setup (one time per partner)                        │
│                                                      │
│  1. You run a Certificate Authority (CA)             │
│     or use a shared CA like AWS Private CA           │
│                                                      │
│  2. You issue Partner A a client certificate:        │
│     - CN=partner-a.company.com                       │
│     - Signed by your CA                              │
│     - Valid for 1 year                               │
│     - Contains partner's public key                  │
│                                                      │
│  3. Partner A gets: client.crt + client.key          │
│     You keep:       ca.crt (to verify partner certs) │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│  Runtime (every request)                             │
│                                                      │
│  Partner A calls your API:                           │
│  curl --cert client.crt --key client.key \           │
│       https://api.yourcompany.com/data               │
│                                                      │
│  Your server:                                        │
│  1. Checks partner's cert was signed by YOUR CA     │
│  2. Checks cert is not expired / not revoked (CRL)  │
│  3. Extracts CN (partner-a.company.com) = identity  │
│  4. Maps CN to permissions in your DB               │
│  5. Allows or denies the request                     │
└─────────────────────────────────────────────────────┘
```

### Why mTLS is strong
- **Both sides are cryptographically verified** — not just "has a secret string", but "possesses the private key that matches the certificate we issued"
- **Private key never leaves the partner's system** — unlike API keys, the secret (private key) is never transmitted
- **Traffic is encrypted end-to-end**
- **Certificate revocation** — if a partner is compromised, you revoke their cert via CRL (Certificate Revocation List) or OCSP and they instantly lose access
- Built on asymmetric cryptography: what's encrypted with the public key can only be decrypted with the matching private key (and vice versa)

### Private Key / Public Key — how it works

```
Partner has:
  private.key  ← NEVER shared, stays on their server
  public.key   ← embedded in their certificate, you can see this

How identity proof works:
  1. During TLS handshake, server sends a random challenge
  2. Partner signs it with their private.key
  3. Server verifies the signature using public.key in the certificate
  4. Only the holder of private.key could produce that signature
  → Identity proven without transmitting the secret
```

This is the fundamental principle behind all certificate-based auth. The certificate is like a passport (public, can be shown to anyone). The private key is the biometric that proves you're the passport holder.

### Setting up mTLS in .NET (ASP.NET Core)

```csharp
// Program.cs
builder.WebHost.ConfigureKestrel(options => {
    options.ConfigureHttpsDefaults(https => {
        https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
        https.ClientCertificateValidation = (cert, chain, errors) => {
            // Validate cert was issued by YOUR CA
            return cert.Issuer == "CN=YourCompanyCA";
        };
    });
});

// Middleware to extract partner identity from cert
app.Use(async (context, next) => {
    var cert = context.Connection.ClientCertificate;
    if (cert != null) {
        var partnerName = cert.GetNameInfo(X509NameType.SimpleName, false);
        context.Items["PartnerId"] = partnerName; // "partner-a.company.com"
    }
    await next();
});
```

---

### Q: With mTLS, do we also need to pass a JWT access token?

**Short answer:** It depends on what you need mTLS to do — authentication alone, or authentication + authorization.

mTLS and JWT solve **different problems**:

| | mTLS Certificate | JWT Access Token |
|---|---|---|
| **Answers** | Who are you? (Authentication) | What can you do? (Authorization) |
| **Layer** | Transport (TLS handshake, before HTTP) | Application (HTTP header: `Authorization: Bearer ...`) |
| **Carries** | Identity of the caller (CN on the cert) | Permissions/scopes (`read:movies`, `write:ratings`) |
| **Verified by** | TLS stack, before your code runs | Your API middleware, in your code |

**Three common combinations in practice:**

#### Pattern A — mTLS Only (no JWT)
```
Partner ──[TLS handshake: presents client cert]──► Your API
                                                      │
                                               Extract CN from cert
                                               → "partner-a.company.com"
                                               → Lookup permissions in DB
                                               → Allow/deny
```
- Cert = both proof of identity AND the authorization lookup key
- Simple. Works well when all partners have the same permissions or you manage authorization in a DB keyed by cert CN
- No JWT involved at all

#### Pattern B — mTLS + JWT (most common in enterprise B2B)
```
Partner ──[mTLS: cert proves who they are]──► Auth Server
                                                  │
                                             Issues JWT with scopes
                                             e.g. { sub: "partner-a", scope: "read:movies" }
                                                  │
Partner ──[mTLS + Bearer JWT]──► Your API
                                      │
                                 mTLS: is this really partner-a? ✓
                                 JWT:  are they allowed to do this? ✓
```
- mTLS handles transport-level identity (the channel is trusted)
- JWT carries fine-grained authorization (scopes, roles, expiry)
- **Why both?** mTLS is binary (pass/fail), JWT carries rich claims about _what_ the partner can do, for how long, and with what scope

#### Pattern C — mTLS-Bound JWT (Certificate-Bound Tokens, RFC 8705)
The most secure option. The JWT is cryptographically **tied** to the specific TLS certificate used to obtain it. Even if someone steals the JWT, they cannot use it without also having the matching private key.

```
Partner ──[mTLS]──► Auth Server
                        │
                    Issues JWT with cert thumbprint baked in:
                    { "cnf": { "x5t#S256": "abc123hash..." } }
                        │
Partner ──[mTLS + Bearer JWT]──► Your API
                                      │
                                 1. Validate JWT signature
                                 2. Extract cert thumbprint from JWT claim
                                 3. Compare to thumbprint of TLS cert used in this connection
                                 4. Mismatch → reject (stolen token can't be replayed)
```
This is called **Proof of Possession** — the token is only valid when presented over the exact mTLS connection it was issued for.

**Summary — when to use what:**

| Scenario | Use |
|---|---|
| Simple B2B, all partners have same access | mTLS alone |
| Partners need different scopes / permissions | mTLS + JWT |
| High-security, regulated (finance, healthcare) | mTLS-bound JWT (RFC 8705) |
| You already have OAuth infrastructure | mTLS for transport, JWT for authorization |
| No OAuth server, want minimal moving parts | mTLS alone, manage permissions in DB |

---

## Option 3 — OAuth 2.0 Client Credentials Flow (Machine-to-Machine)

### What it is
The partner authenticates to an **Authorization Server** (AS) using their client ID + secret, gets a short-lived JWT access token, then calls your API with that token.

This is the **correct OAuth flow for M2M** — no user involved, just two systems.

```
┌─────────────────────────────────────────────────────┐
│                                                      │
│  Partner A ──── POST /oauth/token ──── Auth Server   │
│                 client_id=partner-a                  │
│                 client_secret=xyz                    │
│                 grant_type=client_credentials        │
│                                                      │
│  Auth Server ─── { access_token: "eyJ..." } ────────►│
│                  expires_in: 3600                    │
│                                                      │
│  Partner A ──── GET /api/movies ──── Your API        │
│                 Authorization: Bearer eyJ...         │
│                                                      │
│  Your API validates JWT signature with AS public key │
│  → Extracts partner-a from token claims              │
│  → Allow/deny                                        │
└─────────────────────────────────────────────────────┘
```

### Why this is better than raw API keys
- Token is **short-lived** (1 hour) — stolen token expires quickly
- Token is **scoped** to specific resources (OAuth scopes: `read:movies write:ratings`)
- Your API **doesn't need to call the Auth Server on every request** — just validates the JWT signature locally (same as how your IdentityService JWT works)
- Centralised revocation at the Auth Server

### With Private Key JWT (Stronger variant)
Instead of client_secret (a shared secret — same weakness as API keys), the partner proves identity using their private key:

```
Partner signs a JWT assertion with their private.key
  → Sends assertion to Auth Server
  → AS verifies with partner's public.key (registered once)
  → Issues access token

Advantage: private key never transmitted, same as mTLS principle
```

This is called **private_key_jwt** client authentication in OAuth 2.0.

### Auth Server options
- **AWS Cognito** — managed, supports client credentials
- **Keycloak** — self-hosted, open source, full-featured
- **Auth0 / Okta** — SaaS, expensive but zero ops
- **Duende IdentityServer** — .NET native, you already have IdentityService
- **Azure AD / Entra** — if partner is also in Azure ecosystem

---

## Option 4 — VPN (Network-Level Trust)

### What it is
Instead of authenticating at the application layer, establish a **trusted private network** between you and the partner. Any traffic inside the VPN is trusted by network policy.

```
Partner's network ──── VPN Tunnel ──── Your network
(encrypted, authenticated at network level)
                            │
                    Your private API
                    (no auth headers needed —
                     network presence = trust)
```

### Types
- **Site-to-site VPN** — permanent tunnel between network A and network B (AWS VPN Gateway ↔ partner's firewall). Traffic looks like it's all internal.
- **Client VPN** — individual users connect (AWS Client VPN, OpenVPN). Not typically for M2M.
- **AWS PrivateLink** — expose your service as a VPC endpoint. Partner accesses it via their VPC without traffic leaving AWS network.

### Pros
- Application doesn't need to implement auth — it's handled at network layer
- No credentials to manage in app code
- Works with legacy systems that don't support modern auth

### Cons
- VPN = implicit trust once inside — if partner's network is compromised, attacker has direct access
- Complex to set up and maintain (firewall rules, routing)
- No fine-grained authorization — all-or-nothing network access
- Not suitable if you need per-partner audit logs at the request level

### When to use
- Legacy partner integration
- High-trust partners (subsidiaries, acquired companies)
- When modifying the partner's application is not possible
- Bulk data transfers (ETL, file drops)

---

## Option 5 — Identity Broker / Federation

### What it is
A **broker** sits between you and multiple external identity providers. You only integrate with the broker; the broker handles the diversity of partner IdPs.

```
Partner A (uses Okta)   ─┐
Partner B (uses Azure AD)─┤──► Identity Broker ──► Your API
Partner C (uses Google)  ─┘    (Keycloak, Auth0,
                                 AWS Cognito)
```

The broker:
1. Federates with each partner IdP via SAML 2.0 or OIDC
2. Normalises the identity into a standard token format (JWT)
3. Your API only sees JWTs from the broker — never deals with partner-specific protocols

### SAML 2.0 vs OIDC

| | SAML 2.0 | OIDC |
|---|---|---|
| **Format** | XML assertions | JSON / JWT |
| **Age** | 2002, enterprise legacy | 2014, modern |
| **Transport** | Browser redirects (not great for M2M) | HTTP + JSON |
| **Common users** | Banks, insurance, government | SaaS, mobile, modern web |
| **For M2M** | No (browser-based flow) | Yes (client credentials flow) |

### When to use a broker
- Many partners with different IdPs
- Partners insist on using their own SSO (SAML)
- You want a single audit log of all partner authentications
- Gradual migration: old partners use SAML, new ones use OIDC — broker handles both

---

## Comparison Table

| Method | Identity Proof | Revocation | Setup Complexity | Best For |
|---|---|---|---|---|
| **API Keys** | Shared secret | Delete key from DB | Very Low | Low-risk, quick integrations |
| **mTLS** | Asymmetric crypto (cert) | CRL / OCSP | High | High-security B2B, financial, regulated |
| **OAuth 2.0 Client Credentials** | shared secret or private key | Token expiry + revoke at AS | Medium | Modern M2M, scoped access |
| **Private Key JWT** | Asymmetric crypto | Token expiry | Medium-High | High-assurance OAuth where secret sharing is unacceptable |
| **VPN** | Network identity | Terminate tunnel | High | Legacy systems, bulk data, high-trust partners |
| **Identity Broker / SAML** | Federated IdP | At source IdP | Very High | Enterprise SSO partners, many partner orgs |

---

## How to Decide

```
Is the partner another company's automated system (M2M)? ──Yes──►
    Is this a regulated/financial/healthcare context? ──Yes──► mTLS or Private Key JWT
    Is simplicity more important than maximum security? ──Yes──► OAuth 2.0 Client Credentials
    Does the partner have existing IdP (Okta/Azure AD)? ──Yes──► Identity Broker + OIDC

Is the partner a human (SSO login)? ──Yes──►
    Is the partner using corporate identity (Azure AD, Okta)? ──Yes──► OIDC or SAML via Broker
    Do you control their IdP? ──Yes──► Standard OAuth + OIDC

Is this a legacy integration where you can't change the partner's code? ──Yes──►
    VPN or mTLS at load balancer (no app changes needed)
```

---

## Key Vocabulary

| Term | Meaning |
|---|---|
| **CA (Certificate Authority)** | Trusted issuer that signs certificates. Root of trust. |
| **Certificate** | Public key + identity info + CA signature. Like a passport. |
| **Private Key** | Secret half of a key pair. Never transmitted. Used to sign/decrypt. |
| **Public Key** | Public half. Embedded in certificate. Used to verify/encrypt. |
| **mTLS** | TLS where both client and server present certificates. |
| **CRL** | Certificate Revocation List — list of revoked certs, checked before trusting. |
| **OCSP** | Online Certificate Status Protocol — real-time cert validity check (vs CRL polling). |
| **Client Credentials** | OAuth grant type for M2M — no user involved. |
| **SAML** | XML-based federation protocol. Enterprise SSO standard. |
| **OIDC** | OpenID Connect — identity layer on top of OAuth 2.0. JSON/JWT based. |
| **Federation** | Two orgs agreeing to trust each other's identity assertions. |
| **Broker** | Intermediary that translates between different identity protocols. |
| **Bearer Token** | Token that grants access to whoever holds it (like cash — finders keepers). |
| **Proof of Possession** | Token bound to a key — only the key holder can use it (mTLS-bound token). |
