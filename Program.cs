using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UploadFileStorageAPI;
using UploadFileStorageAPI.Endpoints;
using UploadFileStorageAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddR2Storage(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "10";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Sunucu meşgul, biraz sonra tekrar dene." }, token);
    };

    // Aynı anda en fazla 1 dönüştürme (0.1 CPU / 512 MB)
    options.AddConcurrencyLimiter("upload", o =>
    {
        o.PermitLimit = 1;
        o.QueueLimit = 2;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Dakikada en fazla 20 upload (suistimal / döngü)
    options.AddFixedWindowLimiter("upload-rate", o =>
    {
        o.PermitLimit = 20;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}


app.UseMiddleware<Middleware>();

app.MapGet("/health", () =>
{
    var result = new { isSuccess = true, status = StatusCodes.Status200OK, timestamp = DateTime.UtcNow, version = "1.0.0" };
    return Results.Ok(result);
})
.WithName("HealthCheck");

app.UseRateLimiter();

app.MapFileEndpoints();

app.Run();
