using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = Eshop.Constants.PolicyNames.AdminOnly)]
    public class RecommendationController : Controller
    {
        private readonly RecommendationTrainingService _trainingService;

        public RecommendationController(RecommendationTrainingService trainingService)
        {
            _trainingService = trainingService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Train()
        {
            try
            {
                var path = await _trainingService.TrainAsync();
                TempData["success"] = $"Train recommendation thành công: {path}";
            }
            catch (Exception ex)
            {
                TempData["error"] = $"Train recommendation thất bại: {ex.Message}";
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
