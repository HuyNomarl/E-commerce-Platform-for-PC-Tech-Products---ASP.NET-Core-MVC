using System.ComponentModel.DataAnnotations;

namespace Eshop.Repository.Validation
{
    public class FileExtensionAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is IFormFile file)
            {
                var extextension = Path.GetExtension(file.FileName);
                string[] extensions = { ".jpg", ".jpeg", ".png", ".gif" };

                bool result = extensions.Any(e => extensions.EndsWith(e));

                if (!result)
                {
                    return new ValidationResult("Invalid file extension. Only .jpg, .jpeg, .png, .gif are allowed.");
                }
            }
            return ValidationResult.Success;
        }
    }
}
