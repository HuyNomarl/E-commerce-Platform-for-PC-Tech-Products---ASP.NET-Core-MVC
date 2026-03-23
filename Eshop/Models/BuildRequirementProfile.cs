namespace Eshop.Models.ViewModels
{
    public class BuildRequirementProfile
    {
        public string PrimaryPurpose { get; set; } = "";
        public string GameTitle { get; set; } = "";
        public string ResolutionTarget { get; set; } = "";
        public string PerformancePriority { get; set; } = "";
        public decimal? BudgetMin { get; set; }
        public decimal? BudgetMax { get; set; }
        public bool NeedsMonitorHighRefresh { get; set; }
        public bool NeedsStreaming { get; set; }
        public bool NeedsEditing { get; set; }
        public string PreferredBrand { get; set; } = "";
        public string Notes { get; set; } = "";
    }
}
