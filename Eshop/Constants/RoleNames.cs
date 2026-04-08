using System;
using System.Collections.Generic;
using System.Linq;

namespace Eshop.Constants
{
    public static class RoleNames
    {
        public const string Admin = "Admin";
        public const string Customer = "Customer";
        public const string CatalogManager = "CatalogManager";
        public const string Publisher = "Publisher";
        public const string OrderStaff = "OrderStaff";
        public const string WarehouseManager = "WarehouseManager";
        public const string SupportStaff = "SupportStaff";

        public static readonly string[] CatalogRoles =
        {
            Admin,
            CatalogManager
        };

        public static readonly string[] BrandRoles =
        {
            Admin,
            CatalogManager,
            Publisher
        };

        public static readonly string[] OrderRoles =
        {
            Admin,
            OrderStaff
        };

        public static readonly string[] WarehouseRoles =
        {
            Admin,
            WarehouseManager
        };

        public static readonly string[] SupportRoles =
        {
            Admin,
            SupportStaff
        };

        public static readonly string[] BackOfficeRoles =
        {
            Admin,
            CatalogManager,
            Publisher,
            OrderStaff,
            WarehouseManager,
            SupportStaff
        };

        public static readonly string[] SystemRoles =
        {
            Admin,
            CatalogManager,
            Publisher,
            OrderStaff,
            WarehouseManager,
            SupportStaff,
            Customer
        };

        public static bool IsSystemRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            return SystemRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsBackOfficeRole(string? roleName) =>
            IsRoleIn(roleName, BackOfficeRoles);

        public static bool IsCatalogRole(string? roleName) =>
            IsRoleIn(roleName, CatalogRoles);

        public static bool IsBrandRole(string? roleName) =>
            IsRoleIn(roleName, BrandRoles);

        public static bool IsOrderRole(string? roleName) =>
            IsRoleIn(roleName, OrderRoles);

        public static bool IsWarehouseRole(string? roleName) =>
            IsRoleIn(roleName, WarehouseRoles);

        public static bool IsSupportRole(string? roleName) =>
            IsRoleIn(roleName, SupportRoles);

        public static string ResolvePrimaryRoleName(IEnumerable<string> roleNames)
        {
            var normalizedRoles = roleNames
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedRoles.Count == 0)
            {
                return Customer;
            }

            foreach (var preferredRole in SystemRoles)
            {
                if (normalizedRoles.Contains(preferredRole, StringComparer.OrdinalIgnoreCase))
                {
                    return preferredRole;
                }
            }

            return normalizedRoles
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        public static string GetDisplayName(string? roleName)
        {
            return roleName?.Trim() switch
            {
                Admin => "Quản trị viên",
                CatalogManager => "Quan ly catalog",
                Publisher => "Quan ly thuong hieu",
                OrderStaff => "Nhan vien don hang",
                WarehouseManager => "Quản lý kho",
                SupportStaff => "Nhân viên hỗ trợ",
                Customer => "Khách hàng",
                _ => string.IsNullOrWhiteSpace(roleName) ? "Khách hàng" : roleName.Trim()
            };
        }

        public static string GetDescription(string? roleName)
        {
            return roleName?.Trim() switch
            {
                Admin => "Toàn quyền hệ thống",
                CatalogManager => "Quan ly san pham, danh muc, slider, PC Builder va noi dung catalog.",
                Publisher => "Chi quan ly thuong hieu va nha phat hanh trong catalog.",
                OrderStaff => "Chi xem don hang va cap nhat trang thai xu ly don.",
                WarehouseManager => "Chi thao tac kho, phieu nhap, dieu chuyen, kiem ke va ton kho.",
                SupportStaff => "Chi lam viec voi hoi thoai ho tro khach hang.",
                Customer => "Chi dung cac chuc nang tai khoan va thao tac phia nguoi dung.",
                _ => "Vai tro tuy chinh trong he thong."
            };
        }

        public static int GetDisplayOrder(string? roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return int.MaxValue;
            }

            var index = Array.FindIndex(
                SystemRoles,
                systemRole => string.Equals(systemRole, roleName, StringComparison.OrdinalIgnoreCase));

            return index >= 0 ? index : int.MaxValue - 1;
        }

        private static bool IsRoleIn(string? roleName, IEnumerable<string> supportedRoles)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            return supportedRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
