using Eshop.Models;
using Eshop.Models.ML;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace Eshop.Services
{
    public class RecommendationTrainingService
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _env;

        public RecommendationTrainingService(DataContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<string> TrainAsync()
        {
            var mlContext = new MLContext(seed: 1);

            var interactions = await BuildTrainingDataAsync();
            if (!interactions.Any())
                throw new Exception("Không có dữ liệu để train recommendation model.");

            Console.WriteLine("=== SAMPLE TRAINING DATA ===");
            foreach (var row in interactions.Take(20))
            {
                Console.WriteLine($"UserId={row.UserId}, ProductId={row.ProductId}, Label={row.Label}");
            }

            Console.WriteLine($"Interactions: {interactions.Count}");
            Console.WriteLine($"Users: {interactions.Select(x => x.UserId).Distinct().Count()}");
            Console.WriteLine($"Products: {interactions.Select(x => x.ProductId).Distinct().Count()}");

            var dataView = mlContext.Data.LoadFromEnumerable(interactions);
            var split = mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);

            var options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "UserIdEncoded",
                MatrixRowIndexColumnName = "ProductIdEncoded",
                LabelColumnName = nameof(UserProductInteraction.Label),
                LossFunction = MatrixFactorizationTrainer.LossFunctionType.SquareLossOneClass,
                Alpha = 0.01,
                Lambda = 0.025,
                C = 0.00001f,
                NumberOfIterations = 30,
                ApproximationRank = 64
            };

            var pipeline = mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "UserIdEncoded",
                    inputColumnName: nameof(UserProductInteraction.UserId))
                .Append(mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "ProductIdEncoded",
                    inputColumnName: nameof(UserProductInteraction.ProductId)))
                .Append(mlContext.Recommendation().Trainers.MatrixFactorization(options));

            var model = pipeline.Fit(split.TrainSet);

            var predictions = model.Transform(split.TestSet);
            var metrics = mlContext.Regression.Evaluate(
                predictions,
                labelColumnName: nameof(UserProductInteraction.Label),
                scoreColumnName: "Score");

            Console.WriteLine($"RMSE: {metrics.RootMeanSquaredError}");
            Console.WriteLine($"R2: {metrics.RSquared}");

            var modelFolder = Path.Combine(_env.ContentRootPath, "MLModels");
            if (!Directory.Exists(modelFolder))
                Directory.CreateDirectory(modelFolder);

            var modelPath = Path.Combine(modelFolder, "recommendation-model.zip");
            mlContext.Model.Save(model, dataView.Schema, modelPath);

            var csvPath = Path.Combine(modelFolder, "training-data.csv");
            var lines = new List<string> { "UserId,ProductId,Label" };
            lines.AddRange(interactions.Select(x => $"{x.UserId},{x.ProductId},{x.Label}"));
            await File.WriteAllLinesAsync(csvPath, lines);

            Console.WriteLine($"Model saved: {modelPath}");
            Console.WriteLine($"CSV saved: {csvPath}");

            return modelPath;
        }

        private async Task<List<UserProductInteraction>> BuildTrainingDataAsync()
        {
            var orderRows = await _context.OrderDetails
                .Include(x => x.Order)
                .Where(x => x.Order != null && !string.IsNullOrWhiteSpace(x.Order.UserId))
                .Select(x => new
                {
                    UserId = x.Order.UserId,
                    ProductId = x.ProductId,
                    Score = 5f
                })
                .ToListAsync();

            var wishlistRows = await _context.Wishlists
                .Where(x => !string.IsNullOrWhiteSpace(x.UserId))
                .Select(x => new
                {
                    UserId = x.UserId,
                    ProductId = x.ProductId,
                    Score = 3f
                })
                .ToListAsync();

            var allRows = orderRows
                .Select(x => new UserProductInteraction
                {
                    UserId = x.UserId.Trim(),
                    ProductId = x.ProductId.ToString(),
                    Label = x.Score
                })
                .Concat(
                    wishlistRows.Select(x => new UserProductInteraction
                    {
                        UserId = x.UserId.Trim(),
                        ProductId = x.ProductId.ToString(),
                        Label = x.Score
                    }))
                .GroupBy(x => new { x.UserId, x.ProductId })
                .Select(g => new UserProductInteraction
                {
                    UserId = g.Key.UserId,
                    ProductId = g.Key.ProductId,
                    Label = g.Max(x => x.Label)
                })
                .ToList();

            return allRows;
        }
    }
}