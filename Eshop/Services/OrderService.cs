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
        private readonly IInventoryService _inventoryService;


        public OrderService(
                  DataContext dataContext,
                  IEmailSender emailSender,
                  IHubContext<NotificationHub> notificationHub,
                  IInventoryService inventoryService)
        {
            _dataContext = dataContext;
            _emailSender = emailSender;
            _notificationHub = notificationHub;
            _inventoryService = inventoryService;
        }


        public async Task<string?> CreateOrderFromSessionAsync(HttpContext httpContext, ClaimsPrincipal user, CheckoutInputViewModel model)
        {
            var paymentMethod = (model.PaymentMethod ?? "COD").Trim().ToUpperInvariant();

            if (paymentMethod == "VNPAY" || paymentMethod == "MOMO")
                throw new InvalidOperationException("Thanh toán online phải đi qua luồng reservation trước khi tạo đơn.");

            var reservationCode = await _inventoryService.ReserveCartAsync(httpContext, user, paymentMethod);

            try
            {
                return await CreateOrderFromReservationAsync(httpContext, user, model, reservationCode);
            }
            catch
            {
                await _inventoryService.ReleaseReservationAsync(reservationCode, user.FindFirstValue(ClaimTypes.NameIdentifier), "Tạo đơn COD thất bại.");
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
        public async Task<string?> CreateOrderFromReservationAsync(HttpContext httpContext, ClaimsPrincipal user, CheckoutInputViewModel model, string reservationCode)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = user.FindFirstValue(ClaimTypes.Email);
            var userName = user.Identity?.Name;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
                return null;

            var cartItems = httpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (cartItems.Count == 0)
                return null;

            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                var productIds = cartItems.Select(x => (int)x.ProductId).Distinct().ToList();

                var products = await _dataContext.Products
                    .Where(x => productIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                decimal subTotal = cartItems.Sum(x => x.Quantity * x.Price);

                decimal shippingCost = 0m;
                var shippingCookie = httpContext.Request.Cookies["ShippingPrice"];

                if (!string.IsNullOrWhiteSpace(shippingCookie))
                {
                    try
                    {
                        shippingCost = Newtonsoft.Json.JsonConvert.DeserializeObject<decimal>(shippingCookie);
                        if (shippingCost < 0) shippingCost = 0m;
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
                if (totalAmount < 0) totalAmount = 0;

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
                    TotalAmount = totalAmount,
                    PaymentMethod = model.PaymentMethod,
                    ReservationCode = reservationCode
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

                await _dataContext.SaveChangesAsync();

                await _inventoryService.CommitReservationAsync(reservationCode, orderCode, userId);

                await _dataContext.SaveChangesAsync();
                await transaction.CommitAsync();

                await _notificationHub.Clients.Group("Admins").SendAsync("NewOrderCreated", new
                {
                    orderCode = orderCode,
                    fullName = model.FullName,
                    phone = model.Phone,
                    totalAmount = totalAmount.ToString("N0"),
                    createdAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    url = $"/Admin/Order/ViewOrder?orderCode={orderCode}"
                });

                httpContext.Session.Remove("Cart");
                httpContext.Session.Remove("Coupon");
                httpContext.Session.Remove("CheckoutInfo");
                httpContext.Session.Remove("ActiveReservationCode");
                httpContext.Response.Cookies.Delete("ShippingPrice");

                return orderCode;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


    }
}