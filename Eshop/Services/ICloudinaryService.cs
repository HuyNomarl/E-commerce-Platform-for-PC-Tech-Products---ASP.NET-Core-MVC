using Eshop.Models.ViewModels;
using Microsoft.AspNetCore.Http;

namespace Eshop.Services
{
    public interface ICloudinaryService
    {
        Task<CloudinaryUploadResultViewModel> UploadImageAsync(IFormFile file, string folder);
        Task<CloudinaryUploadResultViewModel> UploadRawFileAsync(IFormFile file, string folder);
        Task DeleteAsync(string publicId, string resourceType = "image");
    }
}