using Eshop.Areas.Admin.Models.ViewModels;
using Eshop.Constants;
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
    [Authorize(Policy = PolicyNames.AdminOnly)]
    public class UserController : Controller
    {
        private readonly UserManager<AppUserModel> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly DataContext _dataContext;

        public UserController(
            DataContext dataContext,
            UserManager<AppUserModel> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _dataContext = dataContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);
            var users = await _userManager.Users
                .OrderBy(x => x.UserName)
                .ThenBy(x => x.Email)
                .ToListAsync();

            var rolesById = await _roleManager.Roles
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);

            var userRoles = await _dataContext.UserRoles.ToListAsync();
            var userLogins = await _dataContext.UserLogins.ToListAsync();

            var roleLookup = userRoles.ToLookup(x => x.UserId, x => x.RoleId);
            var loginLookup = userLogins.ToLookup(x => x.UserId, x => x.LoginProvider);

            var model = users
                .Select(user =>
                {
                    var assignedRoleNames = roleLookup[user.Id]
                        .Select(roleId => rolesById.TryGetValue(roleId, out var roleName) ? roleName : null)
                        .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (assignedRoleNames.Count == 0 &&
                        !string.IsNullOrWhiteSpace(user.RoleId) &&
                        rolesById.TryGetValue(user.RoleId, out var roleNameFromUser))
                    {
                        assignedRoleNames.Add(roleNameFromUser);
                    }

                    var authMethods = loginLookup[user.Id]
                        .Where(provider => !string.IsNullOrWhiteSpace(provider))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(provider => provider)
                        .ToList();

                    if (!string.IsNullOrWhiteSpace(user.PasswordHash))
                    {
                        authMethods.Insert(0, "Local");
                    }

                    if (authMethods.Count == 0)
                    {
                        authMethods.Add("Chưa cấu hình");
                    }

                    return new AdminUserListItemViewModel
                    {
                        Id = user.Id,
                        UserName = user.UserName ?? string.Empty,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        RoleName = assignedRoleNames.Count == 0
                            ? "Chưa gán role"
                            : string.Join(", ", assignedRoleNames.OrderBy(name => name)),
                        AuthMethods = string.Join(", ", authMethods),
                        HasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash),
                        IsCurrentUser = string.Equals(user.Id, currentUserId, StringComparison.Ordinal)
                    };
                })
                .OrderByDescending(x => x.IsCurrentUser)
                .ThenBy(x => x.UserName)
                .ThenBy(x => x.Email)
                .ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var defaultRoleId = await GetRoleIdByNameAsync(RoleNames.Customer);
            var model = new AdminUserUpsertViewModel
            {
                RoleId = defaultRoleId ?? string.Empty
            };

            await PopulateRolesAsync(model.RoleId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminUserUpsertViewModel model)
        {
            var trimmedEmail = model.Email?.Trim() ?? string.Empty;
            var trimmedUserName = model.UserName?.Trim() ?? string.Empty;
            IdentityRole selectedRole = null;

            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError(nameof(model.Password), "Vui lòng nhập mật khẩu.");
            }

            if (!string.IsNullOrWhiteSpace(model.RoleId))
            {
                selectedRole = await _roleManager.FindByIdAsync(model.RoleId);
            }

            if (selectedRole == null)
            {
                ModelState.AddModelError(nameof(model.RoleId), "Vai trò không tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(trimmedEmail) && await _userManager.FindByEmailAsync(trimmedEmail) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Email đã tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(trimmedUserName) && await _userManager.FindByNameAsync(trimmedUserName) != null)
            {
                ModelState.AddModelError(nameof(model.UserName), "Tên tài khoản đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            var user = new AppUserModel
            {
                UserName = trimmedUserName,
                Email = trimmedEmail,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
                Occupation = string.IsNullOrWhiteSpace(model.Occupation) ? null : model.Occupation.Trim(),
                RoleId = selectedRole!.Id
            };

            var createUserResult = await _userManager.CreateAsync(user, model.Password!);
            if (!createUserResult.Succeeded)
            {
                AddErrors(createUserResult);
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            var addToRoleResult = await _userManager.AddToRoleAsync(user, selectedRole.Name!);
            if (!addToRoleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                AddErrors(addToRoleResult);
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            TempData["Success"] = "Tạo người dùng thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            if (string.Equals(currentUserId, id, StringComparison.Ordinal))
            {
                TempData["error"] = "Bạn không thể tự xóa chính mình.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var isAdmin = currentRoles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase);
            if (isAdmin && await CountUsersInRoleAsync(RoleNames.Admin) <= 1)
            {
                TempData["error"] = "Không thể xóa admin cuối cùng của hệ thống.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var deleteUserResult = await _userManager.DeleteAsync(user);
                if (!deleteUserResult.Succeeded)
                {
                    TempData["error"] = string.Join(" | ", deleteUserResult.Errors.Select(x => x.Description));
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException)
            {
                TempData["error"] = "Không thể xóa người dùng vì vẫn còn dữ liệu liên quan trong hệ thống.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "Xóa người dùng thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var model = new AdminUserUpsertViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                Occupation = user.Occupation,
                RoleId = await ResolveSelectedRoleIdAsync(user) ?? string.Empty
            };

            await PopulateRolesAsync(model.RoleId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminUserUpsertViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Id))
            {
                return NotFound();
            }

            var trimmedEmail = model.Email?.Trim() ?? string.Empty;
            var trimmedUserName = model.UserName?.Trim() ?? string.Empty;
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            IdentityRole selectedRole = null;
            if (!string.IsNullOrWhiteSpace(model.RoleId))
            {
                selectedRole = await _roleManager.FindByIdAsync(model.RoleId);
            }

            if (selectedRole == null)
            {
                ModelState.AddModelError(nameof(model.RoleId), "Vai trò không tồn tại.");
            }

            var duplicatedEmailUser = string.IsNullOrWhiteSpace(trimmedEmail)
                ? null
                : await _userManager.FindByEmailAsync(trimmedEmail);
            if (duplicatedEmailUser != null && !string.Equals(duplicatedEmailUser.Id, user.Id, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.Email), "Email đã tồn tại.");
            }

            var duplicatedUserName = string.IsNullOrWhiteSpace(trimmedUserName)
                ? null
                : await _userManager.FindByNameAsync(trimmedUserName);
            if (duplicatedUserName != null && !string.Equals(duplicatedUserName.Id, user.Id, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.UserName), "Tên tài khoản đã tồn tại.");
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentUserId = _userManager.GetUserId(User);
            var isCurrentUser = string.Equals(user.Id, currentUserId, StringComparison.Ordinal);
            var isCurrentlyAdmin = currentRoles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase);
            var willRemainAdmin = string.Equals(selectedRole?.Name, RoleNames.Admin, StringComparison.OrdinalIgnoreCase);

            if (isCurrentUser && isCurrentlyAdmin && !willRemainAdmin)
            {
                ModelState.AddModelError(nameof(model.RoleId), "Bạn không thể tự gỡ quyền Admin của chính mình.");
            }

            if (isCurrentlyAdmin && !willRemainAdmin && await CountUsersInRoleAsync(RoleNames.Admin) <= 1)
            {
                ModelState.AddModelError(nameof(model.RoleId), "Không thể hạ quyền admin cuối cùng của hệ thống.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            user.UserName = trimmedUserName;
            user.Email = trimmedEmail;
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            user.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            user.Occupation = string.IsNullOrWhiteSpace(model.Occupation) ? null : model.Occupation.Trim();

            var updateUserResult = await _userManager.UpdateAsync(user);
            if (!updateUserResult.Succeeded)
            {
                AddErrors(updateUserResult);
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            if (currentRoles.Count > 0)
            {
                var removeFromRolesResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeFromRolesResult.Succeeded)
                {
                    AddErrors(removeFromRolesResult);
                    await PopulateRolesAsync(model.RoleId);
                    return View(model);
                }
            }

            var addToRoleResult = await _userManager.AddToRoleAsync(user, selectedRole!.Name!);
            if (!addToRoleResult.Succeeded)
            {
                AddErrors(addToRoleResult);
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            if (!string.Equals(user.RoleId, selectedRole.Id, StringComparison.Ordinal))
            {
                user.RoleId = selectedRole.Id;
                var syncRoleResult = await _userManager.UpdateAsync(user);
                if (!syncRoleResult.Succeeded)
                {
                    AddErrors(syncRoleResult);
                    await PopulateRolesAsync(model.RoleId);
                    return View(model);
                }
            }

            TempData["Success"] = "Cập nhật người dùng thành công!";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateRolesAsync(string selectedRoleId = null)
        {
            var roles = (await _roleManager.Roles.ToListAsync())
                .OrderBy(x => RoleNames.GetDisplayOrder(x.Name))
                .ThenBy(x => x.Name)
                .ToList();

            ViewBag.Roles = new SelectList(roles, "Id", "Name", selectedRoleId);
        }

        private async Task<string> ResolveSelectedRoleIdAsync(AppUserModel user)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0)
            {
                var primaryRoleName = RoleNames.ResolvePrimaryRoleName(currentRoles);
                var currentRole = await _roleManager.FindByNameAsync(primaryRoleName);
                if (currentRole != null)
                {
                    return currentRole.Id;
                }
            }

            return user.RoleId;
        }

        private async Task<string> GetRoleIdByNameAsync(string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            return role?.Id;
        }

        private async Task<int> CountUsersInRoleAsync(string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return 0;
            }

            return await _dataContext.UserRoles.CountAsync(x => x.RoleId == role.Id);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
