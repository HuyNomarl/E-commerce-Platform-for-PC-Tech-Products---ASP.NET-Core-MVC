using Eshop.Areas.Admin.Repository;
using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;

namespace Eshop.Services
{
    public class OrderService : IOrderService
    {
        private readonly DataContext _dataContext;
        private readonly IEmailSender _emailSender;

        public OrderService(DataContext dataContext, IEmailSender emailSender)
        {
            _dataContext = dataContext;
            _emailSender = emailSender;
        }

        public async Task<string?> CreateOrderFromSessionAsync(HttpContext httpContext, ClaimsPrincipal user)
        {
            var userEmail = user.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail))
                return null;

            var cartItems = httpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (cartItems.Count == 0)
                return null;

            decimal subTotal = cartItems.Sum(x => x.Quantity * x.Price);

            decimal shippingCost = 0m;
            var shippingCookie = httpContext.Request.Cookies["ShippingPrice"];

            if (!string.IsNullOrWhiteSpace(shippingCookie))
            {
                try
                {
                    shippingCost = JsonConvert.DeserializeObject<decimal>(shippingCookie);
                    if (shippingCost < 0)
                        shippingCost = 0m;
                }
                catch
                {
                    shippingCost = 0m;
                }
            }

            decimal discountAmount = 0m;
            string? couponCode = null;

            var appliedCoupon = httpContext.Session.GetJson<AppliedCouponModel>("Coupon");
            if (appliedCoupon != null)
            {
                var coupon = await _dataContext.Coupons.FirstOrDefaultAsync(x => x.Id == appliedCoupon.CouponId);

                if (coupon != null)
                {
                    var now = DateTime.Now;

                    bool isValid =
                        coupon.Status == 1 &&
                        coupon.Quantity > 0 &&
                        coupon.DateStart <= now &&
                        coupon.DateEnd >= now &&
                        (!coupon.MinOrderAmount.HasValue || subTotal >= coupon.MinOrderAmount.Value);

                    if (isValid)
                    {
                        discountAmount = CalculateDiscount(coupon, subTotal);
                        couponCode = coupon.NameCode;

                        coupon.Quantity -= 1;
                    }
                    else
                    {
                        httpContext.Session.Remove("Coupon");
                    }
                }
                else
                {
                    httpContext.Session.Remove("Coupon");
                }
            }

            decimal totalAmount = subTotal - discountAmount + shippingCost;
            if (totalAmount < 0)
                totalAmount = 0;

            var orderCode = Guid.NewGuid().ToString("N");

            var order = new OrderModel
            {
                OrderCode = orderCode,
                UserName = userEmail,
                Status = 1,
                CreatedTime = DateTime.Now,
                SubTotal = subTotal,
                DiscountAmount = discountAmount,
                CouponCode = couponCode,
                ShippingCost = shippingCost,
                TotalAmount = totalAmount
            };

            _dataContext.Orders.Add(order);

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

            var subject = $"Eshop - Đặt hàng thành công #{orderCode}";
            var body = $@"
                <h3>Cảm ơn bạn đã đặt hàng!</h3>
                <p>Mã đơn hàng: <b>{orderCode}</b></p>
                <p>Tạm tính: <b>{subTotal:N0} đ</b></p>
                <p>Giảm giá: <b>{discountAmount:N0} đ</b></p>
                <p>Phí ship: <b>{shippingCost:N0} đ</b></p>
                <p>Tổng thanh toán: <b>{totalAmount:N0} đ</b></p>
                <p>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
            ";

            try
            {
                await _emailSender.SendEmailAsync(userEmail, subject, body);
            }
            catch
            {
            }

            httpContext.Session.Remove("Cart");
            httpContext.Session.Remove("Coupon");
            httpContext.Response.Cookies.Delete("ShippingPrice");

            return orderCode;
        }

        private decimal CalculateDiscount(CouponModel coupon, decimal subTotal)
        {
            decimal discountAmount = 0;

            if (coupon == null || subTotal <= 0)
                return 0;

            if (coupon.DiscountType == 1)
            {
                discountAmount = subTotal * coupon.Discount / 100;
            }
            else if (coupon.DiscountType == 2)
            {
                discountAmount = coupon.Discount;
            }

            if (coupon.MaxDiscountAmount.HasValue && discountAmount > coupon.MaxDiscountAmount.Value)
            {
                discountAmount = coupon.MaxDiscountAmount.Value;
            }

            if (discountAmount > subTotal)
            {
                discountAmount = subTotal;
            }

            return discountAmount;
        }
    }
}