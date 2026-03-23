using Eshop.Models;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Services
{
    public class ProductSpecReader
    {
        private readonly DataContext _context;

        public ProductSpecReader(DataContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, ProductSpecificationModel>> GetSpecsAsync(int productId)
        {
            return await _context.ProductSpecifications
                .Include(x => x.SpecificationDefinition)
                .Where(x => x.ProductId == productId)
                .ToDictionaryAsync(x => x.SpecificationDefinition.Code, x => x);
        }

        public static string? GetText(Dictionary<string, ProductSpecificationModel> specs, string code)
            => specs.TryGetValue(code, out var s) ? s.ValueText : null;

        public static decimal? GetNumber(Dictionary<string, ProductSpecificationModel> specs, string code)
            => specs.TryGetValue(code, out var s) ? s.ValueNumber : null;

        public static bool? GetBool(Dictionary<string, ProductSpecificationModel> specs, string code)
            => specs.TryGetValue(code, out var s) ? s.ValueBool : null;

        public static List<string> GetJsonList(Dictionary<string, ProductSpecificationModel> specs, string code)
        {
            if (!specs.TryGetValue(code, out var s) || string.IsNullOrWhiteSpace(s.ValueJson))
                return new List<string>();

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(s.ValueJson!) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
