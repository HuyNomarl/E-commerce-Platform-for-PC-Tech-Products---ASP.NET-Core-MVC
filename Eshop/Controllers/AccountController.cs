using Eshop.Areas.Admin.Repository;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Eshop.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUserModel> _userManager;
        private readonly SignInManager<AppUserModel> _signInManager;
        private readonly DataContext _dataContext;
        private readonly IEmailSender _emailSender;

        public AccountController(UserManager<AppUserModel> userManager,
                                 SignInManager<AppUserModel> signInManager,
                                 IEmailSender emailSender,
                                 DataContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _dataContext = context;
        }

        public IActionResult Index() => RedirectToAction(nameof(Login));

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
            => View(new LoginViewModel { ReturnURL = returnUrl });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(
                    model.UserName, model.Password, false, false);
                if (result.Succeeded)
                {
                    return Redirect(model.ReturnURL ?? "/");
                }
                ModelState.AddModelError("", "Đăng nhập không thành công!");
            }
            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> UpdateAccount(string tab = "home")
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var vm = new AccountDashboardViewModel
            {
                User = currentUser,
                ActiveTab = string.IsNullOrWhiteSpace(tab) ? "home" : tab.ToLower()
            };

            if (vm.ActiveTab == "orders")
            {
                vm.Orders = await GetUserOrdersAsync(currentUser.Email);
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
                return RedirectToAction("Login", "Account");
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
            return RedirectToAction("UpdateAccount", new { tab = "profile" });
        }

        public async Task<IActionResult> ForgetPass()
        {
            return View();
        }
        public async Task<IActionResult> SendMailForgotPass(AppUserModel user)
        {
            var checkMail = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == user.Email);

            if (checkMail == null)
            {
                TempData["error"] = "Email not found";
                return RedirectToAction("ForgetPass", "Account");
            }
            else
            {
                string token = Guid.NewGuid().ToString();
                // update token to user
                checkMail.Token = token;
                _dataContext.Update(checkMail);
                await _dataContext.SaveChangesAsync();

                var receiver = checkMail.Email;
                var subject = "Change password for user " + checkMail.Email;
                var message = "Click on link to change password " +
                    "<a href='" + $"{Request.Scheme}://{Request.Host}/Account/NewPass?email=" + checkMail.Email + "&token=" + token + "'>Click here</a>";

                await _emailSender.SendEmailAsync(receiver, subject, message);
            }

            TempData["success"] = "An email has been sent to your registered email address with password reset instructions.";
            return RedirectToAction("ForgetPass", "Account");
        }


        public async Task<IActionResult> NewPass(AppUserModel appUser, string token)
        {
            var checkuser = await _userManager.Users
            .Where(u => u.Email == appUser.Email)
            .Where(u => u.Token == appUser.Token).FirstOrDefaultAsync();

            if (checkuser != null)
            {
                ViewBag.Email = checkuser.Email;
                ViewBag.Token = token;
            }
            else
            {
                TempData["error"] = "Email not found or token is not right";
                return RedirectToAction("ForgotPass", "Account");
            }

            return View();

        }
        [HttpPost]
        public async Task<IActionResult> UpdateNewPassword(AppUserModel user, string token)
        {
            var checkuser = await _userManager.Users
                .Where(u => u.Email == user.Email)
                .Where(u => u.Token == user.Token).FirstOrDefaultAsync();

            if (checkuser != null)
            {
                // update user with new password and token
                string newtoken = Guid.NewGuid().ToString();

                // Hash the new password
                var passwordHasher = new PasswordHasher<AppUserModel>();
                var passwordHash = passwordHasher.HashPassword(checkuser, user.PasswordHash);

                checkuser.PasswordHash = passwordHash;
                checkuser.Token = newtoken;

                await _userManager.UpdateAsync(checkuser);
                TempData["success"] = "Password updated successfully.";
                return RedirectToAction("Login", "Account");
            }
            else
            {
                TempData["error"] = "Email not found or token is not right";
                return RedirectToAction("ForgotPass", "Account");
            }

            return View();
        }

        //public async Task<IActionResult> Login()
        //{
        //    return View();
        //}
        public IActionResult Create()
        {
            return View();
        }
        [Authorize]
        public async Task<IActionResult> Details(string orderCode)
        {
            if (string.IsNullOrEmpty(orderCode))
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.UserName == currentUser.Email);

            if (order == null)
            {
                return NotFound();
            }

            var orderDetails = await _dataContext.OrderDetails
                .Include(x => x.Product)
                .Where(x => x.OrderCode == orderCode && x.UserName == currentUser.Email)
                .ToListAsync();

            var vm = new UserOrderDetailViewModel
            {
                OrderCode = order.OrderCode,
                UserName = order.UserName,
                CreatedTime = order.CreatedTime,
                Status = order.Status,
                ShippingCost = order.ShippingCost,
                OrderDetails = orderDetails.Select(x => new UserOrderItemViewModel
                {
                    ProductName = x.Product != null ? x.Product.Name : "",
                    Price = x.Price,
                    Quantity = x.Quantity
                }).ToList()
            };

            return View(vm);
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(string orderCode)
        {
            if (string.IsNullOrEmpty(orderCode))
            {
                TempData["error"] = "Mã đơn hàng không hợp lệ.";
                return RedirectToAction("History");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.UserName == currentUser.Email);

            if (order == null)
            {
                TempData["error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("History");
            }

            if (order.Status != 1)
            {
                TempData["error"] = "Chỉ có thể hủy đơn hàng đang chờ xác nhận.";
                return RedirectToAction("History");
            }

            order.Status = 6;
            _dataContext.Orders.Update(order);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = $"Đã hủy đơn hàng {orderCode} thành công.";
            return RedirectToAction("History");
        }

        [Authorize]
        public async Task<IActionResult> History()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var orders = await _dataContext.Orders
                .Where(o => o.UserName == currentUser.Email)
                .OrderByDescending(o => o.CreatedTime)
                .ToListAsync();

            var orderCodes = orders.Select(o => o.OrderCode).ToList();

            var detailTotals = await _dataContext.OrderDetails
                .Where(d => orderCodes.Contains(d.OrderCode))
                .GroupBy(d => d.OrderCode)
                .Select(g => new
                {
                    OrderCode = g.Key,
                    Total = g.Sum(x => x.Price * x.Quantity)
                })
                .ToListAsync();

            var result = orders.Select(o => new OrderHistoryViewModel
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                CreatedTime = o.CreatedTime,
                Status = o.Status,
                ShippingCost = o.ShippingCost,
                TotalAmount = (detailTotals.FirstOrDefault(x => x.OrderCode == o.OrderCode)?.Total ?? 0) + o.ShippingCost
            }).ToList();

            return View(result);
        }

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

            AppUserModel newUser = new AppUserModel
            {
                UserName = user.UserName,
                Email = user.Email,
                Address = user.Address
            };

            var result = await _userManager.CreateAsync(newUser, user.Password);

            if (result.Succeeded)
            {
                TempData["success"] = "Tài khoản đã được tạo thành công!";
                return RedirectToAction("Login", "Account");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(user);
        }
        public async Task<IActionResult> Logout(string returnURL = "/")
        {
            await _signInManager.SignOutAsync();
            return Redirect(returnURL);
        }
        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
        public IActionResult LoginByGoogle()
        {
            var redirectUrl = Url.Action("GoogleResponse", "Account");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            return Challenge(properties, "Google");
        }

        public async Task<IActionResult> GoogleResponse()
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                TempData["error"] = "Không lấy được thông tin đăng nhập từ Google";
                return RedirectToAction("Login", "Account");
            }

            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: true
            );

            if (result.Succeeded)
            {
                TempData["success"] = "Đăng nhập thành công";
                return RedirectToAction("Index", "Home");
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var name = info.Principal.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrEmpty(email))
            {
                TempData["error"] = "Google không trả về email";
                return RedirectToAction("Login", "Account");
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
                    return RedirectToAction("Login", "Account");
                }
            }

            var checkLogin = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (checkLogin == null)
            {
                var addLoginResult = await _userManager.AddLoginAsync(user, info);

                if (!addLoginResult.Succeeded)
                {
                    TempData["error"] = string.Join(" | ", addLoginResult.Errors.Select(e => e.Description));
                    return RedirectToAction("Login", "Account");
                }
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            TempData["success"] = "Đăng nhập thành công";
            return RedirectToAction("Index", "Home");
        }
        private async Task<List<OrderHistoryViewModel>> GetUserOrdersAsync(string email)
        {
            var orders = await _dataContext.Orders
                .Where(o => o.UserName == email)
                .OrderByDescending(o => o.CreatedTime)
                .ToListAsync();

            var orderCodes = orders.Select(o => o.OrderCode).ToList();

            var detailTotals = await _dataContext.OrderDetails
                .Where(d => orderCodes.Contains(d.OrderCode))
                .GroupBy(d => d.OrderCode)
                .Select(g => new
                {
                    OrderCode = g.Key,
                    Total = g.Sum(x => x.Price * x.Quantity)
                })
                .ToListAsync();

            return orders.Select(o => new OrderHistoryViewModel
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                CreatedTime = o.CreatedTime,
                Status = o.Status,
                ShippingCost = o.ShippingCost,
                TotalAmount = (detailTotals.FirstOrDefault(x => x.OrderCode == o.OrderCode)?.Total ?? 0) + o.ShippingCost
            }).ToList();
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(AccountDashboardViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
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
            return RedirectToAction("UpdateAccount", new { tab = "password" });
        }
    }
}
