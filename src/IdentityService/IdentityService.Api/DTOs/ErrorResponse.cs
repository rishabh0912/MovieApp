using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Api.DTOs
{
    public class ErrorResponse
    {
        public string StatusCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}