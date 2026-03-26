using Eshop.Constants;
using Eshop.Models;
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

        public async Task<IActionResult> Index()
        {
            var roles = (await _roleManager.Roles.ToListAsync())
                .OrderBy(r => RoleNames.GetDisplayOrder(r.Name))
                .ThenBy(r => r.Name)
                .ToList();

            return View(roles);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name")] IdentityRole input)
        {
            var roleName = input?.Name?.Trim();

            if (string.IsNullOrWhiteSpace(roleName))
            {
                ModelState.AddModelError(nameof(IdentityRole.Name), "Tên vai trò không được để trống!");
                return View(input);
            }

            if (await _roleManager.RoleExistsAsync(roleName))
            {
                ModelState.AddModelError(nameof(IdentityRole.Name), "Vai trò đã tồn tại!");
                return View(input);
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Thêm vai trò thành công!";
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View(input);
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

            return View(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Name")] IdentityRole input)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var existingRole = await _roleManager.FindByIdAsync(id);
            if (existingRole == null)
            {
                return NotFound();
            }

            if (RoleNames.IsSystemRole(existingRole.Name))
            {
                TempData["ErrorMessage"] = "Vai trò hệ thống không thể đổi tên.";
                return RedirectToAction(nameof(Index));
            }

            var newName = input?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                ModelState.AddModelError(nameof(IdentityRole.Name), "Tên vai trò không được để trống!");
                return View(existingRole);
            }

            if (!string.Equals(existingRole.Name, newName, StringComparison.OrdinalIgnoreCase)
                && await _roleManager.RoleExistsAsync(newName))
            {
                ModelState.AddModelError(nameof(IdentityRole.Name), "Vai trò đã tồn tại!");
                return View(existingRole);
            }

            existingRole.Name = newName;

            var result = await _roleManager.UpdateAsync(existingRole);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Cập nhật vai trò thành công!";
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View(existingRole);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Id không hợp lệ!";
                return RedirectToAction(nameof(Index));
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Vai trò không tồn tại!";
                return RedirectToAction(nameof(Index));
            }

            if (RoleNames.IsSystemRole(role.Name))
            {
                TempData["ErrorMessage"] = "Vai trò hệ thống không thể xóa.";
                return RedirectToAction(nameof(Index));
            }

            var assignedUsers = await _dataContext.UserRoles.CountAsync(x => x.RoleId == role.Id);
            if (assignedUsers > 0)
            {
                TempData["ErrorMessage"] = "Không thể xóa vai trò đang được gán cho người dùng.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Xóa vai trò thành công!";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
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
