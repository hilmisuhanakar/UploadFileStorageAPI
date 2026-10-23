namespace UploadFileStorageAPI.Services.Interface
{
    public interface IStorageService
    {
        Task<string> UploadAsync(IFormFile file, string bucketName, CancellationToken cancellationToken = default);
        Task DeleteAsync(string key, string bucketName, CancellationToken cancellationToken = default);
    }
}