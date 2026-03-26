using System.Collections.Generic;

namespace Eshop.Views.ViewModels
{
    public class ComparePageVM
    {
        public List<CompareItemVM> Items { get; set; } = new();
        public List<CompareSectionVM> Sections { get; set; } = new();
        public string ModeTitle { get; set; } = "So sánh sản phẩm";
        public string ModeDescription { get; set; } = string.Empty;
        public bool HasMixedComponentTypes { get; set; }
    }

    public class CompareSectionVM
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<CompareRowVM> Rows { get; set; } = new();
    }

    public class CompareRowVM
    {
        public string Label { get; set; } = string.Empty;
        public string? Hint { get; set; }
        public string Preference { get; set; } = "neutral";
        public List<CompareCellVM> Cells { get; set; } = new();
    }

    public class CompareCellVM
    {
        public string Value { get; set; } = "-";
        public decimal? NumericValue { get; set; }
        public bool IsComparable { get; set; }
        public bool IsBest { get; set; }
    }
}
