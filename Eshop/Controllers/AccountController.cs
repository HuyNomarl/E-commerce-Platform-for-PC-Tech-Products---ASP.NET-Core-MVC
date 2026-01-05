using Eshop.Models;
using Eshop.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Eshop.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUserModel> _userManager;
        private readonly SignInManager<AppUserModel> _signInManager;

        public AccountController(UserManager<AppUserModel> userManager,
                                 SignInManager<AppUserModel> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
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
        //public async Task<IActionResult> Login()
        //{
        //    return View();
        //}
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserModel user)
        {
            if (ModelState.IsValid)
            {
                AppUserModel newUser = new AppUserModel
                {
                    UserName = user.UserName,
                    Email = user.Email
                };
                IdentityResult result = await _userManager.CreateAsync(newUser, user.Password);
                
                if (result.Succeeded)
                {
                    TempData["success"] = "Tài khoản đã được tạo thành công!";
                    return RedirectToAction("/account/login");
                }
                foreach (IdentityError error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(user);
        }
        public async Task<IActionResult> Logout(string returnURL = "/")
        {
            await _signInManager.SignOutAsync();
            return Redirect(returnURL);
        }
    }
}
