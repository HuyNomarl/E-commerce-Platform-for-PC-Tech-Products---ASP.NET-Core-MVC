using Eshop.Areas.Admin.Repository;
using Eshop.Hubs;
using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;

namespace Eshop.Services
{
    public class OrderService : IOrderService
    {
        private readonly DataContext _dataContext;
        private readonly IEmailSender _emailSender;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public OrderService(
            DataContext dataContext,
            IEmailSender emailSender,
            IHubContext<NotificationHub> notificationHub)
        {
            _dataContext = dataContext;
            _emailSender = emailSender;
            _notificationHub = notificationHub;
        }

        public async Task<string?> CreateOrderFromSessionAsync(HttpContext httpContext, ClaimsPrincipal user, CheckoutInputViewModel model)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = user.FindFirstValue(ClaimTypes.Email);
            var userName = user.Identity?.Name;

            if (string.IsNullOrEmpty(userId))
                return null;

            if (string.IsNullOrEmpty(userEmail))
                return null;

            var cartItems = httpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (cartItems.Count == 0)
                return null;

            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                var productIds = cartItems
                    .Select(x => (int)x.ProductId)
                    .Distinct()
                    .ToList();

                var products = await _dataContext.Products
                    .Where(x => productIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                var requestedQtyByProduct = cartItems
                    .GroupBy(x => (int)x.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                foreach (var row in requestedQtyByProduct)
                {
                    if (!products.TryGetValue(row.Key, out var product))
                        throw new InvalidOperationException($"Sản phẩm #{row.Key} không tồn tại.");

                    if (product.Quantity < row.Value)
                        throw new InvalidOperationException($"Sản phẩm \"{product.Name}\" chỉ còn {product.Quantity}, không đủ số lượng bạn đặt ({row.Value}).");
                }

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

                var fullAddress = $"{model.Address}, {model.phuong}"
                    + $"{(string.IsNullOrWhiteSpace(model.quan) ? "" : ", " + model.quan)}"
                    + $", {model.tinh}";

                var order = new OrderModel
                {
                    UserId = userId,
                    UserName = userName ?? userEmail,
                    OrderCode = orderCode,
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email,
                    Address = fullAddress,
                    Province = model.tinh,
                    District = model.quan,
                    Ward = model.phuong,
                    Note = model.Note,
                    Status = 1,
                    CreatedTime = DateTime.Now,
                    SubTotal = subTotal,
                    DiscountAmount = discountAmount,
                    CouponCode = couponCode,
                    ShippingCost = shippingCost,
                    TotalAmount = totalAmount
                };

                _dataContext.Orders.Add(order);
                await _dataContext.SaveChangesAsync();

                foreach (var cart in cartItems)
                {
                    if (!products.TryGetValue((int)cart.ProductId, out var product))
                        throw new InvalidOperationException($"Sản phẩm #{cart.ProductId} không tồn tại.");

                    _dataContext.OrderDetails.Add(new OrderDetails
                    {
                        OrderId = order.OrderId,
                        ProductId = (int)cart.ProductId,
                        ProductName = product.Name,
                        ProductImage = product.Image,
                        Price = cart.Price,
                        Quantity = cart.Quantity,
                        BuildGroupKey = cart.BuildGroupKey,
                        PcBuildId = cart.PcBuildId,
                        BuildName = cart.BuildName,
                        ComponentType = cart.ComponentType
                    });
                }

                foreach (var row in requestedQtyByProduct)
                {
                    var product = products[row.Key];
                    product.Quantity -= row.Value;
                    product.Sold += row.Value;
                }

                await _dataContext.SaveChangesAsync();
                await transaction.CommitAsync();

                //Notify admins
                await _notificationHub.Clients.Group("Admins").SendAsync("NewOrderCreated", new
                {
                    orderCode = orderCode,
                    fullName = model.FullName,
                    phone = model.Phone,
                    totalAmount = totalAmount.ToString("N0"),
                    createdAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    url = $"/Admin/Order/ViewOrder?orderCode={orderCode}"
                });

                var subject = $"Eshop - Đặt hàng thành công #{orderCode}";
                var body = $@"
                    <h3>Cảm ơn bạn đã đặt hàng!</h3>
                    <p>Mã đơn hàng: <b>{orderCode}</b></p>
                    <p>Người nhận: <b>{model.FullName}</b></p>
                    <p>SĐT: <b>{model.Phone}</b></p>
                    <p>Email: <b>{model.Email}</b></p>
                    <p>Địa chỉ: <b>{fullAddress}</b></p>
                    <p>Ghi chú: <b>{model.Note}</b></p>
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
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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