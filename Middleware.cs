using System.Security.Cryptography;
using System.Text;

namespace UploadFileStorageAPI.Middleware
{
    public class Middleware
    {
        private readonly RequestDelegate _next;

        public Middleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IConfiguration config)
        {
            var requestPath = context.Request.Path;
            if (requestPath.StartsWithSegments("/health"))
            {
                await _next(context);
                return;
            }

            var expected = config["ApiKey"];

            if (!context.Request.Headers.TryGetValue("X-API-KEY", out var provided)
                || string.IsNullOrEmpty(expected)
                || !CryptographicOperations.FixedTimeEquals(
                       Encoding.UTF8.GetBytes(provided.ToString()),
                       Encoding.UTF8.GetBytes(expected)))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("unauthorized");
                return;
            }

            await _next(context);
        }
    }
}