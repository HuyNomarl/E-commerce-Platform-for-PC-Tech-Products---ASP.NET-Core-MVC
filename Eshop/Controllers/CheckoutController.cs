using Eshop.Areas.Admin.Repository;
using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Eshop.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IEmailSender _emailSender;

        public CheckoutController(DataContext dataContext, IEmailSender emailSender)
        {
            _dataContext = dataContext;
            _emailSender = emailSender;
        }

        public async Task<IActionResult> Checkout()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");

            var orderCode = Guid.NewGuid().ToString();

            var order = new OrderModel
            {
                OrderCode = orderCode,
                UserName = userEmail,
                Status = 1,
                CreatedTime = DateTime.Now
            };

            _dataContext.Orders.Add(order);

            var cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            foreach (var cart in cartItems)
            {
                _dataContext.OrderDetails.Add(new OrderDetails
                {
                    UserName = userEmail,
                    OrderCode = orderCode,
                    ProductId = (int)cart.ProductId,
                    Price = cart.Price,
                    Quantity = cart.Quantity
                });
            }

            await _dataContext.SaveChangesAsync();

            // gửi email sau khi lưu DB OK
            var subject = $"Eshop - Đặt hàng thành công #{orderCode}";
            var body = $@"
                <h3>Cảm ơn bạn đã đặt hàng!</h3>
                <p>Mã đơn hàng: <b>{orderCode}</b></p>
                <p>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                <p>Trạng thái: Chờ duyệt</p>
            ";

            try
            {
                await _emailSender.SendEmailAsync(userEmail, subject, body);
            }
            catch
            {
                // TempData["Warning"] = "Đặt hàng thành công nhưng gửi email thất bại!";
            }

            HttpContext.Session.Remove("Cart");
            TempData["Success"] = "Đặt hàng thành công, vui lòng chờ duyệt!";
            return RedirectToAction("Index", "Home");
        }
    }
}
