using Eshop.Constants;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.SupportManagement)]
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

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetUsersChatted()
        {
            var users = await _supportChatService.GetSupportConversationsAsync();
            return Json(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest();
            }

            await _supportChatService.MarkConversationReadForSupportAsync(userId);
            var messages = await _supportChatService.GetConversationForSupportAsync(userId);
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
        public async Task<IActionResult> SendMessage([FromForm] SupportChatSendInputViewModel input)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(input.ReceiverId))
            {
                return BadRequest(new { message = "Chua chon khach hang de chat." });
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
