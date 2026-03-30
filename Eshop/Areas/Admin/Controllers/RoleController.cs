using Eshop.Areas.Admin.Models.ViewModels;
using Eshop.Constants;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.AdminOnly)]
    public class RoleController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly DataContext _dataContext;

        public RoleController(RoleManager<IdentityRole> roleManager, DataContext dataContext)
        {
            _roleManager = roleManager;
            _dataContext = dataContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var roles = (await _roleManager.Roles
                .AsNoTracking()
                .ToListAsync())
                .OrderBy(r => RoleNames.GetDisplayOrder(r.Name))
                .ThenBy(r => r.Name)
                .ToList();

            return View(roles);
        }

        [HttpGet]
        public IActionResult Create() => View(new AdminRoleUpsertViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminRoleUpsertViewModel model)
        {
            model.Name = NormalizeRoleName(model.Name);

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên vai trò không được để trống.");
            }
            else if (RoleNames.IsSystemRole(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Vai trò hệ thống được quản lý tự động và không thể tạo thủ công.");
            }
            else if (await _roleManager.RoleExistsAsync(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Vai trò đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(model.Name));
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Thêm vai trò thành công.";
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            if (RoleNames.IsSystemRole(role.Name))
            {
                TempData["ErrorMessage"] = "Vai trò hệ thống không thể đổi tên.";
                return RedirectToAction(nameof(Index));
            }

            return View(new AdminRoleUpsertViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminRoleUpsertViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Id))
            {
                return NotFound();
            }

            var existingRole = await _roleManager.FindByIdAsync(model.Id);
            if (existingRole == null)
            {
                return NotFound();
            }

            if (RoleNames.IsSystemRole(existingRole.Name))
            {
                TempData["ErrorMessage"] = "Vai trò hệ thống không thể đổi tên.";
                return RedirectToAction(nameof(Index));
            }

            model.Name = NormalizeRoleName(model.Name);

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên vai trò không được để trống.");
            }
            else if (RoleNames.IsSystemRole(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Không thể đổi tên thành vai trò hệ thống.");
            }
            else if (!string.Equals(existingRole.Name, model.Name, StringComparison.OrdinalIgnoreCase)
                && await _roleManager.RoleExistsAsync(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Vai trò đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (string.Equals(existingRole.Name, model.Name, StringComparison.Ordinal))
            {
                TempData["SuccessMessage"] = "Không có thay đổi nào ở vai trò.";
                return RedirectToAction(nameof(Index));
            }

            existingRole.Name = model.Name;

            var result = await _roleManager.UpdateAsync(existingRole);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Cập nhật vai trò thành công.";
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Id vai trò không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Vai trò không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            if (RoleNames.IsSystemRole(role.Name))
            {
                TempData["ErrorMessage"] = "Vai trò hệ thống không thể xóa.";
                return RedirectToAction(nameof(Index));
            }

            var hasAssignedUsers =
                await _dataContext.UserRoles.AsNoTracking().AnyAsync(x => x.RoleId == role.Id) ||
                await _dataContext.Users.AsNoTracking().AnyAsync(x => x.RoleId == role.Id);

            if (hasAssignedUsers)
            {
                TempData["ErrorMessage"] = "Không thể xóa vai trò đang được gán cho người dùng.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Xóa vai trò thành công.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        private static string NormalizeRoleName(string? roleName) =>
            roleName?.Trim() ?? string.Empty;

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
