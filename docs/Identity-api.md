1. Identity Domain - User entity, RefreshToken entity
2. Identity Application - IUserRepository, ITokenService, LoginCommand
3. Identity Infrastructure - BCrypt password hashing, EF Core DbContext, JWT Generation
4. Identity API - POST /auth/login, POST /auth/refresh, POST /auth/register


1. Domain
    - User entity (userId, userName, passwordHash, roles, status, createdAt)
    - RefreshTokens entity (id, userId, tokenHash, familyId, issuedAt, expiresAt, isRevoked)

2. Application
    - IUserRepository (AddUser, GetByUsername, getById, DeleteUser, UpdateUser)
    - IRefreshTokenRepository (Add, GetByTokenHash, Revoke, RevokeFamily)
    - ITokenService (GenerateAccessToken, GenerateRefreshToken)
    - LoginCommand + LoginCommandHandler
    - RefreshTokenCommand + RefreshTokenCommandHandler
    - RegisterCommand + RegisterCommandHandler

3. Infrastructure
    - UserRepository (implements IUserRepository)
    - RefreshTokenRepository (implements IRefreshTokenRepository)
    - TokenService (implements ITokenService - signs JWT with secret key)
    - AppDbContext (EF Core)

4. API
    - AuthController (POST /auth/register, /auth/login, /auth/refresh)