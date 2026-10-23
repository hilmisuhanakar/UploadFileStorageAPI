using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using UploadFileStorageAPI.ConfigOptions;
using UploadFileStorageAPI.Services;
using UploadFileStorageAPI.Services.Interface;

namespace UploadFileStorageAPI
{
    public static class CustomServiceCollection
    {
        public static void AddR2Storage(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<R2Options>()
                    .BindConfiguration("R2")
                    .Validate(v => !string.IsNullOrWhiteSpace(v.AccountId)
                    && !string.IsNullOrWhiteSpace(v.AccessKey)
                    && !string.IsNullOrWhiteSpace(v.SecretKey),
                    "R2 configuration is required (AccountId, AccessKey, SecretKey)")
                    .ValidateOnStart();
            var a = configuration.GetSection("R2").Get<R2Options>();
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var opt = sp.GetRequiredService<IOptions<R2Options>>().Value;
                var config = new AmazonS3Config
                {
                    ServiceURL = $"https://{opt.AccountId}.r2.cloudflarestorage.com",
                    AuthenticationRegion = "auto",
                    ForcePathStyle = true,
                    // R2, yeni SDK'nın varsayılan checksum başlıklarını desteklemiyor
                    RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
                };
                return new AmazonS3Client(new BasicAWSCredentials(opt.AccessKey, opt.SecretKey), config);
            });

            services.AddScoped<IStorageService, R2StorageService>();

            var maxBody = configuration.GetValue<long?>("R2:MaxFileSizeBytes") ?? 5 * 1024 * 1024;
            services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = maxBody + 1024 * 1024);
            services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxBody + 1024 * 1024);
        }
    }
}