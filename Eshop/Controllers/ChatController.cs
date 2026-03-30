using Eshop.Constants;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Eshop.Controllers
{
    [Authorize(Policy = PolicyNames.CustomerSelfService)]
    public class ChatController : Controller
    {
        private readonly UserManager<AppUserModel> _userManager;
        private readonly ISupportChatService _supportChatService;

        public ChatController(
            UserManager<AppUserModel> userManager,
            ISupportChatService supportChatService)
        {
            _userManager = userManager;
            _supportChatService = supportChatService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAdminInfo()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var supportUser = await _supportChatService.GetPreferredSupportUserAsync(currentUserId);

            if (supportUser == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Chua co tai khoan ho tro."
                });
            }

            return Json(new
            {
                success = true,
                adminId = supportUser.Id,
                adminName = supportUser.UserName,
                title = "Doi ngu ho tro"
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetSupportMessages(string? adminId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized();
            }

            await _supportChatService.MarkConversationReadForCustomerAsync(currentUserId);
            var messages = await _supportChatService.GetConversationForCustomerAsync(currentUserId);
            return Json(messages);
        }

        [HttpGet]
        public async Task<IActionResult> SearchProducts(string? term)
        {
            var products = await _supportChatService.SearchProductsAsync(term);
            return Json(products);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendSupportMessage([FromForm] SupportChatSendInputViewModel input)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            try
            {
                var payload = await _supportChatService.SendAsync(currentUser.Id, input.ReceiverId, input);
                return Json(payload);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
