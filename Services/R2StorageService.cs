using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UploadFileStorageAPI.Services.Interface;
using UploadFileStorageAPI.ConfigOptions;
using SkiaSharp;
using UploadFileStorageAPI.Const;


namespace UploadFileStorageAPI.Services
{
    public class R2StorageService : IStorageService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".txt", ".zip"
        };
        private static readonly HashSet<string> ConvertibleImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png"
        };

        private List<string> AllowedBuckets { get; set; } = [];

        private const int MaxImageDimension = 2560;      // en/boy üst sınırı (resize için)
        private const long MaxImagePixels = 8_000_000;  // decompression bomb koruması
        private const int WebpQuality = 80;

        private readonly IAmazonS3 _s3;
        private readonly R2Options _r2Options;
        private readonly ILogger<R2StorageService> _logger;

        public R2StorageService(IAmazonS3 s3, IOptions<R2Options> r2Options, ILogger<R2StorageService> logger)
        {
            _s3 = s3;
            _r2Options = r2Options.Value;
            _logger = logger;
        }

        public async Task<string> UploadAsync(IFormFile file, string bucketName, CancellationToken cancellationToken = default)
        {
            bucketName = NormalizeBucket(bucketName);
            if (file is null || file.Length == 0)
                throw new ArgumentException(ErrorMessage.FileEmpty);
            if (file.Length > _r2Options.MaxFileSizeBytes)
                throw new ArgumentException(ErrorMessage.FileTooLarge);
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                throw new ArgumentException(ErrorMessage.AllowedExtensions);

            await using var input = file.OpenReadStream();

            Stream uploadStream;
            string key;
            string contentType;

            if (ConvertibleImageExtensions.Contains(extension))
            {
                var webp = await Task.Run(() => ConvertToWebp(input, extension, file.Length), cancellationToken);
                if (webp is not null)
                {
                    uploadStream = webp;
                    key = $"{Guid.NewGuid():N}.webp";
                    contentType = "image/webp";
                }
                else
                {
                    if (input.CanSeek)
                        input.Position = 0;
                    else
                        throw new InvalidOperationException(ErrorMessage.FileCannotBeRead);

                    uploadStream = input;
                    key = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                    contentType = file.ContentType;
                }
            }
            else
            {
                uploadStream = input;
                key = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                contentType = file.ContentType;
            }
            try
            {
                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = key,
                    InputStream = uploadStream,
                    ContentType = contentType,
                    DisablePayloadSigning = true
                };
                await _s3.PutObjectAsync(request, cancellationToken);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode is "NoSuchBucket" or "AccessDenied")
            {
                _logger.LogError(ex, "Bucket bulunamadı veya erişim reddedildi: {BucketName}", bucketName + " - " + ex.ErrorCode);
                throw new ArgumentException(ErrorMessage.NoSuchBucket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "R2'ye yükleme hatası: {Key}", key);
                throw;
            }
            finally
            {
                if (!ReferenceEquals(uploadStream, input))
                    await uploadStream.DisposeAsync();
            }
            _logger.LogInformation("R2'ye yüklendi: {Key} ({ContentType})", key, contentType);
            return key;
        }

        private static MemoryStream? ConvertToWebp(Stream input, string extension, long originalSize)
        {
            // SKData.Create stream'i belleğe kopyalar ve input'u dispose etmez
            using var data = SKData.Create(input);
            using var codec = data is null ? null : SKCodec.Create(data);

            if (codec is null)
                throw new ArgumentException(ErrorMessage.InvalidImageFile);

            if ((long)codec.Info.Width * codec.Info.Height > MaxImagePixels)
                throw new ArgumentException(ErrorMessage.ImageTooLarge);

            var origin = codec.EncodedOrigin;

            using var decoded = SKBitmap.Decode(codec);
            if (decoded is null)
                throw new ArgumentException(ErrorMessage.ImageCannotBeDecoded);

            using var oriented = ApplyOrientation(decoded, origin);

            // Gerekirse küçült
            SKBitmap? resized = null;
            var working = oriented;
            if (oriented.Width > MaxImageDimension || oriented.Height > MaxImageDimension)
            {
                var scale = (float)MaxImageDimension / Math.Max(oriented.Width, oriented.Height);
                var info = new SKImageInfo(
                    Math.Max(1, (int)(oriented.Width * scale)),
                    Math.Max(1, (int)(oriented.Height * scale)),
                    oriented.ColorType, oriented.AlphaType);

                resized = oriented.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
                working = resized ?? oriented;
            }

            try
            {
                var isJpeg = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                          || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

                // JPEG: lossy 80. PNG/diğer: lossless (100 = en iyi sıkıştırma çabası)
                var options = isJpeg
                    ? new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, WebpQuality)
                    : new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, WebpQuality);

                using var pixmap = working.PeekPixels();
                using var encoded = pixmap.Encode(options);

                if (encoded is null || encoded.Size >= originalSize)
                    return null; // WebP daha büyük, orijinal yüklensin

                var output = new MemoryStream((int)encoded.Size);
                encoded.SaveTo(output);
                output.Position = 0;
                return output;
            }
            finally
            {
                resized?.Dispose();
            }
        }

        // EXIF yönünü uygular. Skia yeniden kodlarken EXIF/GPS bilgisini zaten atar.
        private static SKBitmap ApplyOrientation(SKBitmap src, SKEncodedOrigin origin)
        {
            if (origin is SKEncodedOrigin.TopLeft)
                return src;

            float w = src.Width, h = src.Height;

            var matrix = origin switch
            {
                SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
                SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
                SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
                SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
                SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
                _ => SKMatrix.Identity
            };

            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                              or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

            var dst = new SKBitmap(swap ? src.Height : src.Width, swap ? src.Width : src.Height,
                                   src.ColorType, src.AlphaType);

            using var canvas = new SKCanvas(dst);
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(src, 0, 0);
            return dst;
        }

        public async Task DeleteAsync(string key, string bucketName, CancellationToken cancellationToken = default)
        {
            bucketName = NormalizeBucket(bucketName);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(ErrorMessage.KeyCannotBeEmpty);

            try
            {
                await _s3.DeleteObjectAsync(bucketName, key, cancellationToken);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode is "NoSuchKey" or "AccessDenied")
            {
                _logger.LogError(ex, "Key bulunamadı veya erişim reddedildi: {Key}", key + " - " + ex.ErrorCode);
                throw new ArgumentException(ErrorMessage.NoSuchKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "R2'den silme hatası: {Key}", key);
                throw;
            }

            _logger.LogInformation("R2'den silindi: {Key}", key);
        }

        private string NormalizeBucket(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException(ErrorMessage.BucketNameCannotBeEmpty);
            bucketName = bucketName.Trim();
            return bucketName;
        }
    }
}