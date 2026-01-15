using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ContactController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ContactController(DataContext dataContext, IWebHostEnvironment webHostEnvironment)
        {
            _dataContext = dataContext;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            var contact = _dataContext.Contact.ToList();
            return View(contact);
        }

        public async Task<IActionResult> Edit()
        {
            var contact = await _dataContext.Contact.FirstOrDefaultAsync();
            if (contact == null)
            {
                contact = new ContactModel();
            }
            return View(contact);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ContactModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["error"] = "Có lỗi xảy ra, vui lòng kiểm tra lại thông tin!";
                return View(model);
            }

            var existing = await _dataContext.Contact.FirstOrDefaultAsync();

            // Nếu chưa có thì tạo mới
            if (existing == null)
            {
                // Upload logo nếu có
                if (model.LogoImgFile != null && model.LogoImgFile.Length > 0)
                {
                    model.LogoImg = await SaveLogoAsync(model.LogoImgFile);
                }
                else
                {
                    model.LogoImg = model.LogoImg ?? "noname.jpg";
                }

                _dataContext.Contact.Add(model);
                await _dataContext.SaveChangesAsync();

                TempData["success"] = "Tạo thông tin liên hệ thành công!";
                return RedirectToAction(nameof(Index));
            }

            existing.Map = model.Map;
            existing.Phone = model.Phone;
            existing.Email = model.Email;
            existing.Description = model.Description;

            // Upload logo mới nếu có
            if (model.LogoImgFile != null && model.LogoImgFile.Length > 0)
            {
                // xóa ảnh cũ nếu có
                if (!string.IsNullOrEmpty(existing.LogoImg) && existing.LogoImg != "noname.jpg")
                {
                    var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, "media", "contact", existing.LogoImg);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                existing.LogoImg = await SaveLogoAsync(model.LogoImgFile);
            }

            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Cập nhật thông tin liên hệ thành công!";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveLogoAsync(IFormFile file)
        {
            var uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media", "contact");
            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);

            using var fs = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(fs);

            return fileName;
        }
    }
}
