using Eshop.Constants;
using Eshop.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Repository
{
    public static class IdentityDataSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("IdentityDataSeeder");
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUserModel>>();

            foreach (var roleName in RoleNames.SystemRoles)
            {
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                var createRoleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!createRoleResult.Succeeded)
                {
                    logger.LogWarning(
                        "Khong the tao role mac dinh {RoleName}: {Errors}",
                        roleName,
                        string.Join(" | ", createRoleResult.Errors.Select(x => x.Description)));
                }
            }

            var users = await userManager.Users.ToListAsync();
            foreach (var user in users)
            {
                var userRoles = await userManager.GetRolesAsync(user);

                if (userRoles.Count == 0)
                {
                    var addDefaultRoleResult = await userManager.AddToRoleAsync(user, RoleNames.Customer);
                    if (!addDefaultRoleResult.Succeeded)
                    {
                        logger.LogWarning(
                            "Khong the gan role mac dinh cho user {UserId}: {Errors}",
                            user.Id,
                            string.Join(" | ", addDefaultRoleResult.Errors.Select(x => x.Description)));
                        continue;
                    }

                    userRoles = new List<string> { RoleNames.Customer };
                }

                var primaryRoleName = RoleNames.ResolvePrimaryRoleName(userRoles);
                var primaryRole = await roleManager.FindByNameAsync(primaryRoleName);

                if (primaryRole == null || string.Equals(user.RoleId, primaryRole.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                user.RoleId = primaryRole.Id;
                var updateUserResult = await userManager.UpdateAsync(user);
                if (!updateUserResult.Succeeded)
                {
                    logger.LogWarning(
                        "Khong the dong bo RoleId cho user {UserId}: {Errors}",
                        user.Id,
                        string.Join(" | ", updateUserResult.Errors.Select(x => x.Description)));
                }
            }
        }
    }
}
