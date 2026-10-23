using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using UploadFileStorageAPI.Services.Interface;

namespace UploadFileStorageAPI.Endpoints
{
    public static class FileEndpoints
    {
        private class UploadRequest
        {
            public IFormFile File { get; set; } = null!;
            public string BucketName { get; set; } = string.Empty;
        }

        private class DeleteRequest
        {
            public string BucketName { get; set; } = string.Empty;
            public string Key { get; set; } = string.Empty;
        }
        public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/files").WithTags("Files");

            group.MapPost("/", Upload)
                 .DisableAntiforgery()
                 .Accepts<UploadRequest>("multipart/form-data")
                 .Produces(StatusCodes.Status201Created)
                 .Produces(StatusCodes.Status400BadRequest)
                 .WithName("UploadFile")
                 .RequireRateLimiting("upload");

            group.MapDelete("/", Delete)
                 .Accepts<DeleteRequest>("application/json")
                 .WithName("DeleteFile");

            return app;
        }

        private static async Task<IResult> Upload(IFormFile file, [FromForm] string bucketName, IStorageService storage, CancellationToken ct)
        {
            try
            {
                var key = await storage.UploadAsync(file, bucketName, ct);
                return Results.Created($"/files/{key}", new { key });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }

        private static async Task<IResult> Delete([FromBody] DeleteRequest request, IStorageService storage, CancellationToken ct)
        {
            try
            {
                await storage.DeleteAsync(request.Key, request.BucketName, ct);
                return Results.NoContent();
            }
            catch (AmazonS3Exception ex)
            {
                return Results.BadRequest(new { error = ex.ErrorCode });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Bir hata oluştu" });
            }
        }
    }
}