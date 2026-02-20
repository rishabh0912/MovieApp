using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repository
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly AppDbContext _context;

        public RefreshTokenRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<RefreshToken> AddToken(RefreshToken token)
        {
            _context.RefreshTokens.Add(token);
            await _context.SaveChangesAsync();
            return token;
        }

        public async Task<RefreshToken?> GetTokenByHash(string tokenHash)
        {
            return await _context.RefreshTokens
                .Where(u => u.TokenHash == tokenHash)
                .FirstOrDefaultAsync();            
        }

        public async Task RevokeToken(Guid tokenId)
        {
            var token = await _context.RefreshTokens
                .FindAsync(tokenId);
            if (token != null)
            {
                token.IsRevoked = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}