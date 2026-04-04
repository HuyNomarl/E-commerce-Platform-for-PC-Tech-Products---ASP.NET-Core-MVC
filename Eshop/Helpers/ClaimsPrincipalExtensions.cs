using Eshop.Constants;
using System.Security.Claims;

namespace Eshop.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static bool IsInAnyRole(this ClaimsPrincipal? user, IEnumerable<string> roles)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role) && user.IsInRole(role))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CanAccessBackOffice(this ClaimsPrincipal? user) =>
            user.IsInAnyRole(RoleNames.BackOfficeRoles);

        public static bool CanAccessCatalog(this ClaimsPrincipal? user) =>
            user.IsInAnyRole(RoleNames.CatalogRoles);

        public static bool CanManageBrands(this ClaimsPrincipal? user) =>
            user.IsInAnyRole(RoleNames.BrandRoles);

        public static bool CanAccessOrders(this ClaimsPrincipal? user) =>
            user.IsInAnyRole(RoleNames.OrderRoles);

        public static bool CanAccessWarehouse(this ClaimsPrincipal? user) =>
            user.IsInAnyRole(RoleNames.WarehouseRoles);

        public static bool CanAccessSupport(this ClaimsPrincipal? user) =>
            user.IsInAnyRole(RoleNames.SupportRoles);

        public static bool IsCustomerOnly(this ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            return user.IsInRole(RoleNames.Customer) && !user.CanAccessBackOffice();
        }

        public static bool CanUseWishlistAndCompare(this ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            return user.IsCustomerOnly() || user.IsInRole(RoleNames.Admin);
        }

        public static string GetPrimaryRoleName(this ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return RoleNames.Customer;
            }

            foreach (var roleName in RoleNames.SystemRoles)
            {
                if (user.IsInRole(roleName))
                {
                    return roleName;
                }
            }

            var customRoles = user.FindAll(ClaimTypes.Role)
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return customRoles.Count == 0
                ? RoleNames.Customer
                : customRoles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).First();
        }

        public static string GetRoleDisplayLabel(this ClaimsPrincipal? user) =>
            RoleNames.GetDisplayName(user.GetPrimaryRoleName());

        public static string GetBackOfficeLandingController(this ClaimsPrincipal? user)
        {
            if (user == null)
            {
                return "Portal";
            }

            if (user.IsInRole(RoleNames.Admin))
            {
                return "Dashboard";
            }

            if (user.IsInRole(RoleNames.OrderStaff))
            {
                return "Order";
            }

            if (user.IsInRole(RoleNames.WarehouseManager))
            {
                return "Inventory";
            }

            if (user.IsInRole(RoleNames.CatalogManager))
            {
                return "Product";
            }

            if (user.IsInRole(RoleNames.SupportStaff))
            {
                return "Chat";
            }

            if (user.IsInRole(RoleNames.Publisher))
            {
                return "Publisher";
            }

            return "Portal";
        }

        public static string GetBackOfficeLandingAction(this ClaimsPrincipal? user) => "Index";
    }
}
