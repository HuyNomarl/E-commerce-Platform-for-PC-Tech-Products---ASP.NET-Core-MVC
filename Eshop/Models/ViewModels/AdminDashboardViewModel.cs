namespace Eshop.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int RangeDays { get; set; }
        public DateTime GeneratedAt { get; set; }

        public decimal RevenueToday { get; set; }
        public decimal RevenueInRange { get; set; }
        public decimal RevenueGrowthPercent { get; set; }

        public int OrdersInRange { get; set; }
        public decimal OrderGrowthPercent { get; set; }

        public decimal AverageOrderValue { get; set; }
        public decimal CancellationRate { get; set; }
        public decimal FulfillmentRate { get; set; }
        public decimal AverageRating { get; set; }

        public int TotalCustomers { get; set; }
        public int ActiveCustomersInRange { get; set; }
        public int RepeatCustomers { get; set; }

        public int TotalProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalPublishers { get; set; }
        public int TotalWarehouses { get; set; }

        public int PendingOrders { get; set; }
        public int ProcessingOrders { get; set; }
        public int ShippingOrders { get; set; }
        public int ActiveReservations { get; set; }
        public int PendingReceipts { get; set; }
        public int UnreadMessages { get; set; }

        public int LowStockProducts { get; set; }
        public int OutOfStockProducts { get; set; }
        public int AvailableInventoryUnits { get; set; }
        public int ReservedInventoryUnits { get; set; }

        public List<DashboardTrendPointViewModel> RevenueTrend { get; set; } = new();
        public List<DashboardBreakdownItemViewModel> StatusBreakdown { get; set; } = new();
        public List<DashboardBreakdownItemViewModel> PaymentBreakdown { get; set; } = new();
        public List<DashboardTopProductViewModel> TopProducts { get; set; } = new();
        public List<DashboardCategoryPerformanceViewModel> TopCategories { get; set; } = new();
        public List<DashboardCustomerViewModel> TopCustomers { get; set; } = new();
        public List<DashboardRecentOrderViewModel> RecentOrders { get; set; } = new();
        public List<DashboardStockAlertViewModel> LowStockItems { get; set; } = new();
        public List<DashboardWarehouseStockViewModel> WarehouseStocks { get; set; } = new();
    }

    public class DashboardTrendPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public decimal RevenuePercentOfMax { get; set; }
        public decimal OrderPercentOfMax { get; set; }
    }

    public class DashboardBreakdownItemViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AccentClass { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal Percent { get; set; }
    }

    public class DashboardTopProductViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public int AvailableStock { get; set; }
    }

    public class DashboardCategoryPerformanceViewModel
    {
        public string CategoryName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DashboardCustomerViewModel
    {
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DashboardRecentOrderViewModel
    {
        public string OrderCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string PaymentMethodCode { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public int Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class DashboardStockAlertViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int AvailableQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public string WarehouseSummary { get; set; } = string.Empty;
    }

    public class DashboardWarehouseStockViewModel
    {
        public string WarehouseName { get; set; } = string.Empty;
        public int OnHandQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public int AvailableQuantity { get; set; }
    }
}
