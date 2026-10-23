using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UploadFileStorageAPI.Const
{
    public static class ErrorMessage
    {
        public static readonly string NoSuchBucket = "NoSuchBucket";
        public static readonly string AccessDenied = "AccessDenied";
        public static readonly string NoSuchKey = "NoSuchKey";
        public static readonly string InternalError = "InternalError";
        public static readonly string BadRequest = "BadRequest";
        public static readonly string Unauthorized = "Unauthorized";
        public static readonly string Forbidden = "Forbidden";
        public static readonly string NotFound = "NotFound";
        public static readonly string MethodNotAllowed = "MethodNotAllowed";
        public static readonly string AllowedExtensions = "AllowedExtensions";
        public static readonly string FileTooLarge = "FileTooLarge";
        public static readonly string FileEmpty = "FileEmpty";
        public static readonly string FileCannotBeRead = "FileCannotBeRead";
        public static readonly string InvalidImageFile = "InvalidImageFile";
        public static readonly string ImageTooLarge = "ImageTooLarge";
        public static readonly string ImageCannotBeDecoded = "ImageCannotBeDecoded";
        public static readonly string KeyCannotBeEmpty = "KeyCannotBeEmpty";
        public static readonly string BucketNameCannotBeEmpty = "BucketNameCannotBeEmpty";
    }
}