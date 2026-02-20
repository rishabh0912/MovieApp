using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities
{
    public class User
    {
        [Key]
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? UserName { get; set; }
        public string? PasswordHash { get; set; }
        public string Roles { get; set; } = "User"; // Default role
        public string Status { get; set; } = "Active"; // Default status
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    }

}