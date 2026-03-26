using Eshop.Helpers;
using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly DataContext _dataContext;

        public DashboardController(DataContext context)
        {
            _dataContext = context;
        }

        public async Task<IActionResult> Index(int days = 30)
        {
            days = NormalizeRange(days);

            var now = DateTime.Now;
            var today = now.Date;
            var rangeStart = today.AddDays(-(days - 1));
            var rangeEnd = today.AddDays(1);
            var previousRangeStart = rangeStart.AddDays(-days);

            var orders = await _dataContext.Orders
                .AsNoTracking()
                .Select(o => new OrderSnapshot
                {
                    OrderId = o.OrderId,
                    OrderCode = o.OrderCode,
                    UserId = o.UserId,
                    UserName = o.UserName,
                    FullName = o.FullName,
                    Email = o.Email,
                    CreatedTime = o.CreatedTime,
                    Status = o.Status,
                    TotalAmount = o.TotalAmount,
                    PaymentMethod = o.PaymentMethod
                })
                .ToListAsync();

            var salesRows = await _dataContext.OrderDetails
                .AsNoTracking()
                .Where(od => od.Order.CreatedTime >= rangeStart
                    && od.Order.CreatedTime < rangeEnd
                    && od.Order.Status != (int)OrderStatus.Cancelled)
                .Select(od => new SalesRowSnapshot
                {
                    ProductId = od.ProductId,
                    ProductName = od.ProductName,
                    CategoryName = od.Product.Category.Name,
                    Quantity = od.Quantity,
                    Revenue = od.Price * od.Quantity
                })
                .ToListAsync();

            var inventoryRows = await _dataContext.InventoryStocks
                .AsNoTracking()
                .Select(x => new InventoryRowSnapshot
                {
                    WarehouseName = x.Warehouse.Name,
                    ProductId = x.ProductId,
                    ProductName = x.Product.Name,
                    OnHandQuantity = x.OnHandQuantity,
                    ReservedQuantity = x.ReservedQuantity
                })
                .ToListAsync();

            var totalCustomers = await _dataContext.Users.CountAsync();
            var totalProducts = await _dataContext.Products.CountAsync();
            var totalCategories = await _dataContext.Categories.CountAsync();
            var totalPublishers = await _dataContext.Publishers.CountAsync();
            var totalWarehouses = await _dataContext.Warehouses.CountAsync(x => x.IsActive);
            var pendingReceipts = await _dataContext.InventoryReceipts.CountAsync(x => x.Status == InventoryReceiptStatus.Pending);
            var activeReservations = await _dataContext.InventoryReservations.CountAsync(x => x.Status == InventoryReservationStatus.Active);
            var unreadMessages = await _dataContext.Messages.CountAsync(x => !x.IsRead);
            var averageRating = (decimal)Math.Round(await _dataContext.RatingModels
                .AsNoTracking()
                .AverageAsync(x => (double?)x.Stars) ?? 0d, 1);

            var ordersInRange = orders
                .Where(o => o.CreatedTime >= rangeStart && o.CreatedTime < rangeEnd)
                .ToList();

            var previousOrders = orders
                .Where(o => o.CreatedTime >= previousRangeStart && o.CreatedTime < rangeStart)
                .ToList();

            var salesOrdersInRange = ordersInRange
                .Where(o => o.Status != (int)OrderStatus.Cancelled)
                .ToList();

            var previousSalesOrders = previousOrders
                .Where(o => o.Status != (int)OrderStatus.Cancelled)
                .ToList();

            var revenueToday = orders
                .Where(o => o.CreatedTime >= today && o.CreatedTime < rangeEnd && o.Status != (int)OrderStatus.Cancelled)
                .Sum(o => o.TotalAmount);

            var revenueInRange = salesOrdersInRange.Sum(o => o.TotalAmount);
            var previousRevenue = previousSalesOrders.Sum(o => o.TotalAmount);
            var averageOrderValue = salesOrdersInRange.Count == 0
                ? 0
                : Math.Round(revenueInRange / salesOrdersInRange.Count, 0);

            var statusBreakdown = BuildStatusBreakdown(ordersInRange);
            var paymentBreakdown = BuildPaymentBreakdown(ordersInRange);
            var trend = BuildTrend(ordersInRange, rangeStart, days);

            var productStockLookup = inventoryRows
                .GroupBy(x => new { x.ProductId, x.ProductName })
                .ToDictionary(
                    g => g.Key.ProductId,
                    g => new
                    {
                        ProductName = g.Key.ProductName,
                        Available = g.Sum(x => x.OnHandQuantity - x.ReservedQuantity),
                        Reserved = g.Sum(x => x.ReservedQuantity),
                        Warehouses = string.Join(", ", g
                            .Where(x => x.OnHandQuantity > 0 || x.ReservedQuantity > 0)
                            .Select(x => $"{x.WarehouseName}: {Math.Max(0, x.OnHandQuantity - x.ReservedQuantity)} khả dụng"))
                    });

            var lowStockItems = productStockLookup
                .Values
                .Where(x => x.Available > 0 && x.Available <= 5)
                .OrderBy(x => x.Available)
                .ThenByDescending(x => x.Reserved)
                .Take(6)
                .Select(x => new DashboardStockAlertViewModel
                {
                    ProductName = x.ProductName,
                    AvailableQuantity = x.Available,
                    ReservedQuantity = x.Reserved,
                    WarehouseSummary = string.IsNullOrWhiteSpace(x.Warehouses)
                        ? "Chưa có phân bổ tồn kho."
                        : x.Warehouses
                })
                .ToList();

            var topProducts = salesRows
                .GroupBy(x => new { x.ProductId, x.ProductName, x.CategoryName })
                .Select(g => new DashboardTopProductViewModel
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    CategoryName = g.Key.CategoryName,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Revenue),
                    AvailableStock = productStockLookup.TryGetValue(g.Key.ProductId, out var stock)
                        ? stock.Available
                        : 0
                })
                .OrderByDescending(x => x.QuantitySold)
                .ThenByDescending(x => x.Revenue)
                .Take(8)
                .ToList();

            var topCategories = salesRows
                .GroupBy(x => string.IsNullOrWhiteSpace(x.CategoryName) ? "Chưa phân loại" : x.CategoryName)
                .Select(g => new DashboardCategoryPerformanceViewModel
                {
                    CategoryName = g.Key,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Revenue)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(6)
                .ToList();

            var topCustomers = salesOrdersInRange
                .GroupBy(o => new
                {
                    o.UserId,
                    CustomerName = GetCustomerName(o.FullName, o.UserName, o.Email),
                    Email = string.IsNullOrWhiteSpace(o.Email) ? o.UserName : o.Email
                })
                .Select(g => new DashboardCustomerViewModel
                {
                    CustomerName = g.Key.CustomerName,
                    Email = g.Key.Email ?? string.Empty,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.Revenue)
                .ThenByDescending(x => x.OrderCount)
                .Take(6)
                .ToList();

            var recentOrders = orders
                .OrderByDescending(o => o.CreatedTime)
                .Take(8)
                .Select(o => new DashboardRecentOrderViewModel
                {
                    OrderCode = o.OrderCode,
                    CustomerName = GetCustomerName(o.FullName, o.UserName, o.Email),
                    PaymentMethodCode = o.PaymentMethod ?? string.Empty,
                    PaymentMethod = OrderDisplayHelper.GetPaymentMethodLabel(o.PaymentMethod),
                    Status = o.Status,
                    TotalAmount = o.TotalAmount,
                    CreatedTime = o.CreatedTime
                })
                .ToList();

            var warehouseStocks = inventoryRows
                .GroupBy(x => x.WarehouseName)
                .Select(g => new DashboardWarehouseStockViewModel
                {
                    WarehouseName = g.Key,
                    OnHandQuantity = g.Sum(x => x.OnHandQuantity),
                    ReservedQuantity = g.Sum(x => x.ReservedQuantity),
                    AvailableQuantity = g.Sum(x => x.OnHandQuantity - x.ReservedQuantity)
                })
                .OrderByDescending(x => x.AvailableQuantity)
                .ToList();

            var lowStockProducts = productStockLookup.Values.Count(x => x.Available > 0 && x.Available <= 5);
            var outOfStockProducts = productStockLookup.Values.Count(x => x.Available <= 0);

            var vm = new AdminDashboardViewModel
            {
                RangeDays = days,
                GeneratedAt = now,
                RevenueToday = revenueToday,
                RevenueInRange = revenueInRange,
                RevenueGrowthPercent = CalculateGrowthPercent(revenueInRange, previousRevenue),
                OrdersInRange = ordersInRange.Count,
                OrderGrowthPercent = CalculateGrowthPercent(ordersInRange.Count, previousOrders.Count),
                AverageOrderValue = averageOrderValue,
                CancellationRate = CalculateRate(
                    ordersInRange.Count(o => o.Status == (int)OrderStatus.Cancelled),
                    ordersInRange.Count),
                FulfillmentRate = CalculateRate(
                    ordersInRange.Count(o => o.Status == (int)OrderStatus.Delivered || o.Status == (int)OrderStatus.Completed),
                    ordersInRange.Count),
                AverageRating = averageRating,
                TotalCustomers = totalCustomers,
                ActiveCustomersInRange = salesOrdersInRange
                    .Where(o => !string.IsNullOrWhiteSpace(o.UserId))
                    .Select(o => o.UserId)
                    .Distinct()
                    .Count(),
                RepeatCustomers = orders
                    .Where(o => o.Status != (int)OrderStatus.Cancelled && !string.IsNullOrWhiteSpace(o.UserId))
                    .GroupBy(o => o.UserId)
                    .Count(g => g.Count() > 1),
                TotalProducts = totalProducts,
                TotalCategories = totalCategories,
                TotalPublishers = totalPublishers,
                TotalWarehouses = totalWarehouses,
                PendingOrders = orders.Count(o => o.Status == (int)OrderStatus.Pending),
                ProcessingOrders = orders.Count(o => o.Status == (int)OrderStatus.Processing),
                ShippingOrders = orders.Count(o => o.Status == (int)OrderStatus.Shipped),
                ActiveReservations = activeReservations,
                PendingReceipts = pendingReceipts,
                UnreadMessages = unreadMessages,
                LowStockProducts = lowStockProducts,
                OutOfStockProducts = outOfStockProducts,
                AvailableInventoryUnits = inventoryRows.Sum(x => x.OnHandQuantity - x.ReservedQuantity),
                ReservedInventoryUnits = inventoryRows.Sum(x => x.ReservedQuantity),
                RevenueTrend = trend,
                StatusBreakdown = statusBreakdown,
                PaymentBreakdown = paymentBreakdown,
                TopProducts = topProducts,
                TopCategories = topCategories,
                TopCustomers = topCustomers,
                RecentOrders = recentOrders,
                LowStockItems = lowStockItems,
                WarehouseStocks = warehouseStocks
            };

            return View(vm);
        }

        private static int NormalizeRange(int days)
        {
            return days is 7 or 30 or 90 ? days : 30;
        }

        private static decimal CalculateGrowthPercent(decimal current, decimal previous)
        {
            if (previous <= 0)
            {
                return current > 0 ? 100 : 0;
            }

            return Math.Round(((current - previous) / previous) * 100, 1);
        }

        private static decimal CalculateRate(int numerator, int denominator)
        {
            if (denominator <= 0)
            {
                return 0;
            }

            return Math.Round((decimal)numerator / denominator * 100, 1);
        }

        private static string GetCustomerName(string? fullName, string? userName, string? email)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                return email;
            }

            return userName ?? "Khách hàng";
        }

        private static List<DashboardBreakdownItemViewModel> BuildStatusBreakdown(IEnumerable<OrderSnapshot> orders)
        {
            var snapshots = orders.ToList();
            var totalOrders = snapshots.Count;

            return Enum.GetValues<OrderStatus>()
                .Select(status =>
                {
                    var matched = snapshots.Where(o => o.Status == (int)status).ToList();

                    return new DashboardBreakdownItemViewModel
                    {
                        Label = OrderDisplayHelper.GetStatusLabel(status),
                        Description = OrderDisplayHelper.GetStatusDescription(status),
                        AccentClass = OrderDisplayHelper.GetSoftBadgeClass(status),
                        Count = matched.Count,
                        Amount = matched.Sum(o => (decimal)o.TotalAmount),
                        Percent = CalculateRate(matched.Count, totalOrders)
                    };
                })
                .ToList();
        }

        private static List<DashboardBreakdownItemViewModel> BuildPaymentBreakdown(IEnumerable<OrderSnapshot> orders)
        {
            var snapshots = orders.ToList();
            var totalOrders = snapshots.Count;

            return snapshots
                .GroupBy(o => OrderDisplayHelper.NormalizePaymentMethod((string?)o.PaymentMethod))
                .Select(g => new DashboardBreakdownItemViewModel
                {
                    Label = OrderDisplayHelper.GetPaymentMethodLabel(g.Key),
                    Description = "Tỷ trọng đơn hàng theo phương thức thanh toán.",
                    AccentClass = OrderDisplayHelper.GetPaymentMethodBadgeClass(g.Key),
                    Count = g.Count(),
                    Amount = g.Where(x => x.Status != (int)OrderStatus.Cancelled).Sum(x => (decimal)x.TotalAmount),
                    Percent = CalculateRate(g.Count(), totalOrders)
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.Amount)
                .ToList();
        }

        private static List<DashboardTrendPointViewModel> BuildTrend(IEnumerable<OrderSnapshot> orders, DateTime rangeStart, int days)
        {
            var orderSnapshots = orders.ToList();
            var revenueByDay = orderSnapshots
                .Where(o => o.Status != (int)OrderStatus.Cancelled)
                .GroupBy(o => o.CreatedTime.Date)
                .ToDictionary(g => g.Key, g => g.Sum(x => (decimal)x.TotalAmount));

            var ordersByDay = orderSnapshots
                .GroupBy(o => o.CreatedTime.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var trend = Enumerable.Range(0, days)
                .Select(offset =>
                {
                    var date = rangeStart.AddDays(offset);
                    revenueByDay.TryGetValue(date, out var revenue);
                    ordersByDay.TryGetValue(date, out var orderCount);

                    return new DashboardTrendPointViewModel
                    {
                        Label = date.ToString("dd/MM"),
                        Revenue = revenue,
                        OrderCount = orderCount
                    };
                })
                .ToList();

            var maxRevenue = trend.Max(x => x.Revenue);
            var maxOrders = trend.Max(x => x.OrderCount);

            foreach (var item in trend)
            {
                item.RevenuePercentOfMax = maxRevenue <= 0
                    ? 0
                    : Math.Round(item.Revenue / maxRevenue * 100, 1);
                item.OrderPercentOfMax = maxOrders <= 0
                    ? 0
                    : Math.Round((decimal)item.OrderCount / maxOrders * 100, 1);
            }

            return trend;
        }

        private sealed class OrderSnapshot
        {
            public int OrderId { get; set; }
            public string OrderCode { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public DateTime CreatedTime { get; set; }
            public int Status { get; set; }
            public decimal TotalAmount { get; set; }
            public string? PaymentMethod { get; set; }
        }

        private sealed class SalesRowSnapshot
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string CategoryName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal Revenue { get; set; }
        }

        private sealed class InventoryRowSnapshot
        {
            public string WarehouseName { get; set; } = string.Empty;
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public int OnHandQuantity { get; set; }
            public int ReservedQuantity { get; set; }
        }
    }
}
