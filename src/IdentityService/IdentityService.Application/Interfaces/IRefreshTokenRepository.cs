using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken> AddToken(RefreshToken token);
        Task<RefreshToken?> GetTokenByHash(string tokenHash);
        Task RevokeToken(Guid tokenId);

//       Not needed as of now, can be added later if needed

//        Task RevokeTokensByFamilyId(Guid familyId);
//        Task RevokeAllByUserId(Guid userId);
//        Task DeleteToken(Guid tokenId);
    }
}