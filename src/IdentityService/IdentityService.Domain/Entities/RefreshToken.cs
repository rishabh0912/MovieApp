using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities
{
    public class RefreshToken
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public Guid FamilyId { get; set; }  = Guid.NewGuid(); // Group tokens from the same login session
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; } = false;
        public User User { get; set; } = null!;
    }
}