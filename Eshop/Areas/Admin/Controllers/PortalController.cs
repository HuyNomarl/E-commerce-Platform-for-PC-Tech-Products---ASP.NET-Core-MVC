using Eshop.Constants;
using Eshop.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.BackOfficeAccess)]
    public class PortalController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            var controller = User.GetBackOfficeLandingController();
            var action = User.GetBackOfficeLandingAction();

            if (string.Equals(controller, "Portal", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("AccessDenied", "Account", new
                {
                    area = "Admin",
                    returnUrl = $"{Request.PathBase}{Request.Path}{Request.QueryString}"
                });
            }

            return RedirectToAction(action, controller, new { area = "Admin" });
        }
    }
}
