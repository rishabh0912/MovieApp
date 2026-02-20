using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityService.Api.DTOs;

namespace IdentityService.Api.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _logger = logger;
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, ex.Message);
                await WriteResponse(context, StatusCodes.Status409Conflict, ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(ex, ex.Message);
                await WriteResponse(context, StatusCodes.Status404NotFound, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, ex.Message);
                await WriteResponse(context, StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred.");
                await WriteResponse(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        private static async Task WriteResponse(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorResponse
            {
                StatusCode = statusCode.ToString(),
                Message = message
            });
        }
        
    }
}