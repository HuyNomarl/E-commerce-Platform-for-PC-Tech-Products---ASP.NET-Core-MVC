using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<AppUserModel> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly DataContext _dataContext;

        public object AddIdentityErrors { get; private set; }

        public UserController(DataContext dataContext,UserManager<AppUserModel> userManager, RoleManager<IdentityRole> roleManager)
        {

            _userManager = userManager;
            _roleManager = roleManager;
            _dataContext = dataContext;

        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var usersWithRoles = await (from u in _dataContext.Users
                                        join ur in _dataContext.UserRoles on u.Id equals ur.UserId
                                        join r in _dataContext.Roles on ur.RoleId equals r.Id
                                        select new
                                        {
                                            User = u,
                                            RoleName = r.Name
                                        }).ToListAsync();

            return View(usersWithRoles);
        }
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            ViewBag.Roles = new SelectList(roles, "Id", "Name");
            return View(new AppUserModel());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppUserModel user)
        {
            if (ModelState.IsValid)
            {
                var createUserResult = await _userManager.CreateAsync(user, user.PasswordHash);

                if (createUserResult.Succeeded)
                {
                    var createdUser = await _userManager.FindByEmailAsync(user.Email); // tim user
                    var userId = createdUser.Id;
                    var role = await _roleManager.FindByIdAsync(user.RoleId);
                    var addToRoleResult = await _userManager.AddToRoleAsync(createdUser, role.Name);

                    if (!addToRoleResult.Succeeded)
                    {
                        foreach (var error in addToRoleResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        return View(user);
                    }
                    TempData["Success"] = "Tạo người dùng thành công!";
                    return RedirectToAction("Index", "User");
                }
                else
                {
                    foreach (var error in createUserResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View(user);
                }
            }
            else
            {
                TempData["error"] = "Model đang bị lỗi, vui lòng thử lại sau!";
                List<string> errors = new List<string>();
                foreach (var error in ModelState.Values)
                {
                    foreach (var errorModel in error.Errors)
                    {
                        errors.Add(errorModel.ErrorMessage);
                    }
                }
                string errorMessage = string.Join("\n", errors);
                return BadRequest(errorMessage);
            }
            var roles = await _roleManager.Roles.ToListAsync();
            ViewBag.Roles = new SelectList(roles, "Id", "Name");
            return View(user);
        }
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            var deleteUserResult = await _userManager.DeleteAsync(user);
            if (!deleteUserResult.Succeeded)
            {
                return View("Error");
            }
            TempData["Success"] = "Xóa người dùng thành công!";
            return RedirectToAction("Index");
        }
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            var roles = await _roleManager.Roles.ToListAsync();
            ViewBag.Roles = new SelectList(roles, "Id", "Name");

            return View(user);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AppUserModel user)
        {
            if (!ModelState.IsValid)
            {
                var roles = await _roleManager.Roles.ToListAsync();
                ViewBag.Roles = new SelectList(roles, "Id", "Name");
                return View(user);
            }

            var existingUser = await _userManager.FindByIdAsync(user.Id);
            if (existingUser == null) return NotFound();

            // Update các field cơ bản
            existingUser.UserName = user.UserName;
            existingUser.Email = user.Email;
            existingUser.Occupation = user.Occupation;

            var updateUserResult = await _userManager.UpdateAsync(existingUser);
            if (!updateUserResult.Succeeded)
            {
                foreach (var error in updateUserResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                var roles = await _roleManager.Roles.ToListAsync();
                ViewBag.Roles = new SelectList(roles, "Id", "Name");
                return View(user);
            }

            // Update ROLE (quan trọng)
            var newRole = await _roleManager.FindByIdAsync(user.RoleId);
            if (newRole == null)
            {
                ModelState.AddModelError("", "Role không tồn tại.");
                var roles = await _roleManager.Roles.ToListAsync();
                ViewBag.Roles = new SelectList(roles, "Id", "Name");
                return View(user);
            }

            var currentRoles = await _userManager.GetRolesAsync(existingUser);
            await _userManager.RemoveFromRolesAsync(existingUser, currentRoles);
            var addToRoleResult = await _userManager.AddToRoleAsync(existingUser, newRole.Name);

            if (!addToRoleResult.Succeeded)
            {
                foreach (var error in addToRoleResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                var roles = await _roleManager.Roles.ToListAsync();
                ViewBag.Roles = new SelectList(roles, "Id", "Name");
                return View(user);
            }

            TempData["Success"] = "Cập nhật người dùng thành công!";
            return RedirectToAction("Index");
        }

    }
}