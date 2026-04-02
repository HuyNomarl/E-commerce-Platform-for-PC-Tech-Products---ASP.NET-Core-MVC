using Eshop.Areas.Admin.Repository;
using Eshop.Constants;
using Eshop.Helpers;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;

namespace Eshop.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUserModel> _userManager;
        private readonly SignInManager<AppUserModel> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly DataContext _dataContext;
        private readonly IEmailSender _emailSender;
        private readonly IInventoryService _inventoryService;
        private readonly IOrderStateService _orderStateService;
        private readonly ICartService _cartService;

        public AccountController(
                UserManager<AppUserModel> userManager,
                SignInManager<AppUserModel> signInManager,
                RoleManager<IdentityRole> roleManager,
                IEmailSender emailSender,
                DataContext context,
                IInventoryService inventoryService,
                IOrderStateService orderStateService,
                ICartService cartService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _dataContext = context;
            _inventoryService = inventoryService;
            _orderStateService = orderStateService;
            _cartService = cartService;
        }



        public IActionResult Index() => RedirectToAction(nameof(Login));

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel
            {
                ReturnURL = returnUrl
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.UserName,
                model.Password,
                isPersistent: false,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var signedInUser = await ResolveUserByLoginIdentifierAsync(model.UserName);
                return await RedirectAfterSuccessfulSignInAsync(signedInUser, model.ReturnURL);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Đăng nhập không thành công!");
            return View(model);
        }

        // Alias để layout đang gọi asp-action="Register" vẫn chạy được
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            return RedirectToAction(nameof(Create));
        }

        // Giữ lại để không phải đổi view Create.cshtml hiện tại
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserModel user)
        {
            if (!ModelState.IsValid)
            {
                return View(user);
            }

            var existingEmail = await _userManager.FindByEmailAsync(user.Email);
            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "Email đã tồn tại.");
                return View(user);
            }

            var existingUserName = await _userManager.FindByNameAsync(user.UserName);
            if (existingUserName != null)
            {
                ModelState.AddModelError("UserName", "Tên tài khoản đã tồn tại.");
                return View(user);
            }

            var newUser = new AppUserModel
            {
                UserName = user.UserName,
                Email = user.Email,
                Address = user.Address
            };

            var result = await _userManager.CreateAsync(newUser, user.Password);

            if (result.Succeeded)
            {
                var ensureRoleResult = await EnsureRoleAssignmentAsync(newUser, RoleNames.Customer);
                if (!ensureRoleResult.Succeeded)
                {
                    foreach (var error in ensureRoleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    await _userManager.DeleteAsync(newUser);
                    return View(user);
                }

                TempData["success"] = "Tài khoản đã được tạo thành công!";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(user);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgetPass()
        {
            return View(new ForgotPasswordRequestViewModel());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMailForgotPass(ForgotPasswordRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(ForgetPass), model);
            }

            var checkMail = await _userManager.FindByEmailAsync(model.Email);
            if (checkMail != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(checkMail);
                var resetLink = $"{Request.Scheme}://{Request.Host}/Account/NewPass?email={WebUtility.UrlEncode(checkMail.Email)}&token={WebUtility.UrlEncode(token)}";
                var receiver = checkMail.Email!;
                var subject = "Đổi mật khẩu tài khoản " + checkMail.Email;
                var message = "Bấm vào link để đổi mật khẩu: "
                    + $"<a href='{resetLink}'>Click here</a>";

                await _emailSender.SendEmailAsync(receiver, subject, message);
            }

            TempData["success"] = "Nếu email tồn tại trong hệ thống, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu.";
            return RedirectToAction(nameof(ForgetPass));
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> NewPass(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["error"] = "Liên kết không hợp lệ.";
                return RedirectToAction(nameof(ForgetPass));
            }

            var decodedEmail = WebUtility.UrlDecode(email);
            var decodedToken = WebUtility.UrlDecode(token);
            var checkUser = await _userManager.FindByEmailAsync(decodedEmail);
            if (checkUser == null)
            {
                TempData["error"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(ForgetPass));
            }

            return View(new ResetPasswordViewModel
            {
                Email = checkUser.Email!,
                Token = decodedToken
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNewPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(NewPass), model);
            }

            var checkUser = await _userManager.FindByEmailAsync(model.Email);
            if (checkUser == null)
            {
                TempData["error"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(ForgetPass));
            }

            var resetResult = await _userManager.ResetPasswordAsync(checkUser, model.Token, model.Password);
            if (!resetResult.Succeeded)
            {
                foreach (var error in resetResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(nameof(NewPass), model);
            }

            TempData["success"] = "Đổi mật khẩu thành công.";
            return RedirectToAction(nameof(Login));
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpGet]
        public async Task<IActionResult> UpdateAccount(string tab = "home")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var vm = new AccountDashboardViewModel
            {
                User = currentUser,
                ActiveTab = string.IsNullOrWhiteSpace(tab) ? "home" : tab.ToLower()
            };

            if (vm.ActiveTab == "orders")
            {
                vm.Orders = await GetUserOrdersAsync(currentUser.Id);
            }

            return View(vm);
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInfoAccount(AccountDashboardViewModel model)
        {
            ModelState.Remove("ChangePassword.CurrentPassword");
            ModelState.Remove("ChangePassword.NewPassword");
            ModelState.Remove("ChangePassword.ConfirmNewPassword");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (!ModelState.IsValid)
            {
                model.User = currentUser;
                model.ActiveTab = "profile";
                return View("UpdateAccount", model);
            }

            currentUser.UserName = model.User.UserName;
            currentUser.Email = model.User.Email;
            currentUser.Address = model.User.Address;
            currentUser.PhoneNumber = model.User.PhoneNumber;

            var result = await _userManager.UpdateAsync(currentUser);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                model.User = currentUser;
                model.ActiveTab = "profile";
                return View("UpdateAccount", model);
            }

            TempData["success"] = "Cập nhật thông tin tài khoản thành công.";
            return RedirectToAction(nameof(UpdateAccount), new { tab = "profile" });
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpGet]
        public async Task<IActionResult> Details(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.UserId == currentUser.Id);

            if (order == null)
            {
                return NotFound();
            }

            var orderDetails = await _dataContext.OrderDetails
                .Include(x => x.Product)
                .Where(x => x.OrderId == order.OrderId)
                .ToListAsync();

            var vm = new UserOrderDetailViewModel
            {
                OrderCode = order.OrderCode,
                UserName = order.UserName,
                CreatedTime = order.CreatedTime,
                Status = order.Status,
                FullName = order.FullName,
                Phone = order.Phone,
                Email = order.Email,
                Address = order.Address,
                Note = order.Note,
                PaymentMethod = order.PaymentMethod,
                SubTotal = order.SubTotal,
                DiscountAmount = order.DiscountAmount,
                ShippingCost = order.ShippingCost,
                TotalAmount = order.TotalAmount,
                CanCancel = _orderStateService.CanCustomerCancel((OrderStatus)order.Status),
                OrderDetails = orderDetails.Select(x => new UserOrderItemViewModel
                {
                    ProductName = !string.IsNullOrWhiteSpace(x.ProductName)
                        ? x.ProductName
                        : (x.Product != null ? x.Product.Name : string.Empty),
                    Price = x.Price,
                    Quantity = x.Quantity,
                    BuildName = x.BuildName,
                    ComponentType = x.ComponentType
                }).ToList()
            };

            return View(vm);
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                TempData["error"] = "Mã đơn hàng không hợp lệ.";
                return RedirectToAction(nameof(History));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.UserId == currentUser.Id);

            if (order == null)
            {
                TempData["error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(History));
            }

            if (!_orderStateService.CanCustomerCancel((OrderStatus)order.Status))
            {
                TempData["error"] = "Đơn hàng này không còn ở trạng thái cho phép khách hủy.";
                return RedirectToAction(nameof(History));
            }

            await using var tx = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                await _inventoryService.RevertOrderInventoryAsync(
                    orderCode,
                    order.ReservationCode,
                    currentUser.Id,
                    "Khách hàng hủy đơn.");
                order.Status = (int)OrderStatus.Cancelled;

                await _dataContext.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["success"] = $"Đã hủy đơn hàng {orderCode} thành công.";
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["error"] = ex.Message;
            }

            return RedirectToAction(nameof(History));
        }


        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpGet]
        public async Task<IActionResult> History()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var orders = await _dataContext.Orders
                .Where(o => o.UserId == currentUser.Id)
                .OrderByDescending(o => o.CreatedTime)
                .ToListAsync();

            var result = orders.Select(o => new OrderHistoryViewModel
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                CreatedTime = o.CreatedTime,
                Status = o.Status,
                ShippingCost = o.ShippingCost,
                TotalAmount = o.TotalAmount,
                PaymentMethod = o.PaymentMethod,
                CanCancel = _orderStateService.CanCustomerCancel((OrderStatus)o.Status)
            }).ToList();

            return View(result);
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(AccountDashboardViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (!ModelState.IsValid)
            {
                model.User = currentUser;
                model.ActiveTab = "password";
                return View("UpdateAccount", model);
            }

            var result = await _userManager.ChangePasswordAsync(
                currentUser,
                model.ChangePassword.CurrentPassword,
                model.ChangePassword.NewPassword
            );

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                model.User = currentUser;
                model.ActiveTab = "password";
                return View("UpdateAccount", model);
            }

            await _signInManager.RefreshSignInAsync(currentUser);

            TempData["success"] = "Đổi mật khẩu thành công.";
            return RedirectToAction(nameof(UpdateAccount), new { tab = "password" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string returnURL = "/")
        {
            await _signInManager.SignOutAsync();
            if (!string.IsNullOrWhiteSpace(returnURL) && Url.IsLocalUrl(returnURL))
            {
                return LocalRedirect(returnURL);
            }

            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult LoginByGoogle(string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(GoogleResponse), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            return Challenge(properties, "Google");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GoogleResponse(string? returnUrl = null)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                TempData["error"] = "Không lấy được thông tin đăng nhập từ Google.";
                return RedirectToAction(nameof(Login));
            }

            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: true);

            if (result.Succeeded)
            {
                var linkedUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (linkedUser != null)
                {
                    var ensureRoleResult = await EnsureRoleAssignmentAsync(linkedUser, RoleNames.Customer);
                    if (!ensureRoleResult.Succeeded)
                    {
                        TempData["error"] = string.Join(" | ", ensureRoleResult.Errors.Select(e => e.Description));
                        await _signInManager.SignOutAsync();
                        return RedirectToAction(nameof(Login));
                    }

                    await _signInManager.RefreshSignInAsync(linkedUser);
                }

                TempData["success"] = "Đăng nhập thành công.";
                return await RedirectAfterSuccessfulSignInAsync(linkedUser, returnUrl);
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["error"] = "Google không trả về email.";
                return RedirectToAction(nameof(Login));
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new AppUserModel
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user);

                if (!createResult.Succeeded)
                {
                    TempData["error"] = string.Join(" | ", createResult.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Login));
                }
            }
            else if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            var checkLogin = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (checkLogin == null)
            {
                var addLoginResult = await _userManager.AddLoginAsync(user, info);

                if (!addLoginResult.Succeeded)
                {
                    TempData["error"] = string.Join(" | ", addLoginResult.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Login));
                }
            }

            var roleAssignmentResult = await EnsureRoleAssignmentAsync(user, RoleNames.Customer);
            if (!roleAssignmentResult.Succeeded)
            {
                TempData["error"] = string.Join(" | ", roleAssignmentResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Login));
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            TempData["success"] = "Đăng nhập thành công.";
            return await RedirectAfterSuccessfulSignInAsync(user, returnUrl);
        }

        private async Task<AppUserModel?> ResolveUserByLoginIdentifierAsync(string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return null;
            }

            var normalizedIdentifier = identifier.Trim();
            var user = await _userManager.FindByNameAsync(normalizedIdentifier);

            if (user != null)
            {
                return user;
            }

            return normalizedIdentifier.Contains('@')
                ? await _userManager.FindByEmailAsync(normalizedIdentifier)
                : null;
        }

        private async Task<IActionResult> RedirectAfterSuccessfulSignInAsync(
            AppUserModel? user,
            string? returnUrl = null)
        {
            if (user != null)
            {
                try
                {
                    await _cartService.MergeSessionCartAsync(HttpContext, user.Id);
                }
                catch
                {
                    TempData["error"] = "Đăng nhập thành công nhưng chưa thể đồng bộ giỏ hàng lúc này.";
                }
            }

            var roles = user != null
                ? await _userManager.GetRolesAsync(user)
                : Array.Empty<string>();

            var isBackOfficeUser = roles.Any(RoleNames.IsBackOfficeRole);

            if (IsAllowedReturnUrl(returnUrl, isBackOfficeUser))
            {
                return LocalRedirect(returnUrl!);
            }

            if (isBackOfficeUser)
            {
                return RedirectToAction("Index", "Portal", new { area = "Admin" });
            }

            return RedirectToAction("Index", "Home");
        }

        private bool IsAllowedReturnUrl(string? returnUrl, bool isBackOfficeUser)
        {
            if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                return false;
            }

            return !returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) || isBackOfficeUser;
        }

        private async Task<IdentityResult> EnsureRoleAssignmentAsync(AppUserModel user, string defaultRoleName)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            var targetRoleName = currentRoles.Count == 0
                ? defaultRoleName
                : RoleNames.ResolvePrimaryRoleName(currentRoles);

            var role = await _roleManager.FindByNameAsync(targetRoleName);
            if (role == null)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Description = $"Không tìm thấy vai trò {targetRoleName} trong hệ thống."
                });
            }

            if (currentRoles.Count == 0)
            {
                var addToRoleResult = await _userManager.AddToRoleAsync(user, role.Name!);
                if (!addToRoleResult.Succeeded)
                {
                    return addToRoleResult;
                }
            }

            if (string.Equals(user.RoleId, role.Id, StringComparison.Ordinal))
            {
                return IdentityResult.Success;
            }

            user.RoleId = role.Id;
            return await _userManager.UpdateAsync(user);
        }

        private async Task<List<OrderHistoryViewModel>> GetUserOrdersAsync(string userId)
        {
            var orders = await _dataContext.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedTime)
                .ToListAsync();

            return orders.Select(o => new OrderHistoryViewModel
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                CreatedTime = o.CreatedTime,
                Status = o.Status,
                ShippingCost = o.ShippingCost,
                TotalAmount = o.TotalAmount,
                PaymentMethod = o.PaymentMethod,
                CanCancel = _orderStateService.CanCustomerCancel((OrderStatus)o.Status)
            }).ToList();
        }
    }
}
