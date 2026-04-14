using Eshop.Models;
using Eshop.Repository;
using Eshop.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.CatalogManagement)]
    public class SliderController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IWebHostEnvironment _env;
        private readonly HybridCache _cache;

        public SliderController(DataContext dataContext, IWebHostEnvironment env, HybridCache cache)
        {
            _dataContext = dataContext;
            _env = env;
            _cache = cache;
        }

        public async Task<IActionResult> Index()
        {
            var sliders = await _dataContext.Sliders
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return View(sliders);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SliderModel model)
        {
            // Nếu bạn đang gắn [Required] cho ImageUpload trong model thì Create ok.
            if (!ModelState.IsValid)
                return View(model);

            if (model.ImageUpload == null || model.ImageUpload.Length == 0)
            {
                ModelState.AddModelError(nameof(SliderModel.ImageUpload), "Vui lòng chọn ảnh.");
                return View(model);
            }

            var uploadDir = Path.Combine(_env.WebRootPath, "media", "sliders");
            Directory.CreateDirectory(uploadDir);

            var ext = Path.GetExtension(model.ImageUpload.FileName);
            var imageName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadDir, imageName);

            await using (var fs = new FileStream(filePath, FileMode.Create))
            {
                await model.ImageUpload.CopyToAsync(fs);
            }

            model.Image = imageName;

            _dataContext.Sliders.Add(model);
            await _dataContext.SaveChangesAsync();
            await RemoveHomeSliderCacheAsync();

            TempData["success"] = "Thêm slider thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var slider = await _dataContext.Sliders.FindAsync(id);
            if (slider == null) return NotFound();

            return View(slider);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SliderModel model)
        {
            if (id != model.Id) return NotFound();

            // Nếu SliderModel có [Required] cho ImageUpload thì khi Edit (không chọn ảnh)
            // sẽ bị invalid => remove validation cho ImageUpload ở Edit
            ModelState.Remove(nameof(SliderModel.ImageUpload));

            if (!ModelState.IsValid)
                return View(model);

            var slider = await _dataContext.Sliders.FindAsync(id);
            if (slider == null) return NotFound();

            slider.Name = model.Name;
            slider.Description = model.Description;
            slider.Status = model.Status;

            // Upload ảnh mới (optional)
            if (model.ImageUpload != null && model.ImageUpload.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "media", "sliders");
                Directory.CreateDirectory(uploadDir);

                // Xóa ảnh cũ (nếu có)
                if (!string.IsNullOrWhiteSpace(slider.Image) && slider.Image != "noname.jpg")
                {
                    var oldPath = Path.Combine(uploadDir, slider.Image);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                var ext = Path.GetExtension(model.ImageUpload.FileName);
                var newImageName = $"{Guid.NewGuid()}{ext}";
                var newPath = Path.Combine(uploadDir, newImageName);

                await using (var fs = new FileStream(newPath, FileMode.Create))
                {
                    await model.ImageUpload.CopyToAsync(fs);
                }

                slider.Image = newImageName;
            }

            await _dataContext.SaveChangesAsync();
            await RemoveHomeSliderCacheAsync();

            TempData["success"] = "Cập nhật slider thành công!";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var slider = await _dataContext.Sliders.FindAsync(id);
            if (slider == null) return NotFound();

            var uploadDir = Path.Combine(_env.WebRootPath, "media", "sliders");

            if (!string.IsNullOrWhiteSpace(slider.Image) &&
                !string.Equals(slider.Image, "noname.jpg", StringComparison.OrdinalIgnoreCase))
            {
                var filePath = Path.Combine(uploadDir, slider.Image);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            _dataContext.Sliders.Remove(slider);
            await _dataContext.SaveChangesAsync();
            await RemoveHomeSliderCacheAsync();

            TempData["success"] = "Xóa slider thành công!";
            return RedirectToAction(nameof(Index));
        }

        private ValueTask RemoveHomeSliderCacheAsync(CancellationToken cancellationToken = default)
            => _cache.RemoveAsync(CacheKeys.HomeSliders, cancellationToken);

    }
}
