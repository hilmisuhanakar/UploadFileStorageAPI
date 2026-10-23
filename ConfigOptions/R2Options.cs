using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UploadFileStorageAPI.ConfigOptions
{
    public class R2Options
    {
        public const string SectionName = "R2";
        public string AccountId { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;

        // Opsiyonel: bucket'a bağlı public domain
        public string? PublicBaseUrl { get; set; }
        public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024; // 10 MB
    }
}