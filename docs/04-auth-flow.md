# Auth Flow (Interview Prep)

## 1) Core Decisions

- **User credentials storage:** Store users in the **Identity DB**.
- **Password handling:** Do **not** encrypt passwords. Store a **salted one-way hash** (e.g., Argon2/bcrypt).
- **JWT in login request:** No JWT is sent during login; login uses `username/password` only.
- **JWT usage:** JWT is returned after successful login and then sent in API calls as `Bearer <token>`.
- **Token model:** Use short-lived **access token** + longer-lived **refresh token**.

## 2) End-to-End Login + API Call Flow

```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant C as Client App
    participant G as API Gateway
    participant I as Identity Service
    participant DB as Identity DB
    participant M as Movie Service

    U->>C: Enter username + password
    C->>I: POST /auth/login
    I->>DB: Load user by username
    DB-->>I: PasswordHash + Salt + Roles + Status
    I->>I: Verify password hash
    alt Valid credentials
        I->>I: Create Access Token + Refresh Token
        I->>DB: Store refresh token metadata (hashed)
        I-->>C: access_token, refresh_token, expires_in
    else Invalid credentials
        I-->>C: 401 Unauthorized
    end

    C->>G: API call with Bearer access token
    G->>G: Validate JWT signature, exp, issuer, audience
    G->>M: Forward request with user claims
    M-->>G: Business response
    G-->>C: API response

    C->>I: POST /auth/refresh with refresh token
    I->>DB: Validate refresh token family + revocation
    I-->>C: New access token (+ rotated refresh token)
```

## 3) Custom JWT Design (Now)

At successful login, Identity Service:
1. Validates password hash from DB.
2. Builds claims (minimal): `sub` (userId), `roles`, `iss`, `aud`, `iat`, `exp`, `jti`.
3. Signs JWT with configured signing key.
4. Returns access token + refresh token.

## 4) AWS Integration Path (Later)

```mermaid
flowchart LR
    A[Client] --> B[API Gateway Layer]

    subgraph Now[Phase 1-4: Custom Auth]
      B --> C[Identity Service]
      C --> D[(Identity DB)]
      C --> E[Issue JWT]
      B --> F[Movie/Rating Services validate JWT]
    end

    subgraph Later[Phase 6: AWS]
      G[Secrets Manager or KMS key] --> C
      H[CloudWatch Logs + Tracing] --> B
      H --> C
      H --> F
      I[ALB or API Gateway on AWS] --> B
      J[Option: Cognito] -. replace token issuer .-> C
    end
```

## 5) Security Baseline

- Access token expiry: ~10–15 minutes.
- Refresh token expiry: days/weeks with rotation + revocation support.
- Store refresh tokens hashed (or strong token fingerprint metadata), not plaintext.
- Validate `iss`, `aud`, signature, expiry on every protected request.
- Keep claims small and stable to reduce coupling.

## 6) Interview-Friendly Summary (2 minutes)

“We keep auth in a dedicated Identity Service. User credentials are stored in Identity DB with salted one-way password hashes. Login accepts username/password only; on success, Identity issues a short-lived JWT access token and a longer-lived refresh token. The client sends the access token in Bearer headers for API calls. Gateway/services validate signature and claims (`iss`, `aud`, `exp`, roles). Refresh tokens are rotated and revocable for logout/session control. Later on AWS, we externalize secrets using Secrets Manager/KMS, add tracing/logging, and can optionally migrate token issuance to Cognito without changing downstream service contracts.”
