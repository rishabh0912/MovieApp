using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityService.Application.DTOs;

namespace IdentityService.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> Register(string username, string password);
        Task<AuthResponse> Login(string username, string password);
        Task<AuthResponse> RefreshToken(string refreshToken);
    }
}