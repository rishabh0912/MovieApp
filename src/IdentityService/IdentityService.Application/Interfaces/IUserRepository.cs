using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User> AddUser(User user);
        Task<User?> GetUserByUsername(string username);

//       Not needed as of now, can be added later if needed

//        Task<User?> GetUserById(Guid userId);
//        Task UpdateUser(User user);
//        Task DeleteUser(Guid userId);
    }
}