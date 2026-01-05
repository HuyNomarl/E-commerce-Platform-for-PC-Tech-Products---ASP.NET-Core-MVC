using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eshop.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly DataContext _dataContext;
        public CheckoutController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }
        public async Task<IActionResult> Checkout()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userEmail == null)
            {
                return RedirectToAction("Login", "Account");
            }
            else
            {
                var OrderCode = Guid.NewGuid().ToString();
                var OrderItems = new OrderModel();
                OrderItems.OrderCode = OrderCode;
                OrderItems.UserName = userEmail;    
                OrderItems.Status = 1;
                OrderItems.CreatedTime = DateTime.Now;
                _dataContext.Orders.Add(OrderItems);
                _dataContext.SaveChanges();
                List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
                foreach (var cart in cartItems)
                {
                    var orderDetail = new OrderDetails();
                    orderDetail.UserName = userEmail;
                    orderDetail.OrderCode = OrderCode;
                    orderDetail.ProductId = cart.ProductId;
                    orderDetail.Price = cart.Price;
                    orderDetail.Quantity = cart.Quantity;
                    _dataContext.OrderDetails.Add(orderDetail);
                    _dataContext.SaveChanges();
                }
                HttpContext.Session.Remove("Cart");
                TempData["Success"] = "Đặt hàng thành công, vui lòng chờ duyệt!";
                return RedirectToAction("Index", "Home");
            }
            return View();
        }
    }
}
