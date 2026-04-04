using Eshop.Models;
using Eshop.Models.ML;
using Eshop.Helpers;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML;

namespace Eshop.Services
{
    public class RecommendationPredictService
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<RecommendationPredictService> _logger;
        private readonly MLContext _mlContext;
        private ITransformer? _model;
        private string _modelPath = string.Empty;

        public RecommendationPredictService(
            DataContext context,
            IWebHostEnvironment env,
            ILogger<RecommendationPredictService> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
            _mlContext = new MLContext();

            LoadModel();
        }

        private void LoadModel()
        {
            _modelPath = Path.Combine(_env.ContentRootPath, "MLModels", "recommendation-model.zip");

            if (!File.Exists(_modelPath))
            {
                _model = null;
                _logger.LogWarning("Recommendation model not found. Path: {ModelPath}", _modelPath);
                return;
            }

            try
            {
                using var stream = new FileStream(_modelPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _model = _mlContext.Model.Load(stream, out _);

                var fileInfo = new FileInfo(_modelPath);
                _logger.LogInformation(
                    "Recommendation model loaded successfully. Path: {ModelPath}, Size: {Size} bytes, LastWriteTime: {LastWriteTime}",
                    _modelPath,
                    fileInfo.Length,
                    fileInfo.LastWriteTime);
            }
            catch (Exception ex)
            {
                _model = null;
                _logger.LogError(ex, "Failed to load recommendation model. Path: {ModelPath}", _modelPath);
            }
        }

        public async Task<List<ProductModel>> RecommendAsync(string userId, int top = 8)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var startedAt = DateTime.Now;

            _logger.LogInformation(
                "=== Recommendation request started === RequestId={RequestId}, UserId={UserId}, Top={Top}",
                requestId, userId, top);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning(
                    "RequestId={RequestId}: UserId is empty. Using fallback recommendation.",
                    requestId);

                return await GetFallbackProductsAsync(top, requestId, "EmptyUserId");
            }

            if (_model == null)
            {
                _logger.LogWarning(
                    "RequestId={RequestId}: Model is null. Using fallback recommendation. ModelPath={ModelPath}",
                    requestId, _modelPath);

                return await GetFallbackProductsAsync(top, requestId, "ModelNull");
            }

            userId = userId.Trim();

            var boughtIds = await _context.OrderDetails
                .Include(x => x.Order)
                .Where(x => x.Order.UserId == userId)
                .Select(x => x.ProductId)
                .Distinct()
                .ToListAsync();

            var wishlistIds = await _context.Wishlists
                .Where(x => x.UserId == userId)
                .Select(x => x.ProductId)
                .Distinct()
                .ToListAsync();

            var excludedIds = boughtIds.Concat(wishlistIds).Distinct().ToList();

            _logger.LogInformation(
                "RequestId={RequestId}: BoughtCount={BoughtCount}, WishlistCount={WishlistCount}, ExcludedCount={ExcludedCount}",
                requestId,
                boughtIds.Count,
                wishlistIds.Count,
                excludedIds.Count);

            if (boughtIds.Any())
            {
                _logger.LogInformation(
                    "RequestId={RequestId}: Sample bought product ids: {BoughtIds}",
                    requestId,
                    string.Join(", ", boughtIds.Take(10)));
            }

            if (wishlistIds.Any())
            {
                _logger.LogInformation(
                    "RequestId={RequestId}: Sample wishlist product ids: {WishlistIds}",
                    requestId,
                    string.Join(", ", wishlistIds.Take(10)));
            }

            var candidateProducts = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.ProductImages)
                .WhereVisibleOnStorefront(_context)
                .Where(p => p.Quantity > 0 && !excludedIds.Contains(p.Id))
                .ToListAsync();

            _logger.LogInformation(
                "RequestId={RequestId}: CandidateProductsCount={CandidateProductsCount}",
                requestId,
                candidateProducts.Count);

            if (!candidateProducts.Any())
            {
                _logger.LogWarning(
                    "RequestId={RequestId}: No candidate products found after exclusion. Using fallback recommendation.",
                    requestId);

                return await GetFallbackProductsAsync(top, requestId, "NoCandidates");
            }

            var engine = _mlContext.Model
                .CreatePredictionEngine<UserProductInteraction, ProductRecommendationPrediction>(_model);

            var scored = candidateProducts
                .Select(p => new
                {
                    Product = p,
                    Score = engine.Predict(new UserProductInteraction
                    {
                        UserId = userId,
                        ProductId = p.Id.ToString(),
                        Label = 0
                    }).Score
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            _logger.LogInformation(
                "RequestId={RequestId}: Scoring completed. TotalScored={TotalScored}",
                requestId,
                scored.Count);

            foreach (var item in scored.Take(10))
            {
                _logger.LogInformation(
                    "RequestId={RequestId}: Predicted ProductId={ProductId}, Name={ProductName}, Score={Score}, Category={Category}, Publisher={Publisher}, Price={Price}",
                    requestId,
                    item.Product.Id,
                    item.Product.Name,
                    item.Score,
                    item.Product.Category?.Name,
                    item.Product.Publisher?.Name,
                    item.Product.Price);
            }

            var result = scored
                .Take(top)
                .Select(x => x.Product)
                .ToList();

            if (!result.Any())
            {
                _logger.LogWarning(
                    "RequestId={RequestId}: Result empty after scoring. Using fallback recommendation.",
                    requestId);

                return await GetFallbackProductsAsync(top, requestId, "EmptyScoredResult");
            }

            foreach (var product in result)
            {
                _logger.LogInformation(
                    "RequestId={RequestId}: Recommended ProductId={ProductId}, ProductName={ProductName} for UserId={UserId}",
                    requestId,
                    product.Id,
                    product.Name,
                    userId);
            }

            _logger.LogInformation(
                "RequestId={RequestId}: UserId={UserId} was recommended products: {RecommendedProducts}",
                requestId,
                userId,
                string.Join(" | ", result.Select(p => $"[{p.Id}] {p.Name}")));

            var elapsed = DateTime.Now - startedAt;
            _logger.LogInformation(
                "=== Recommendation request finished === RequestId={RequestId}, ResultCount={ResultCount}, DurationMs={DurationMs}",
                requestId,
                result.Count,
                elapsed.TotalMilliseconds);

            return result;
        }

        private async Task<List<ProductModel>> GetFallbackProductsAsync(int top, string requestId, string reason)
        {
            _logger.LogInformation(
                "RequestId={RequestId}: Loading fallback products. Reason={Reason}, Top={Top}",
                requestId,
                reason,
                top);

            var fallback = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.ProductImages)
                .WhereVisibleOnStorefront(_context)
                .Where(p => p.Quantity > 0)
                .OrderByDescending(p => p.Sold)
                .ThenByDescending(p => p.Id)
                .Take(top)
                .ToListAsync();

            foreach (var item in fallback)
            {
                _logger.LogInformation(
                    "RequestId={RequestId}: Fallback ProductId={ProductId}, Name={ProductName}, Sold={Sold}, Price={Price}",
                    requestId,
                    item.Id,
                    item.Name,
                    item.Sold,
                    item.Price);
            }

            return fallback;
        }
    }
}
