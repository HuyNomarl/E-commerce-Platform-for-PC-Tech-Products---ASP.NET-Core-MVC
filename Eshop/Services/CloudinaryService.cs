using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Eshop.Models.ViewModels;
using Microsoft.AspNetCore.Http;

namespace Eshop.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        public async Task<CloudinaryUploadResultViewModel> UploadImageAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                throw new Exception("File ảnh không hợp lệ.");

            await using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                throw new Exception(result.Error.Message);

            return new CloudinaryUploadResultViewModel
            {
                PublicId = result.PublicId,
                Url = result.SecureUrl?.ToString() ?? string.Empty,
                ResourceType = result.ResourceType,
                OriginalFileName = file.FileName
            };
        }

        public async Task<CloudinaryUploadResultViewModel> UploadVideoAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                throw new Exception("File video không hợp lệ.");

            await using var stream = file.OpenReadStream();

            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                throw new Exception(result.Error.Message);

            return new CloudinaryUploadResultViewModel
            {
                PublicId = result.PublicId,
                Url = result.SecureUrl?.ToString() ?? string.Empty,
                ResourceType = result.ResourceType,
                OriginalFileName = file.FileName
            };
        }

        public async Task<CloudinaryUploadResultViewModel> UploadRawFileAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                throw new Exception("File kỹ thuật không hợp lệ.");

            await using var stream = file.OpenReadStream();

            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                throw new Exception(result.Error.Message);

            return new CloudinaryUploadResultViewModel
            {
                PublicId = result.PublicId,
                Url = result.SecureUrl?.ToString() ?? string.Empty,
                ResourceType = result.ResourceType,
                OriginalFileName = file.FileName
            };
        }

        public async Task DeleteAsync(string publicId, string resourceType = "image")
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return;

            var deleteParams = new DeletionParams(publicId)
            {
                ResourceType = resourceType?.ToLower() switch
                {
                    "video" => ResourceType.Video,
                    "raw" => ResourceType.Raw,
                    _ => ResourceType.Image
                }
            };

            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Error != null)
                throw new Exception(result.Error.Message);
        }
    }
}
