namespace Eshop.Models.Enums
{
    public enum InventoryTransactionType
    {
        Receive = 1,   // Nhập kho
        Issue = 2,     // Xuất kho
        Adjust = 3,    // Điều chỉnh
        Transfer = 4,  // Chuyển kho
        Return = 5     // Hoàn kho
    }
}
