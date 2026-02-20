using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Api.DTOs
{
    public class RefreshRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}