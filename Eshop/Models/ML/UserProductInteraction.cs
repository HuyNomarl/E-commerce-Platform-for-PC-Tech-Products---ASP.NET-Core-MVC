namespace Eshop.Models.ML
{
    public class UserProductInteraction
    {
        public string UserId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public float Label { get; set; }
    }

    public class ProductRecommendationPrediction
    {
        public float Score { get; set; }
    }
}