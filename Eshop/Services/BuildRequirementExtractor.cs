using Eshop.Models.ViewModels;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Eshop.Services
{
    public class BuildRequirementExtractor : IBuildRequirementExtractor
    {
        private static readonly Regex MaxBudgetRegex = new(
            @"(?:duoi|toi da|khong qua|max|under|<=?)\s*(\d+(?:[.,]\d+)?)\s*(ty|tỷ|trieu|triệu|tr|cu|củ|k|nghin|ngàn)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MinBudgetRegex = new(
            @"(?:tren|tu|it nhat|min|>=?)\s*(\d+(?:[.,]\d+)?)\s*(ty|tỷ|trieu|triệu|tr|cu|củ|k|nghin|ngàn)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex GenericBudgetRegex = new(
            @"(?<!\d)(\d+(?:[.,]\d+)?)\s*(ty|tỷ|trieu|triệu|tr|cu|củ)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ILlmChatClient _llmChatClient;

        public BuildRequirementExtractor(ILlmChatClient llmChatClient)
        {
            _llmChatClient = llmChatClient;
        }

        public async Task<BuildRequirementProfile> ExtractAsync(string userMessage)
        {
            var systemPrompt = """
                    Ban la bo phan trich xuat nhu cau build PC.
                    Hay tra ve JSON hop le theo schema:

                    {
                      "PrimaryPurpose": "",
                      "GameTitle": "",
                      "ResolutionTarget": "",
                      "PerformancePriority": "",
                      "BudgetMin": null,
                      "BudgetMax": null,
                      "NeedsMonitorHighRefresh": false,
                      "NeedsStreaming": false,
                      "NeedsEditing": false,
                      "PreferredBrand": "",
                      "Notes": ""
                    }

                    Chi tra ve JSON, khong giai thich.
                    """;

            BuildRequirementProfile profile;

            try
            {
                var raw = await _llmChatClient.AskAsync(systemPrompt, userMessage);
                var json = ExtractJson(raw);

                profile = JsonSerializer.Deserialize<BuildRequirementProfile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new BuildRequirementProfile();
            }
            catch
            {
                profile = new BuildRequirementProfile();
            }

            ApplyHeuristics(profile, userMessage);
            profile.Notes = string.IsNullOrWhiteSpace(profile.Notes) ? userMessage : profile.Notes;
            return profile;
        }

        private static void ApplyHeuristics(BuildRequirementProfile profile, string userMessage)
        {
            var normalized = NormalizeText(userMessage);

            var maxBudget = TryMatchBudget(MaxBudgetRegex, normalized);
            var minBudget = TryMatchBudget(MinBudgetRegex, normalized);
            var genericBudget = TryMatchBudget(GenericBudgetRegex, normalized);

            if (maxBudget.HasValue)
            {
                profile.BudgetMax = maxBudget;
            }
            else if (!profile.BudgetMax.HasValue && genericBudget.HasValue)
            {
                profile.BudgetMax = genericBudget;
            }

            if (minBudget.HasValue)
            {
                profile.BudgetMin = minBudget;
            }

            if (string.IsNullOrWhiteSpace(profile.ResolutionTarget))
            {
                if (normalized.Contains("1440p", StringComparison.Ordinal)
                    || normalized.Contains("2k", StringComparison.Ordinal))
                {
                    profile.ResolutionTarget = "1440p";
                }
                else if (normalized.Contains("1080p", StringComparison.Ordinal)
                    || normalized.Contains("full hd", StringComparison.Ordinal))
                {
                    profile.ResolutionTarget = "1080p";
                }
                else if (normalized.Contains("4k", StringComparison.Ordinal))
                {
                    profile.ResolutionTarget = "4K";
                }
            }

            if (string.IsNullOrWhiteSpace(profile.PrimaryPurpose))
            {
                if (ContainsAny(normalized, "choi game", "gaming", "fps", "esports"))
                {
                    profile.PrimaryPurpose = "Gaming";
                }
                else if (ContainsAny(normalized, "ai", "machine learning", "deep learning", "llm", "stable diffusion", "train model"))
                {
                    profile.PrimaryPurpose = "AI / Machine Learning";
                }
                else if (ContainsAny(normalized, "cad", "revit", "autocad", "solidworks", "sketchup", "blender", "3d cad"))
                {
                    profile.PrimaryPurpose = "CAD / 3D";
                }
                else if (ContainsAny(normalized, "photoshop", "lightroom", "edit anh", "chinh sua anh", "photo"))
                {
                    profile.PrimaryPurpose = "Photo Editing";
                }
                else if (ContainsAny(normalized, "server", "nas", "home server", "luu tru", "backup", "plex"))
                {
                    profile.PrimaryPurpose = "Home Server";
                }
                else if (ContainsAny(normalized, "trading", "chung khoan", "da man hinh", "multi monitor", "nhieu man hinh"))
                {
                    profile.PrimaryPurpose = "Trading / Multi Monitor";
                }
                else if (ContainsAny(normalized, "studio", "thu am", "lam nhac", "mix nhac", "producer", "daw", "fl studio", "ableton", "cubase", "pro tools", "logic pro", "audio workstation"))
                {
                    profile.PrimaryPurpose = "Music Production";
                }
                else if (ContainsAny(normalized, "van phong", "office", "word", "excel", "powerpoint", "ketoan"))
                {
                    profile.PrimaryPurpose = "Office";
                }
                else if (ContainsAny(normalized, "hoc tap", "hoc online", "zoom", "teams", "google meet", "sinh vien"))
                {
                    profile.PrimaryPurpose = "Study";
                }
                else if (ContainsAny(normalized, "nghe nhac", "xem phim", "giai tri", "spotify", "youtube", "media", "htpc"))
                {
                    profile.PrimaryPurpose = "Home Entertainment";
                }
                else if (ContainsAny(normalized, "lap trinh", "code", "coding", "dev", "programming"))
                {
                    profile.PrimaryPurpose = "Programming";
                }
                else if (ContainsAny(normalized, "render", "dung phim", "video", "edit", "do hoa", "3d"))
                {
                    profile.PrimaryPurpose = "Content Creation";
                }
                else if (ContainsAny(normalized, "stream", "livestream"))
                {
                    profile.PrimaryPurpose = "Streaming";
                }
            }

            if (!profile.NeedsStreaming && ContainsAny(normalized, "stream", "livestream"))
            {
                profile.NeedsStreaming = true;
            }

            if (!profile.NeedsEditing && ContainsAny(normalized, "edit", "render", "video", "do hoa", "3d", "studio", "thu am", "lam nhac", "mix nhac", "producer", "daw", "photoshop", "lightroom", "autocad", "revit", "solidworks", "blender"))
            {
                profile.NeedsEditing = true;
            }

            if (!profile.NeedsMonitorHighRefresh && ContainsAny(normalized, "144hz", "165hz", "240hz", "high refresh"))
            {
                profile.NeedsMonitorHighRefresh = true;
            }

            if (string.IsNullOrWhiteSpace(profile.PerformancePriority))
            {
                if (ContainsAny(normalized, "im lang", "yen tinh", "it on", "quiet", "silent"))
                {
                    profile.PerformancePriority = "Quiet Operation";
                }
                else if (ContainsAny(normalized, "da nhiem", "multitask", "nhieu tab"))
                {
                    profile.PerformancePriority = "Multitasking";
                }
                else if (ContainsAny(normalized, "luu tru", "dung luong", "backup", "media", "plex", "nas"))
                {
                    profile.PerformancePriority = "Storage Capacity";
                }
                else if (ContainsAny(normalized, "gpu", "vga", "card man hinh", "cuda"))
                {
                    profile.PerformancePriority = "GPU Performance";
                }
                else if (ContainsAny(normalized, "cpu", "xu ly", "compile", "render cpu"))
                {
                    profile.PerformancePriority = "CPU Performance";
                }
            }

            if (string.IsNullOrWhiteSpace(profile.PreferredBrand))
            {
                if (ContainsAny(normalized, "nvidia", "geforce"))
                {
                    profile.PreferredBrand = "NVIDIA";
                }
                else if (ContainsAny(normalized, "amd", "radeon", "ryzen"))
                {
                    profile.PreferredBrand = "AMD";
                }
                else if (ContainsAny(normalized, "intel"))
                {
                    profile.PreferredBrand = "Intel";
                }
            }
        }

        private static decimal? TryMatchBudget(Regex regex, string normalizedText)
        {
            var match = regex.Match(normalizedText);
            if (!match.Success)
            {
                return null;
            }

            var rawNumber = match.Groups[1].Value.Replace(",", ".", StringComparison.Ordinal);
            if (!decimal.TryParse(rawNumber, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            var unit = match.Groups[2].Value.Trim();
            if (string.IsNullOrWhiteSpace(unit))
            {
                return null;
            }

            return unit switch
            {
                "ty" or "tỷ" => value * 1_000_000_000m,
                "k" or "nghin" or "ngàn" => value * 1_000m,
                _ => value * 1_000_000m,
            };
        }

        private static string ExtractJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "{}";
            }

            var trimmed = raw.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return trimmed;
            }

            var fenced = Regex.Match(trimmed, "```(?:json)?\\s*(\\{[\\s\\S]*\\})\\s*```", RegexOptions.IgnoreCase);
            if (fenced.Success)
            {
                return fenced.Groups[1].Value;
            }

            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return trimmed[start..(end + 1)];
            }

            return trimmed;
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var buffer = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    buffer.Append(ch);
                }
            }

            return buffer
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Replace('Đ', 'D')
                .ToLowerInvariant();
        }

        private static bool ContainsAny(string source, params string[] values)
        {
            return values.Any(value => source.Contains(value, StringComparison.Ordinal));
        }
    }
}
