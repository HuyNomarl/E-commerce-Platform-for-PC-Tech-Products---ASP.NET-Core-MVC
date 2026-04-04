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
        private readonly SignInManager<AppUserModel> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly DataContext _dataContext;

        public UserController(
            DataContext dataContext,
            UserManager<AppUserModel> userManager,
            SignInManager<AppUserModel> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _dataContext = dataContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);
            var users = await _userManager.Users
                .AsNoTracking()
                .OrderBy(x => x.UserName)
                .ThenBy(x => x.Email)
                .Select(user => new AppUserModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    RoleId = user.RoleId,
                    PasswordHash = user.PasswordHash,
                    LockoutEnabled = user.LockoutEnabled,
                    LockoutEnd = user.LockoutEnd
                })
                .ToListAsync();

            var userIds = users.Select(x => x.Id).ToList();

            var rolesById = await _roleManager.Roles
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);

            var userRoles = userIds.Count == 0
                ? new List<IdentityUserRole<string>>()
                : await _dataContext.UserRoles
                    .AsNoTracking()
                    .Where(x => userIds.Contains(x.UserId))
                    .ToListAsync();

            var userLogins = userIds.Count == 0
                ? new List<IdentityUserLogin<string>>()
                : await _dataContext.UserLogins
                    .AsNoTracking()
                    .Where(x => userIds.Contains(x.UserId))
                    .ToListAsync();

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

                    var primaryRoleName = assignedRoleNames.Count == 0
                        ? string.Empty
                        : RoleNames.ResolvePrimaryRoleName(assignedRoleNames);

                    var authMethods = loginLookup[user.Id]
                        .Where(provider => !string.IsNullOrWhiteSpace(provider))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(provider => provider)
                        .ToList();

                    if (!string.IsNullOrWhiteSpace(user.PasswordHash))
                    {
                        authMethods.Insert(0, "Mật khẩu");
                    }

                    if (authMethods.Count == 0)
                    {
                        authMethods.Add("Chưa cấu hình");
                    }

                    var isLocked = IsUserLocked(user);

                    return new AdminUserListItemViewModel
                    {
                        Id = user.Id,
                        UserName = user.UserName ?? string.Empty,
                        Email = user.Email ?? string.Empty,
                        PhoneNumber = user.PhoneNumber ?? string.Empty,
                        RoleName = string.IsNullOrWhiteSpace(primaryRoleName)
                            ? "Chưa gán vai trò"
                            : string.Join(", ", assignedRoleNames
                                .OrderBy(RoleNames.GetDisplayOrder)
                                .ThenBy(name => name)
                                .Select(RoleNames.GetDisplayName)),
                        RoleDescription = string.IsNullOrWhiteSpace(primaryRoleName)
                            ? "Tài khoản này chưa có vai trò chính được đồng bộ."
                            : RoleNames.GetDescription(primaryRoleName),
                        AuthMethods = string.Join(", ", authMethods),
                        HasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash),
                        IsLocked = isLocked,
                        LockoutEndText = isLocked && user.LockoutEnd.HasValue
                            ? user.LockoutEnd.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                            : null,
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
            NormalizeUserInput(model);

            var selectedRole = await FindRoleByIdAsync(model.RoleId);

            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError(nameof(model.Password), "Vui lòng nhập mật khẩu.");
            }

            if (selectedRole == null)
            {
                ModelState.AddModelError(nameof(model.RoleId), "Vai trò không tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(model.Email) &&
                await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Email đã tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(model.UserName) &&
                await _userManager.FindByNameAsync(model.UserName) != null)
            {
                ModelState.AddModelError(nameof(model.UserName), "Tên tài khoản đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            var user = new AppUserModel
            {
                UserName = model.UserName,
                Email = model.Email,
                PhoneNumber = EmptyToNull(model.PhoneNumber),
                Address = EmptyToNull(model.Address),
                Occupation = EmptyToNull(model.Occupation),
                RoleId = selectedRole!.Id
            };

            var createUserResult = await _userManager.CreateAsync(user, model.Password);
            if (!createUserResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddErrors(createUserResult);
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            var addToRoleResult = await _userManager.AddToRoleAsync(user, selectedRole.Name!);
            if (!addToRoleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddErrors(addToRoleResult);
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            await transaction.CommitAsync();

            TempData["SuccessMessage"] = "Tạo người dùng thành công.";
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
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Address = user.Address ?? string.Empty,
                Occupation = user.Occupation ?? string.Empty,
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

            NormalizeUserInput(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            var selectedRole = await FindRoleByIdAsync(model.RoleId);
            if (selectedRole == null)
            {
                ModelState.AddModelError(nameof(model.RoleId), "Vai trò không tồn tại.");
            }

            var duplicatedEmailUser = string.IsNullOrWhiteSpace(model.Email)
                ? null
                : await _userManager.FindByEmailAsync(model.Email);
            if (duplicatedEmailUser != null &&
                !string.Equals(duplicatedEmailUser.Id, user.Id, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.Email), "Email đã tồn tại.");
            }

            var duplicatedUserName = string.IsNullOrWhiteSpace(model.UserName)
                ? null
                : await _userManager.FindByNameAsync(model.UserName);
            if (duplicatedUserName != null &&
                !string.Equals(duplicatedUserName.Id, user.Id, StringComparison.Ordinal))
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

            var shouldSyncRoles = ShouldSyncRoles(currentRoles, selectedRole!);

            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            user.UserName = model.UserName;
            user.Email = model.Email;
            user.PhoneNumber = EmptyToNull(model.PhoneNumber);
            user.Address = EmptyToNull(model.Address);
            user.Occupation = EmptyToNull(model.Occupation);

            var updateUserResult = await _userManager.UpdateAsync(user);
            if (!updateUserResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddErrors(updateUserResult);
                await PopulateRolesAsync(model.RoleId);
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var passwordResult = await SetPasswordAsync(user, model.Password);
                if (!passwordResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    AddErrors(passwordResult);
                    await PopulateRolesAsync(model.RoleId);
                    return View(model);
                }
            }

            if (shouldSyncRoles)
            {
                if (currentRoles.Count > 0)
                {
                    var removeFromRolesResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!removeFromRolesResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        AddErrors(removeFromRolesResult);
                        await PopulateRolesAsync(model.RoleId);
                        return View(model);
                    }
                }

                var addToRoleResult = await _userManager.AddToRoleAsync(user, selectedRole.Name!);
                if (!addToRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    AddErrors(addToRoleResult);
                    await PopulateRolesAsync(model.RoleId);
                    return View(model);
                }
            }

            if (!string.Equals(user.RoleId, selectedRole.Id, StringComparison.Ordinal))
            {
                user.RoleId = selectedRole.Id;

                var syncRoleResult = await _userManager.UpdateAsync(user);
                if (!syncRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    AddErrors(syncRoleResult);
                    await PopulateRolesAsync(model.RoleId);
                    return View(model);
                }
            }

            await transaction.CommitAsync();

            if (isCurrentUser)
            {
                await _signInManager.RefreshSignInAsync(user);
            }

            TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(model.Password)
                ? "Cập nhật người dùng thành công."
                : "Cập nhật người dùng và đặt lại mật khẩu thành công.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Id người dùng không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var currentUserId = _userManager.GetUserId(User);
            if (string.Equals(currentUserId, id, StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] = "Bạn không thể tự xóa chính mình.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Người dùng không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var isAdmin = currentRoles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase);
            if (isAdmin && await CountUsersInRoleAsync(RoleNames.Admin) <= 1)
            {
                TempData["ErrorMessage"] = "Không thể xóa admin cuối cùng của hệ thống.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var deleteUserResult = await _userManager.DeleteAsync(user);
                if (!deleteUserResult.Succeeded)
                {
                    TempData["ErrorMessage"] = string.Join(" | ", deleteUserResult.Errors.Select(x => x.Description));
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = "Không thể xóa người dùng vì vẫn còn dữ liệu liên quan trong hệ thống.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Xóa người dùng thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Id người dùng không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var currentUserId = _userManager.GetUserId(User);
            if (string.Equals(currentUserId, id, StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] = "Bạn không thể tự khóa chính mình.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Người dùng không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            var isLocked = IsUserLocked(user);
            var currentRoles = await _userManager.GetRolesAsync(user);
            var isAdmin = currentRoles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase);

            if (!isLocked && isAdmin && await CountUnlockedUsersInRoleAsync(RoleNames.Admin) <= 1)
            {
                TempData["ErrorMessage"] = "Không thể khóa admin cuối cùng còn hoạt động.";
                return RedirectToAction(nameof(Index));
            }

            var enableLockoutResult = await _userManager.SetLockoutEnabledAsync(user, true);
            if (!enableLockoutResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(" | ", enableLockoutResult.Errors.Select(x => x.Description));
                return RedirectToAction(nameof(Index));
            }

            var lockoutEndResult = isLocked
                ? await _userManager.SetLockoutEndDateAsync(user, null)
                : await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));

            if (!lockoutEndResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(" | ", lockoutEndResult.Errors.Select(x => x.Description));
                return RedirectToAction(nameof(Index));
            }

            if (isLocked)
            {
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            _ = await _userManager.UpdateSecurityStampAsync(user);

            TempData["SuccessMessage"] = isLocked
                ? "Đã mở khóa tài khoản."
                : "Đã khóa tài khoản.";

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateRolesAsync(string? selectedRoleId = null)
        {
            var roles = (await _roleManager.Roles
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .ToListAsync())
                .OrderBy(x => RoleNames.GetDisplayOrder(x.Name))
                .ThenBy(x => x.Name)
                .ToList();

            ViewBag.Roles = roles
                .Select(role => new SelectListItem
                {
                    Value = role.Id,
                    Text = BuildRoleOptionLabel(role.Name),
                    Selected = string.Equals(role.Id, selectedRoleId, StringComparison.Ordinal)
                })
                .ToList();
        }

        private async Task<string?> ResolveSelectedRoleIdAsync(AppUserModel user)
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

        private async Task<string?> GetRoleIdByNameAsync(string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            return role?.Id;
        }

        private async Task<IdentityRole?> FindRoleByIdAsync(string? roleId)
        {
            if (string.IsNullOrWhiteSpace(roleId))
            {
                return null;
            }

            return await _roleManager.FindByIdAsync(roleId);
        }

        private async Task<int> CountUsersInRoleAsync(string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return 0;
            }

            var userIdsFromRoleMappings = _dataContext.UserRoles
                .AsNoTracking()
                .Where(x => x.RoleId == role.Id)
                .Select(x => x.UserId);

            var userIdsFromPrimaryRole = _dataContext.Users
                .AsNoTracking()
                .Where(x => x.RoleId == role.Id)
                .Select(x => x.Id);

            return await userIdsFromRoleMappings
                .Union(userIdsFromPrimaryRole)
                .CountAsync();
        }

        private async Task<int> CountUnlockedUsersInRoleAsync(string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return 0;
            }

            var userIds = await _dataContext.UserRoles
                .AsNoTracking()
                .Where(x => x.RoleId == role.Id)
                .Select(x => x.UserId)
                .Union(_dataContext.Users
                    .AsNoTracking()
                    .Where(x => x.RoleId == role.Id)
                    .Select(x => x.Id))
                .Distinct()
                .ToListAsync();

            if (userIds.Count == 0)
            {
                return 0;
            }

            var now = DateTimeOffset.UtcNow;

            return await _dataContext.Users
                .AsNoTracking()
                .Where(x => userIds.Contains(x.Id))
                .CountAsync(x => !x.LockoutEnabled || !x.LockoutEnd.HasValue || x.LockoutEnd <= now);
        }

        private async Task<IdentityResult> SetPasswordAsync(AppUserModel user, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return IdentityResult.Success;
            }

            if (await _userManager.HasPasswordAsync(user))
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                return await _userManager.ResetPasswordAsync(user, resetToken, password);
            }

            return await _userManager.AddPasswordAsync(user, password);
        }

        private static void NormalizeUserInput(AdminUserUpsertViewModel model)
        {
            model.UserName = model.UserName?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim() ?? string.Empty;
            model.PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty;
            model.Address = model.Address?.Trim() ?? string.Empty;
            model.Occupation = model.Occupation?.Trim() ?? string.Empty;
        }

        private static string? EmptyToNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string BuildRoleOptionLabel(string? roleName)
        {
            var normalizedRoleName = roleName?.Trim() ?? string.Empty;
            var displayName = RoleNames.GetDisplayName(normalizedRoleName);

            if (string.IsNullOrWhiteSpace(normalizedRoleName) ||
                string.Equals(displayName, normalizedRoleName, StringComparison.OrdinalIgnoreCase))
            {
                return displayName;
            }

            return $"{displayName} ({normalizedRoleName})";
        }

        private static bool ShouldSyncRoles(IList<string> currentRoles, IdentityRole selectedRole)
        {
            if (currentRoles.Count != 1)
            {
                return true;
            }

            return !string.Equals(currentRoles[0], selectedRole.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUserLocked(AppUserModel user)
        {
            return user.LockoutEnabled &&
                   user.LockoutEnd.HasValue &&
                   user.LockoutEnd.Value > DateTimeOffset.UtcNow;
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
