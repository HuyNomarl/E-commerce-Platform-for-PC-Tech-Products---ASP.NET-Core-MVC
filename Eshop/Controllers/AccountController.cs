using Eshop.Areas.Admin.Repository;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eshop.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUserModel> _userManager;
        private readonly SignInManager<AppUserModel> _signInManager;
        private readonly DataContext _dataContext;
        private readonly IEmailSender _emailSender;
        private readonly IInventoryService _inventoryService;
        private readonly IOrderStateService _orderStateService;

        public AccountController(
                UserManager<AppUserModel> userManager,
                SignInManager<AppUserModel> signInManager,
                IEmailSender emailSender,
                DataContext context,
                IInventoryService inventoryService,
                IOrderStateService orderStateService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _dataContext = context;
            _inventoryService = inventoryService;
            _orderStateService = orderStateService;
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
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return Redirect(model.ReturnURL ?? "/");
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
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMailForgotPass(AppUserModel user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                TempData["error"] = "Vui lòng nhập email.";
                return RedirectToAction(nameof(ForgetPass));
            }

            var checkMail = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == user.Email);

            if (checkMail == null)
            {
                TempData["error"] = "Email không tồn tại.";
                return RedirectToAction(nameof(ForgetPass));
            }

            var token = Guid.NewGuid().ToString();

            checkMail.Token = token;
            _dataContext.Update(checkMail);
            await _dataContext.SaveChangesAsync();

            var receiver = checkMail.Email!;
            var subject = "Đổi mật khẩu tài khoản " + checkMail.Email;
            var message = "Bấm vào link để đổi mật khẩu: "
                + $"<a href='{Request.Scheme}://{Request.Host}/Account/NewPass?email={checkMail.Email}&token={token}'>Click here</a>";

            await _emailSender.SendEmailAsync(receiver, subject, message);

            TempData["success"] = "Đã gửi email hướng dẫn đặt lại mật khẩu.";
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

            var checkUser = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.Token == token);

            if (checkUser == null)
            {
                TempData["error"] = "Email không tồn tại hoặc token không đúng.";
                return RedirectToAction(nameof(ForgetPass));
            }

            ViewBag.Email = checkUser.Email;
            ViewBag.Token = token;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNewPassword(AppUserModel user, string token)
        {
            if (string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                TempData["error"] = "Dữ liệu không hợp lệ.";
                return RedirectToAction(nameof(ForgetPass));
            }

            var checkUser = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Email == user.Email && u.Token == token);

            if (checkUser == null)
            {
                TempData["error"] = "Email không tồn tại hoặc token không đúng.";
                return RedirectToAction(nameof(ForgetPass));
            }

            var newToken = Guid.NewGuid().ToString();

            var passwordHasher = new PasswordHasher<AppUserModel>();
            var passwordHash = passwordHasher.HashPassword(checkUser, user.PasswordHash);

            checkUser.PasswordHash = passwordHash;
            checkUser.Token = newToken;

            await _userManager.UpdateAsync(checkUser);

            TempData["success"] = "Đổi mật khẩu thành công.";
            return RedirectToAction(nameof(Login));
        }

        [Authorize]
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

        [Authorize]
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

        [Authorize]
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

        [Authorize]
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


        [Authorize]
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

        [Authorize]
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

        public async Task<IActionResult> Logout(string returnURL = "/")
        {
            await _signInManager.SignOutAsync();
            return Redirect(returnURL);
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
        public IActionResult LoginByGoogle()
        {
            var redirectUrl = Url.Action(nameof(GoogleResponse), "Account");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            return Challenge(properties, "Google");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GoogleResponse()
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
                TempData["success"] = "Đăng nhập thành công.";
                return RedirectToAction("Index", "Home");
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
                    Email = email
                };

                var createResult = await _userManager.CreateAsync(user);

                if (!createResult.Succeeded)
                {
                    TempData["error"] = string.Join(" | ", createResult.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Login));
                }
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

            await _signInManager.SignInAsync(user, isPersistent: false);

            TempData["success"] = "Đăng nhập thành công.";
            return RedirectToAction("Index", "Home");
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
