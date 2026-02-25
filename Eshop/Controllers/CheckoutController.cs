using Eshop.Areas.Admin.Repository;
using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eshop.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly DataContext _db;
        private readonly IEmailSender _emailSender;

        public CheckoutController(DataContext db, IEmailSender emailSender)
        {
            _db = db;
            _emailSender = emailSender;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");

            var cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new();
            if (!cartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng trống!";
                return RedirectToAction("Index", "Cart");
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var productIds = cartItems.Select(x => (int)x.ProductId).Distinct().ToList();

                var products = await _db.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                // 1) Check tồn kho
                foreach (var item in cartItems)
                {
                    if (!products.TryGetValue((int)item.ProductId, out var p))
                    {
                        TempData["Error"] = "Có sản phẩm không tồn tại.";
                        await tx.RollbackAsync();
                        return RedirectToAction("Index", "Cart");
                    }

                    if (p.Quantity < item.Quantity)
                    {
                        TempData["Error"] = $"Sản phẩm \"{p.Name}\" không đủ tồn. Còn {p.Quantity}.";
                        await tx.RollbackAsync();
                        return RedirectToAction("Index", "Cart");
                    }
                }

                // 2) Tạo Order
                var orderCode = Guid.NewGuid().ToString("N");

                var order = new OrderModel
                {
                    OrderCode = orderCode,
                    UserName = userEmail,
                    Status = (int)OrderStatus.Pending,
                    CreatedTime = DateTime.Now
                };
                _db.Orders.Add(order);

                // 3) Tạo OrderDetails + TRỪ KHO (giữ hàng)
                foreach (var item in cartItems)
                {
                    var p = products[(int)item.ProductId];

                    p.Quantity -= item.Quantity; // trừ kho
                    // Sold KHÔNG tăng ở đây

                    _db.OrderDetails.Add(new OrderDetails
                    {
                        UserName = userEmail,
                        OrderCode = orderCode,
                        ProductId = p.Id,
                        Price = item.Price,
                        Quantity = item.Quantity
                    });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // 4) Email (sau commit)
                try
                {
                    var subject = $"Eshop - Đặt hàng thành công #{orderCode}";
                    var body = $@"
                        <h3>Cảm ơn bạn đã đặt hàng!</h3>
                        <p>Mã đơn hàng: <b>{orderCode}</b></p>
                        <p>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                    ";
                    await _emailSender.SendEmailAsync(userEmail, subject, body);
                }
                catch { }

                HttpContext.Session.Remove("Cart");
                TempData["Success"] = "Đặt hàng thành công, vui lòng chờ duyệt!";
                return RedirectToAction("Index", "Home");
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Checkout thất bại, vui lòng thử lại!";
                return RedirectToAction("Index", "Cart");
            }
        }
    }
}
