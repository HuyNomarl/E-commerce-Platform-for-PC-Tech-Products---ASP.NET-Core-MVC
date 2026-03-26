using System;
using System.Collections.Generic;
using Eshop.Models;

namespace Eshop.Views.ViewModels
{
    public class CompareItemVM
    {
        public CompareModel Compare { get; set; } = null!;
        public ProductModel Product { get; set; } = null!;
        public decimal AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public int AvailableQuantity { get; set; }
        public List<string> SummarySpecs { get; set; } = new();
        public Dictionary<string, string> SpecsByName { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SpecsByCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, decimal> NumericSpecsByCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
