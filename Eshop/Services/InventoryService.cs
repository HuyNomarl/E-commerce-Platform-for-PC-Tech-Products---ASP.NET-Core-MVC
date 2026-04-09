using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Eshop.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly DataContext _context;
        private readonly ICartService _cartService;
        private readonly IProductCatalogRagSyncService _productCatalogRagSyncService;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(
            DataContext context,
            ICartService cartService,
            IProductCatalogRagSyncService productCatalogRagSyncService,
            ILogger<InventoryService> logger)
        {
            _context = context;
            _cartService = cartService;
            _productCatalogRagSyncService = productCatalogRagSyncService;
            _logger = logger;
        }

        public async Task<int> GetAvailableStockAsync(int productId)
        {
            return await _context.InventoryStocks
                .Where(x => x.ProductId == productId && x.Warehouse.IsActive)
                .SumAsync(x => (int?)x.OnHandQuantity - x.ReservedQuantity) ?? 0;
        }

        public async Task<int> CreateReceiptAsync(AdminInventoryReceiveViewModel vm, string? userId)
        {
            await RequireActiveWarehouseAsync(vm.WarehouseId);

            var publisher = await _context.Publishers.FirstOrDefaultAsync(x => x.Id == vm.PublisherId);
            if (publisher == null)
                throw new InvalidOperationException("Brand không tồn tại.");

            var items = NormalizeReceiveItems(vm.Items);

            if (!items.Any())
                throw new InvalidOperationException("Bạn phải nhập ít nhất 1 sản phẩm hợp lệ.");

            var productIds = items.Select(x => x.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(x => productIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name, x.PublisherId })
                .ToListAsync();

            if (products.Count != productIds.Count)
                throw new InvalidOperationException("Có sản phẩm không tồn tại.");

            var invalidProducts = products
                .Where(x => x.PublisherId != vm.PublisherId)
                .Select(x => x.Name)
                .ToList();

            if (invalidProducts.Any())
                throw new InvalidOperationException($"Các sản phẩm không thuộc brand đã chọn: {string.Join(", ", invalidProducts)}.");

            var receipt = new InventoryReceiptModel
            {
                ReceiptCode = $"PN{DateTime.Now:yyyyMMddHHmmssfff}",
                WarehouseId = vm.WarehouseId,
                PublisherId = vm.PublisherId,
                ReferenceCode = string.IsNullOrWhiteSpace(vm.ReferenceCode) ? null : vm.ReferenceCode.Trim(),
                Note = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note.Trim(),
                Status = InventoryReceiptStatus.Pending,
                CreatedByUserId = userId
            };

            foreach (var item in items)
            {
                receipt.Details.Add(new InventoryReceiptDetailModel
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost
                });
            }

            _context.InventoryReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            return receipt.Id;
        }

        public async Task ApproveReceiptAsync(int receiptId, string? userId)
        {
            var receipt = await _context.InventoryReceipts
                .Include(x => x.Publisher)
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.Id == receiptId);

            if (receipt == null)
                throw new InvalidOperationException("Không tìm thấy phiếu nhập.");

            if (receipt.Status != InventoryReceiptStatus.Pending)
                throw new InvalidOperationException("Chỉ có thể duyệt phiếu nhập đang chờ duyệt.");

            var warehouse = await RequireActiveWarehouseAsync(receipt.WarehouseId);
            var productIds = receipt.Details.Select(x => x.ProductId).Distinct().ToList();

            var transaction = await CreateTransactionAsync(
                InventoryTransactionType.Receive,
                warehouse.Id,
                receipt.ReceiptCode,
                BuildReceiptTransactionNote(receipt),
                userId,
                "NK");

            foreach (var item in receipt.Details)
            {
                var stock = await GetOrCreateStockAsync(item.ProductId, warehouse.Id);
                var beforeQty = stock.OnHandQuantity;
                stock.OnHandQuantity += item.Quantity;

                _context.InventoryTransactionDetails.Add(new InventoryTransactionDetailModel
                {
                    InventoryTransactionId = transaction.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    BeforeQuantity = beforeQty,
                    AfterQuantity = stock.OnHandQuantity,
                    UnitCost = item.UnitCost
                });
            }

            receipt.Status = InventoryReceiptStatus.Approved;
            receipt.ApprovedAt = DateTime.Now;
            receipt.ApprovedByUserId = userId;

            await _context.SaveChangesAsync();
            await SyncProductsCacheAsync(productIds);
        }

        public async Task CancelReceiptAsync(int receiptId, string? userId, string? note = null)
        {
            var receipt = await _context.InventoryReceipts.FirstOrDefaultAsync(x => x.Id == receiptId);

            if (receipt == null)
                throw new InvalidOperationException("Không tìm thấy phiếu nhập.");

            if (receipt.Status != InventoryReceiptStatus.Pending)
                throw new InvalidOperationException("Chỉ có thể hủy phiếu nhập đang chờ duyệt.");

            receipt.Status = InventoryReceiptStatus.Cancelled;
            receipt.CancelledAt = DateTime.Now;
            receipt.CancelledByUserId = userId;

            if (!string.IsNullOrWhiteSpace(note))
            {
                receipt.Note = string.IsNullOrWhiteSpace(receipt.Note)
                    ? note.Trim()
                    : $"{receipt.Note} | {note.Trim()}";
            }

            await _context.SaveChangesAsync();
        }

        public async Task AdjustAsync(AdminInventoryAdjustViewModel vm, string? userId)
        {
            await RequireActiveWarehouseAsync(vm.WarehouseId);

            var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == vm.ProductId);
            if (product == null)
                throw new InvalidOperationException("Sản phẩm không tồn tại.");

            var stock = await GetOrCreateStockAsync(vm.ProductId, vm.WarehouseId);

            if (vm.NewQuantity < stock.ReservedQuantity)
                throw new InvalidOperationException($"Không thể điều chỉnh nhỏ hơn số lượng đang giữ chỗ ({stock.ReservedQuantity}).");

            var transaction = await CreateTransactionAsync(
                InventoryTransactionType.Adjust,
                vm.WarehouseId,
                vm.ReferenceCode,
                vm.Note,
                userId,
                "DC");

            var beforeQty = stock.OnHandQuantity;
            stock.OnHandQuantity = vm.NewQuantity;

            _context.InventoryTransactionDetails.Add(new InventoryTransactionDetailModel
            {
                InventoryTransactionId = transaction.Id,
                ProductId = vm.ProductId,
                Quantity = vm.NewQuantity - beforeQty,
                BeforeQuantity = beforeQty,
                AfterQuantity = stock.OnHandQuantity
            });

            await _context.SaveChangesAsync();
            await SyncProductQuantityCacheAsync(vm.ProductId);
        }

        public async Task TransferAsync(AdminInventoryTransferViewModel vm, string? userId)
        {
            if (vm.FromWarehouseId == vm.ToWarehouseId)
                throw new InvalidOperationException("Kho nguồn và kho đích không được trùng nhau.");

            var source = await RequireActiveWarehouseAsync(vm.FromWarehouseId);
            var target = await RequireActiveWarehouseAsync(vm.ToWarehouseId);
            var items = NormalizeTransferItems(vm.Items);

            if (!items.Any())
                throw new InvalidOperationException("Bạn phải nhập ít nhất 1 sản phẩm hợp lệ.");

            var productIds = items.Select(x => x.ProductId).Distinct().ToList();
            var products = await _context.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

            if (products.Count != productIds.Count)
                throw new InvalidOperationException("Có sản phẩm không tồn tại.");

            foreach (var item in items)
            {
                var sourceStock = await GetOrCreateStockAsync(item.ProductId, source.Id);
                var available = sourceStock.OnHandQuantity - sourceStock.ReservedQuantity;

                if (available < item.Quantity)
                    throw new InvalidOperationException($"Sản phẩm \"{products[item.ProductId].Name}\" không đủ tồn ở kho nguồn.");
            }

            var refCode = string.IsNullOrWhiteSpace(vm.ReferenceCode)
                ? "CH-" + DateTime.Now.ToString("yyyyMMddHHmmss")
                : vm.ReferenceCode.Trim();

            var issueTx = await CreateTransactionAsync(
                InventoryTransactionType.Transfer,
                source.Id,
                refCode,
                $"Chuyển kho đi. {vm.Note}",
                userId,
                "CKX");

            var receiveTx = await CreateTransactionAsync(
                InventoryTransactionType.Transfer,
                target.Id,
                refCode,
                $"Chuyển kho đến. {vm.Note}",
                userId,
                "CKN");

            foreach (var item in items)
            {
                var sourceStock = await GetOrCreateStockAsync(item.ProductId, source.Id);
                var targetStock = await GetOrCreateStockAsync(item.ProductId, target.Id);

                var sourceBefore = sourceStock.OnHandQuantity;
                sourceStock.OnHandQuantity -= item.Quantity;

                var targetBefore = targetStock.OnHandQuantity;
                targetStock.OnHandQuantity += item.Quantity;

                _context.InventoryTransactionDetails.Add(new InventoryTransactionDetailModel
                {
                    InventoryTransactionId = issueTx.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    BeforeQuantity = sourceBefore,
                    AfterQuantity = sourceStock.OnHandQuantity
                });

                _context.InventoryTransactionDetails.Add(new InventoryTransactionDetailModel
                {
                    InventoryTransactionId = receiveTx.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    BeforeQuantity = targetBefore,
                    AfterQuantity = targetStock.OnHandQuantity
                });
            }

            await _context.SaveChangesAsync();
            await SyncProductsCacheAsync(productIds);
        }

        public async Task BootstrapLegacyStockAsync(string? userId)
        {
            var defaultWarehouse = await EnsureDefaultWarehouseAsync();

            var products = await _context.Products
                .Where(x => x.Quantity > 0)
                .ToListAsync();

            foreach (var product in products)
            {
                var hasAnyStock = await _context.InventoryStocks.AnyAsync(x => x.ProductId == product.Id);
                if (hasAnyStock)
                    continue;

                var transaction = await CreateTransactionAsync(
                    InventoryTransactionType.Receive,
                    defaultWarehouse.Id,
                    "BOOTSTRAP-LEGACY",
                    "Khởi tạo tồn kho từ Product.Quantity cũ",
                    userId,
                    "BS");

                var stock = await GetOrCreateStockAsync(product.Id, defaultWarehouse.Id);
                var beforeQty = stock.OnHandQuantity;
                stock.OnHandQuantity += product.Quantity;

                _context.InventoryTransactionDetails.Add(new InventoryTransactionDetailModel
                {
                    InventoryTransactionId = transaction.Id,
                    ProductId = product.Id,
                    Quantity = product.Quantity,
                    BeforeQuantity = beforeQty,
                    AfterQuantity = stock.OnHandQuantity
                });
            }

            await _context.SaveChangesAsync();
            await SyncProductsCacheAsync(products.Select(x => x.Id).ToList());
        }

        public async Task CleanupExpiredReservationsAsync(string? userId = null)
        {
            var expiredCodes = await _context.InventoryReservations
                .Where(x => x.Status == InventoryReservationStatus.Active && x.ExpiresAt <= DateTime.Now)
                .Select(x => x.ReservationCode)
                .ToListAsync();

            foreach (var code in expiredCodes)
            {
                await ReleaseReservationAsync(code, userId, "Reservation hết hạn", true);
            }
        }

        public async Task<string> ReserveCartAsync(
            HttpContext httpContext,
            ClaimsPrincipal user,
            string paymentMethod,
            IReadOnlyCollection<CartItemModel>? cartItems = null,
            int expireMinutes = 20)
        {
            await CleanupExpiredReservationsAsync(user.FindFirstValue(ClaimTypes.NameIdentifier));

            var currentCart = cartItems?
                .Where(x => x != null && x.ProductId > 0 && x.Quantity > 0)
                .ToList()
                ?? await _cartService.GetCartAsync(httpContext);

            if (!currentCart.Any())
                throw new InvalidOperationException("Giỏ hàng đang trống.");

            var sessionId = httpContext.Session.Id;
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            var oldReservations = await _context.InventoryReservations
                .Where(x => x.SessionId == sessionId && x.Status == InventoryReservationStatus.Active)
                .Select(x => x.ReservationCode)
                .ToListAsync();

            foreach (var code in oldReservations)
            {
                await ReleaseReservationAsync(code, userId, "Giữ chỗ cũ bị thay thế.");
            }

            var requestedQtyByProduct = currentCart
                .GroupBy(x => (int)x.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var productIds = requestedQtyByProduct.Keys.ToList();
            var products = await _context.Products
                .Where(x => productIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            if (products.Count != productIds.Count)
                throw new InvalidOperationException("Có sản phẩm trong giỏ không còn tồn tại.");

            var stocks = await _context.InventoryStocks
                .Include(x => x.Warehouse)
                .Where(x => productIds.Contains(x.ProductId) && x.Warehouse.IsActive)
                .OrderByDescending(x => x.Warehouse.IsDefault)
                .ThenByDescending(x => x.OnHandQuantity - x.ReservedQuantity)
                .ThenBy(x => x.WarehouseId)
                .ToListAsync();

            foreach (var row in requestedQtyByProduct)
            {
                var totalAvailable = stocks
                    .Where(x => x.ProductId == row.Key)
                    .Sum(x => x.OnHandQuantity - x.ReservedQuantity);

                if (totalAvailable < row.Value)
                    throw new InvalidOperationException($"Sản phẩm \"{products[row.Key].Name}\" chỉ còn {totalAvailable}, không đủ số lượng bạn đặt ({row.Value}).");
            }

            var reservation = new InventoryReservationModel
            {
                ReservationCode = "RS" + DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                SessionId = sessionId,
                UserId = userId,
                PaymentMethod = paymentMethod,
                Status = InventoryReservationStatus.Active,
                ExpiresAt = DateTime.Now.AddMinutes(expireMinutes),
                Note = $"Giữ chỗ từ giỏ hàng - {paymentMethod}"
            };

            _context.InventoryReservations.Add(reservation);
            await _context.SaveChangesAsync();

            foreach (var row in requestedQtyByProduct)
            {
                int remain = row.Value;
                var productStocks = stocks.Where(x => x.ProductId == row.Key).ToList();

                foreach (var stock in productStocks)
                {
                    if (remain <= 0) break;

                    var available = stock.OnHandQuantity - stock.ReservedQuantity;
                    if (available <= 0) continue;

                    var reserveQty = Math.Min(available, remain);

                    stock.ReservedQuantity += reserveQty;
                    remain -= reserveQty;

                    _context.InventoryReservationDetails.Add(new InventoryReservationDetailModel
                    {
                        InventoryReservationId = reservation.Id,
                        ProductId = row.Key,
                        WarehouseId = stock.WarehouseId,
                        Quantity = reserveQty
                    });
                }

                if (remain > 0)
                    throw new InvalidOperationException($"Không thể giữ chỗ đầy đủ cho sản phẩm \"{products[row.Key].Name}\".");
            }

            await _context.SaveChangesAsync();
            await SyncProductsCacheAsync(productIds);

            httpContext.Session.SetString("ActiveReservationCode", reservation.ReservationCode);
            return reservation.ReservationCode;
        }

        public async Task CommitReservationAsync(string reservationCode, string orderCode, string? userId)
        {
            var reservation = await _context.InventoryReservations
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.ReservationCode == reservationCode);

            if (reservation == null)
                throw new InvalidOperationException("Không tìm thấy reservation.");

            if (reservation.Status != InventoryReservationStatus.Active)
                throw new InvalidOperationException("Reservation không còn hiệu lực.");

            if (reservation.ExpiresAt <= DateTime.Now)
            {
                await ReleaseReservationAsync(reservationCode, userId, "Reservation hết hạn khi commit.", true);
                throw new InvalidOperationException("Reservation đã hết hạn.");
            }

            var productIds = reservation.Details.Select(x => x.ProductId).Distinct().ToList();
            var products = await _context.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

            var groups = reservation.Details.GroupBy(x => x.WarehouseId).ToList();

            foreach (var warehouseGroup in groups)
            {
                var tx = await CreateTransactionAsync(
                    InventoryTransactionType.Issue,
                    warehouseGroup.Key,
                    orderCode,
                    $"Xuất kho từ reservation {reservation.ReservationCode}",
                    userId,
                    "XK");

                foreach (var detail in warehouseGroup)
                {
                    var stock = await GetOrCreateStockAsync(detail.ProductId, detail.WarehouseId);

                    if (stock.ReservedQuantity < detail.Quantity)
                        throw new InvalidOperationException("ReservedQuantity không đủ để commit đơn hàng.");

                    if (stock.OnHandQuantity < detail.Quantity)
                        throw new InvalidOperationException("OnHandQuantity không đủ để commit đơn hàng.");

                    var beforeQty = stock.OnHandQuantity;

                    stock.ReservedQuantity -= detail.Quantity;
                    stock.OnHandQuantity -= detail.Quantity;

                    _context.InventoryTransactionDetails.Add(new InventoryTransactionDetailModel
                    {
                        InventoryTransactionId = tx.Id,
                        ProductId = detail.ProductId,
                        Quantity = detail.Quantity,
                        BeforeQuantity = beforeQty,
                        AfterQuantity = stock.OnHandQuantity
                    });

                    if (products.TryGetValue(detail.ProductId, out var product))
                        product.Sold += detail.Quantity;
                }
            }

            reservation.Status = InventoryReservationStatus.Confirmed;
            reservation.OrderCode = orderCode;
            reservation.ConfirmedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            await SyncProductsCacheAsync(productIds);
        }

        public async Task ReleaseReservationAsync(string reservationCode, string? userId, string? note = null, bool expired = false)
        {
            var reservation = await _context.InventoryReservations
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.ReservationCode == reservationCode);

            if (reservation == null)
                return;

            if (reservation.Status != InventoryReservationStatus.Active)
                return;

            var productIds = new HashSet<int>();

            foreach (var detail in reservation.Details)
            {
                var stock = await _context.InventoryStocks
                    .FirstOrDefaultAsync(x => x.ProductId == detail.ProductId && x.WarehouseId == detail.WarehouseId);

                if (stock != null)
                {
                    stock.ReservedQuantity = Math.Max(0, stock.ReservedQuantity - detail.Quantity);
                    productIds.Add(detail.ProductId);
                }
            }

            reservation.Status = expired
                ? InventoryReservationStatus.Expired
                : InventoryReservationStatus.Released;

            reservation.ReleasedAt = DateTime.Now;
            reservation.Note = string.IsNullOrWhiteSpace(note)
                ? reservation.Note
                : $"{reservation.Note} | {note}";

            await _context.SaveChangesAsync();
            await SyncProductsCacheAsync(productIds.ToList());
        }

        public async Task SyncWarehouseProductsAsync(int warehouseId)
        {
            var productIds = await _context.InventoryStocks
                .Where(x => x.WarehouseId == warehouseId)
                .Select(x => x.ProductId)
                .Distinct()
                .ToListAsync();

            if (!productIds.Any())
                return;

            await SyncProductsCacheAsync(productIds);
        }

        public async Task IssueOrderAsync(string orderCode, Dictionary<int, int> requestedQtyByProduct, string? userId)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
                throw new InvalidOperationException("Thiếu mã đơn hàng.");

            if (!requestedQtyByProduct.Any())
                throw new InvalidOperationException("Đơn hàng không có sản phẩm.");

            var productIds = requestedQtyByProduct.Keys.ToList();
            var products = await _context.Products
                .Where(x => productIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            if (products.Count != productIds.Count)
                throw new InvalidOperationException("Có sản phẩm không tồn tại trong đơn hàng.");

            var stocks = await _context.InventoryStocks
                .Include(x => x.Warehouse)
                .Where(x => productIds.Contains(x.ProductId) && x.Warehouse.IsActive)
                .OrderByDescending(x => x.Warehouse.IsDefault)
                .ThenByDescending(x => x.OnHandQuantity - x.ReservedQuantity)
                .ThenBy(x => x.WarehouseId)
                .ToListAsync();

            foreach (var row in requestedQtyByProduct)
            {
                var totalAvailable = stocks
                    .Where(x => x.ProductId == row.Key)
                    .Sum(x => x.OnHandQuantity - x.ReservedQuantity);

                if (totalAvailable < row.Value)
                    throw new InvalidOperationException($"Sản phẩm \"{products[row.Key].Name}\" chỉ còn {totalAvailable}, không đủ số lượng bạn đặt ({row.Value}).");
            }

            var warehouseIssueMap = new Dictionary<int, List<(int ProductId, int Qty, int Before, int After)>>();

            foreach (var row in requestedQtyByProduct)
            {
                int remain = row.Value;
                var productStocks = stocks.Where(x => x.ProductId == row.Key).ToList();

                foreach (var stock in productStocks)
                {
                    if (remain <= 0) break;

                    var available = stock.OnHandQuantity - stock.ReservedQuantity;
                    if (available <= 0) continue;

                    var issueQty = Math.Min(available, remain);
                    var beforeQty = stock.OnHandQuantity;

                    stock.OnHandQuantity -= issueQty;
                    remain -= issueQty;

                    if (!warehouseIssueMap.ContainsKey(stock.WarehouseId))
                        warehouseIssueMap[stock.WarehouseId] = new List<(int, int, int, int)>();

                    warehouseIssueMap[stock.WarehouseId].Add((row.Key, issueQty, beforeQty, stock.OnHandQuantity));
                }

                if (remain > 0)
                    throw new InvalidOperationException($"Không thể phân bổ tồn kho cho sản phẩm \"{products[row.Key].Name}\".");
            }

            foreach (var warehouseGroup in warehouseIssueMap)
            {
                var tx = await CreateTransactionAsync(
                    InventoryTransactionType.Issue,
                    warehouseGroup.Key,
                    orderCode,
                    "Xuất kho theo đơn hàng",
                    userId,
                    "XK");

                foreach (var item in warehouseGroup.Value)
                {
                    _context.InventoryTransactionDetails.Add(new InventoryTransactionDetailModel
                    {
                        InventoryTransactionId = tx.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Qty,
                        BeforeQuantity = item.Before,
                        AfterQuantity = item.After
                    });

                    products[item.ProductId].Sold += item.Qty;
                }
            }

            await _context.SaveChangesAsync();
            await SyncProductsCacheAsync(productIds);
        }

        public async Task ReturnOrderAsync(string orderCode, string? userId, string? note = null)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
                throw new InvalidOperationException("Thiếu mã đơn hàng.");

            var issuedTransactions = await _context.InventoryTransactions
                .Include(x => x.Details)
                .Where(x => x.ReferenceCode == orderCode && x.TransactionType == InventoryTransactionType.Issue)
                .ToListAsync();

            if (!issuedTransactions.Any())
                throw new InvalidOperationException("Không tìm thấy lịch sử xuất kho của đơn hàng.");

            var returnedAlready = await _context.InventoryTransactions
                .AnyAsync(x => x.ReferenceCode == orderCode && x.TransactionType == InventoryTransactionType.Return);

            if (returnedAlready)
                throw new InvalidOperationException("Đơn hàng này đã được hoàn kho trước đó.");

            var affectedProductIds = new HashSet<int>();

            foreach (var issueTx in issuedTransactions)
            {
                var returnTx = await CreateTransactionAsync(
                    InventoryTransactionType.Return,
                    issueTx.WarehouseId,
                    orderCode,
                    note ?? "Hoàn kho từ đơn hàng bị hủy",
                    userId,
                    "HK");

                foreach (var detail in issueTx.Details)
                {
                    var stock = await GetOrCreateStockAsync(detail.ProductId, issueTx.WarehouseId);
                    var beforeQty = stock.OnHandQuantity;

                    stock.OnHandQuantity += detail.Quantity;
                    affectedProductIds.Add(detail.ProductId);

                    _context.InventoryTransactionDetails.Add(new InventoryTransactionDetailModel
                    {
                        InventoryTransactionId = returnTx.Id,
                        ProductId = detail.ProductId,
                        Quantity = detail.Quantity,
                        BeforeQuantity = beforeQty,
                        AfterQuantity = stock.OnHandQuantity
                    });

                    var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == detail.ProductId);
                    if (product != null)
                        product.Sold = Math.Max(0, product.Sold - detail.Quantity);
                }
            }

            await _context.SaveChangesAsync();
            await SyncProductsCacheAsync(affectedProductIds.ToList());
        }

        public async Task RevertOrderInventoryAsync(string orderCode, string? reservationCode, string? userId, string? note = null)
        {
            if (!string.IsNullOrWhiteSpace(reservationCode))
            {
                var reservation = await _context.InventoryReservations
                    .FirstOrDefaultAsync(x => x.ReservationCode == reservationCode);

                if (reservation?.Status == InventoryReservationStatus.Active)
                {
                    await ReleaseReservationAsync(reservationCode, userId, note);
                    return;
                }
            }

            var hasIssueTransaction = await _context.InventoryTransactions
                .AnyAsync(x => x.ReferenceCode == orderCode && x.TransactionType == InventoryTransactionType.Issue);

            if (!hasIssueTransaction)
                return;

            var returnedAlready = await _context.InventoryTransactions
                .AnyAsync(x => x.ReferenceCode == orderCode && x.TransactionType == InventoryTransactionType.Return);

            if (returnedAlready)
                return;

            await ReturnOrderAsync(orderCode, userId, note);
        }

        private async Task<WarehouseModel> EnsureDefaultWarehouseAsync()
        {
            var defaultWarehouse = await _context.Warehouses
                .FirstOrDefaultAsync(x => x.IsDefault && x.IsActive);

            if (defaultWarehouse != null)
                return defaultWarehouse;

            defaultWarehouse = new WarehouseModel
            {
                Code = "KHO-TONG",
                Name = "Kho Tổng",
                Address = "Kho mặc định của hệ thống",
                IsDefault = true,
                IsActive = true
            };

            _context.Warehouses.Add(defaultWarehouse);
            await _context.SaveChangesAsync();

            return defaultWarehouse;
        }

        private async Task<WarehouseModel> RequireActiveWarehouseAsync(int warehouseId)
        {
            var warehouse = await _context.Warehouses
                .FirstOrDefaultAsync(x => x.Id == warehouseId && x.IsActive);

            if (warehouse == null)
                throw new InvalidOperationException("Kho không tồn tại hoặc đã ngưng hoạt động.");

            return warehouse;
        }

        private async Task<InventoryStockModel> GetOrCreateStockAsync(int productId, int warehouseId)
        {
            var stock = await _context.InventoryStocks
                .FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId);

            if (stock != null)
                return stock;

            stock = new InventoryStockModel
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                OnHandQuantity = 0,
                ReservedQuantity = 0
            };

            _context.InventoryStocks.Add(stock);
            return stock;
        }

        private async Task<InventoryTransactionModel> CreateTransactionAsync(
            InventoryTransactionType type,
            int warehouseId,
            string? referenceCode,
            string? note,
            string? userId,
            string prefix)
        {
            var transaction = new InventoryTransactionModel
            {
                TransactionCode = $"{prefix}{DateTime.Now:yyyyMMddHHmmssfff}",
                TransactionType = type,
                WarehouseId = warehouseId,
                ReferenceCode = string.IsNullOrWhiteSpace(referenceCode) ? null : referenceCode.Trim(),
                Note = note,
                CreatedByUserId = userId
            };

            _context.InventoryTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return transaction;
        }

        private static string BuildReceiptTransactionNote(InventoryReceiptModel receipt)
        {
            var parts = new List<string>
            {
                $"Duyệt phiếu nhập {receipt.ReceiptCode}"
            };

            if (!string.IsNullOrWhiteSpace(receipt.ReferenceCode))
                parts.Add($"Mã tham chiếu: {receipt.ReferenceCode.Trim()}");

            if (receipt.Publisher != null && !string.IsNullOrWhiteSpace(receipt.Publisher.Name))
                parts.Add($"Brand: {receipt.Publisher.Name.Trim()}");

            if (!string.IsNullOrWhiteSpace(receipt.Note))
                parts.Add(receipt.Note.Trim());

            var note = string.Join(" | ", parts);
            return note.Length <= 1000 ? note : note[..1000];
        }

        private List<AdminInventoryReceiveItemViewModel> NormalizeReceiveItems(List<AdminInventoryReceiveItemViewModel> items)
        {
            return items
                .Where(x => x.ProductId > 0 && x.Quantity > 0)
                .GroupBy(x => x.ProductId)
                .Select(g => new AdminInventoryReceiveItemViewModel
                {
                    ProductId = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    UnitCost = g.LastOrDefault(x => x.UnitCost.HasValue)?.UnitCost
                })
                .ToList();
        }

        private List<AdminInventoryTransferItemViewModel> NormalizeTransferItems(List<AdminInventoryTransferItemViewModel> items)
        {
            return items
                .Where(x => x.ProductId > 0 && x.Quantity > 0)
                .GroupBy(x => x.ProductId)
                .Select(g => new AdminInventoryTransferItemViewModel
                {
                    ProductId = g.Key,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToList();
        }

        private async Task SyncProductQuantityCacheAsync(int productId)
        {
            var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == productId);
            if (product == null) return;

            var available = await GetAvailableStockAsync(productId);
            product.Quantity = available < 0 ? 0 : available;

            await _context.SaveChangesAsync();
            await TrySyncProductRagAsync(productId);
        }

        private async Task SyncProductsCacheAsync(List<int> productIds)
        {
            productIds = productIds.Distinct().ToList();
            var products = await _context.Products.Where(x => productIds.Contains(x.Id)).ToListAsync();

            foreach (var product in products)
            {
                var available = await GetAvailableStockAsync(product.Id);
                product.Quantity = available < 0 ? 0 : available;
            }

            await _context.SaveChangesAsync();

            foreach (var product in products)
            {
                await TrySyncProductRagAsync(product.Id);
            }
        }

        private async Task TrySyncProductRagAsync(int productId)
        {
            try
            {
                var synced = await _productCatalogRagSyncService.SyncProductAsync(productId);
                if (!synced)
                {
                    _logger.LogWarning("Dong bo ton kho sang RAG that bai cho product {ProductId}.", productId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dong bo ton kho sang RAG loi cho product {ProductId}.", productId);
            }
        }
    }
}
