using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityService.Application.DTOs;
using IdentityService.Application.Interfaces;
using BCrypt.Net;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace IdentityService.Infrastructure.Services
{
    public class AuthService : IAuthService
    {

        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public AuthService(IUserRepository userRepository, ITokenService tokenService, IRefreshTokenRepository refreshTokenRepository)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _refreshTokenRepository = refreshTokenRepository;
        }

        public async Task<AuthResponse> Login(string username, string password)
        {
            var user = await _userRepository.GetUserByUsername(username);
            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid username or password");
            }
            var passwordHash = user?.PasswordHash;

            if (passwordHash != null)
            {
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, passwordHash);
                if(!isPasswordValid)
                {
                    throw new UnauthorizedAccessException("Invalid username or password");
                }
                if(isPasswordValid)
                {
                    var token = _tokenService.GenerateToken(user);
                    var refreshTokenString = _tokenService.GenerateRefreshToken();

                    var refreshToken = new RefreshToken
                    {
                        UserId = user.UserId,
                        TokenHash = HashToken(refreshTokenString),
                        ExpiresAt = DateTime.UtcNow.AddDays(7) // Example expiration
                    };

                    await _refreshTokenRepository.AddToken(refreshToken);

                    return new AuthResponse
                    {
                        AccessToken = token,
                        RefreshToken = refreshTokenString
                    };
                }
            }
            throw new UnauthorizedAccessException("Invalid username or password");            
        }
        public async Task<AuthResponse> RefreshToken(string refreshToken)
        {
            var tokenHash = HashToken(refreshToken);
            var storedToken = await _refreshTokenRepository.GetTokenByHash(tokenHash);
            
            if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Invalid refresh token");
            }

            await _refreshTokenRepository.RevokeToken(storedToken.Id); // Revoke the old token
            
            var user = await _userRepository.GetUserByUsername(storedToken.User.UserName);
            var token = _tokenService.GenerateToken(user);
            
            var refreshTokenString = _tokenService.GenerateRefreshToken();
            var newToken = new RefreshToken
            {
                UserId = user.UserId,
                TokenHash = HashToken(refreshTokenString),
                ExpiresAt = DateTime.UtcNow.AddDays(7) // Example expiration
            };

            await _refreshTokenRepository.AddToken(newToken);
            return new AuthResponse
            {
                AccessToken = token,
                RefreshToken = refreshTokenString
            };  
        }

        public async Task<AuthResponse> Register(string username, string password)
        {
            var checkUser = await _userRepository.GetUserByUsername(username);
            if(checkUser != null)
            {
                throw new InvalidOperationException("Username already exists");                
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                UserName = username,
                PasswordHash = passwordHash
            };

            var newUser = await _userRepository.AddUser(user);
            var token = _tokenService.GenerateToken(newUser);
            var refreshTokenString = _tokenService.GenerateRefreshToken();
            var refreshToken = new RefreshToken
            {
                UserId = newUser.UserId,
                TokenHash = HashToken(refreshTokenString),
                ExpiresAt = DateTime.UtcNow.AddDays(7) // Example expiration
            };

            await _refreshTokenRepository.AddToken(refreshToken);
            return new AuthResponse
            {
                AccessToken = token,
                RefreshToken = refreshTokenString
            };

        }

        private static string HashToken(string token)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData
                (System.Text.Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }
    }
}