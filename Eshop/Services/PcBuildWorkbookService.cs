using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Eshop.Services
{
    public class PcBuildWorkbookService : IPcBuildWorkbookService
    {
        private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace DocumentRelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace ContentTypeNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
        private static readonly XNamespace CoreNamespace = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        private static readonly XNamespace DcNamespace = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace DctermsNamespace = "http://purl.org/dc/terms/";
        private static readonly XNamespace DcmiTypeNamespace = "http://purl.org/dc/dcmitype/";
        private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
        private static readonly XNamespace ExtendedPropertiesNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
        private static readonly XNamespace VariantTypesNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";

        public byte[] Export(PcBuilderBuildDetailDto build)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                AddEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
                AddEntry(archive, "_rels/.rels", BuildRootRelationshipsXml());
                AddEntry(archive, "docProps/core.xml", BuildCorePropertiesXml(build));
                AddEntry(archive, "docProps/app.xml", BuildAppPropertiesXml());
                AddEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
                AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsXml());
                AddEntry(archive, "xl/styles.xml", BuildStylesXml());
                AddEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(build));
            }

            return stream.ToArray();
        }

        public async Task<PcBuildWorkbookImportModel> ImportAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            memory.Position = 0;

            using var archive = new ZipArchive(memory, ZipArchiveMode.Read, false);

            var sharedStrings = await ReadSharedStringsAsync(archive, cancellationToken);
            var worksheetXml = await ReadWorksheetXmlAsync(archive, cancellationToken);
            if (worksheetXml == null)
            {
                throw new InvalidOperationException("Không tìm thấy sheet dữ liệu trong file Excel.");
            }

            var document = XDocument.Parse(worksheetXml);
            var sheetData = document.Root?.Element(SpreadsheetNamespace + "sheetData");

            if (sheetData == null)
            {
                throw new InvalidOperationException("File Excel không có vùng dữ liệu hợp lệ.");
            }

            var rows = sheetData.Elements(SpreadsheetNamespace + "row")
                .Select(ParseRow)
                .Where(x => x.Count > 0)
                .ToList();

            var resolvedRows = rows
                .Select(row => row.ToDictionary(
                    x => x.Key,
                    x => ResolveSharedString(x.Value, sharedStrings),
                    StringComparer.OrdinalIgnoreCase))
                .ToList();

            var buildName = ExtractBuildName(resolvedRows);
            var headerRowIndex = FindHeaderRowIndex(resolvedRows);
            if (headerRowIndex < 0)
            {
                throw new InvalidOperationException("Không nhận diện được cấu trúc cột của file Excel.");
            }

            var header = resolvedRows[headerRowIndex];
            var importRows = new List<PcBuildWorkbookRowModel>();

            for (var i = headerRowIndex + 1; i < resolvedRows.Count; i++)
            {
                var row = resolvedRows[i];
                var mapped = MapImportedRow(row, header, sharedStrings);
                if (mapped != null)
                {
                    importRows.Add(mapped);
                }
            }

            return new PcBuildWorkbookImportModel
            {
                BuildName = buildName,
                Rows = importRows
            };
        }

        private static void AddEntry(ZipArchive archive, string entryPath, string content)
        {
            var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string BuildContentTypesXml()
        {
            var document = new XDocument(
                new XElement(ContentTypeNamespace + "Types",
                    new XAttribute(XNamespace.Xmlns + "ct", ContentTypeNamespace),
                    new XElement(ContentTypeNamespace + "Default",
                        new XAttribute("Extension", "rels"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(ContentTypeNamespace + "Default",
                        new XAttribute("Extension", "xml"),
                        new XAttribute("ContentType", "application/xml")),
                    new XElement(ContentTypeNamespace + "Override",
                        new XAttribute("PartName", "/xl/workbook.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement(ContentTypeNamespace + "Override",
                        new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                    new XElement(ContentTypeNamespace + "Override",
                        new XAttribute("PartName", "/xl/styles.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")),
                    new XElement(ContentTypeNamespace + "Override",
                        new XAttribute("PartName", "/docProps/core.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-package.core-properties+xml")),
                    new XElement(ContentTypeNamespace + "Override",
                        new XAttribute("PartName", "/docProps/app.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.extended-properties+xml"))));

            return document.Declaration + document.ToString(SaveOptions.DisableFormatting);
        }

        private static string BuildRootRelationshipsXml()
        {
            var document = new XDocument(
                new XElement(RelationshipNamespace + "Relationships",
                    new XElement(RelationshipNamespace + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml")),
                    new XElement(RelationshipNamespace + "Relationship",
                        new XAttribute("Id", "rId2"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"),
                        new XAttribute("Target", "docProps/core.xml")),
                    new XElement(RelationshipNamespace + "Relationship",
                        new XAttribute("Id", "rId3"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"),
                        new XAttribute("Target", "docProps/app.xml"))));

            return document.Declaration + document.ToString(SaveOptions.DisableFormatting);
        }

        private static string BuildCorePropertiesXml(PcBuilderBuildDetailDto build)
        {
            var now = DateTime.UtcNow;
            var document = new XDocument(
                new XElement(CoreNamespace + "coreProperties",
                    new XAttribute(XNamespace.Xmlns + "cp", CoreNamespace),
                    new XAttribute(XNamespace.Xmlns + "dc", DcNamespace),
                    new XAttribute(XNamespace.Xmlns + "dcterms", DctermsNamespace),
                    new XAttribute(XNamespace.Xmlns + "dcmitype", DcmiTypeNamespace),
                    new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                    new XElement(DcNamespace + "creator", "Eshop"),
                    new XElement(CoreNamespace + "lastModifiedBy", "Eshop"),
                    new XElement(DcNamespace + "title", string.IsNullOrWhiteSpace(build.BuildName) ? "PC Build" : build.BuildName),
                    new XElement(DctermsNamespace + "created",
                        new XAttribute(XsiNamespace + "type", "dcterms:W3CDTF"),
                        now.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                    new XElement(DctermsNamespace + "modified",
                        new XAttribute(XsiNamespace + "type", "dcterms:W3CDTF"),
                        now.ToString("yyyy-MM-ddTHH:mm:ssZ"))));

            return document.Declaration + document.ToString(SaveOptions.DisableFormatting);
        }

        private static string BuildAppPropertiesXml()
        {
            var document = new XDocument(
                new XElement(ExtendedPropertiesNamespace + "Properties",
                    new XAttribute(XNamespace.Xmlns + "ep", ExtendedPropertiesNamespace),
                    new XAttribute(XNamespace.Xmlns + "vt", VariantTypesNamespace),
                    new XElement(ExtendedPropertiesNamespace + "Application", "Eshop"),
                    new XElement(ExtendedPropertiesNamespace + "DocSecurity", 0),
                    new XElement(ExtendedPropertiesNamespace + "ScaleCrop", false),
                    new XElement(ExtendedPropertiesNamespace + "HeadingPairs",
                        new XElement(VariantTypesNamespace + "vector",
                            new XAttribute("size", 2),
                            new XAttribute("baseType", "variant"),
                            new XElement(VariantTypesNamespace + "variant",
                                new XElement(VariantTypesNamespace + "lpstr", "Worksheets")),
                            new XElement(VariantTypesNamespace + "variant",
                                new XElement(VariantTypesNamespace + "i4", 1)))),
                    new XElement(ExtendedPropertiesNamespace + "TitlesOfParts",
                        new XElement(VariantTypesNamespace + "vector",
                            new XAttribute("size", 1),
                            new XAttribute("baseType", "lpstr"),
                            new XElement(VariantTypesNamespace + "lpstr", "PC Build")))));

            return document.Declaration + document.ToString(SaveOptions.DisableFormatting);
        }

        private static string BuildWorkbookXml()
        {
            var document = new XDocument(
                new XElement(SpreadsheetNamespace + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", DocumentRelationshipNamespace),
                    new XElement(SpreadsheetNamespace + "sheets",
                        new XElement(SpreadsheetNamespace + "sheet",
                            new XAttribute("name", "PC Build"),
                            new XAttribute("sheetId", 1),
                            new XAttribute(DocumentRelationshipNamespace + "id", "rId1")))));

            return document.Declaration + document.ToString(SaveOptions.DisableFormatting);
        }

        private static string BuildWorkbookRelationshipsXml()
        {
            var document = new XDocument(
                new XElement(RelationshipNamespace + "Relationships",
                    new XElement(RelationshipNamespace + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet1.xml")),
                    new XElement(RelationshipNamespace + "Relationship",
                        new XAttribute("Id", "rId2"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                        new XAttribute("Target", "styles.xml"))));

            return document.Declaration + document.ToString(SaveOptions.DisableFormatting);
        }

        private static string BuildStylesXml()
        {
            var document = new XDocument(
                new XElement(SpreadsheetNamespace + "styleSheet",
                    new XElement(SpreadsheetNamespace + "fonts",
                        new XAttribute("count", 1),
                        new XElement(SpreadsheetNamespace + "font",
                            new XElement(SpreadsheetNamespace + "sz", new XAttribute("val", 11)),
                            new XElement(SpreadsheetNamespace + "color", new XAttribute("theme", 1)),
                            new XElement(SpreadsheetNamespace + "name", new XAttribute("val", "Calibri")),
                            new XElement(SpreadsheetNamespace + "family", new XAttribute("val", 2)),
                            new XElement(SpreadsheetNamespace + "scheme", new XAttribute("val", "minor")))),
                    new XElement(SpreadsheetNamespace + "fills",
                        new XAttribute("count", 2),
                        new XElement(SpreadsheetNamespace + "fill",
                            new XElement(SpreadsheetNamespace + "patternFill", new XAttribute("patternType", "none"))),
                        new XElement(SpreadsheetNamespace + "fill",
                            new XElement(SpreadsheetNamespace + "patternFill", new XAttribute("patternType", "gray125")))),
                    new XElement(SpreadsheetNamespace + "borders",
                        new XAttribute("count", 1),
                        new XElement(SpreadsheetNamespace + "border",
                            new XElement(SpreadsheetNamespace + "left"),
                            new XElement(SpreadsheetNamespace + "right"),
                            new XElement(SpreadsheetNamespace + "top"),
                            new XElement(SpreadsheetNamespace + "bottom"),
                            new XElement(SpreadsheetNamespace + "diagonal"))),
                    new XElement(SpreadsheetNamespace + "cellStyleXfs",
                        new XAttribute("count", 1),
                        new XElement(SpreadsheetNamespace + "xf",
                            new XAttribute("numFmtId", 0),
                            new XAttribute("fontId", 0),
                            new XAttribute("fillId", 0),
                            new XAttribute("borderId", 0))),
                    new XElement(SpreadsheetNamespace + "cellXfs",
                        new XAttribute("count", 2),
                        new XElement(SpreadsheetNamespace + "xf",
                            new XAttribute("numFmtId", 0),
                            new XAttribute("fontId", 0),
                            new XAttribute("fillId", 0),
                            new XAttribute("borderId", 0),
                            new XAttribute("xfId", 0)),
                        new XElement(SpreadsheetNamespace + "xf",
                            new XAttribute("numFmtId", 4),
                            new XAttribute("fontId", 0),
                            new XAttribute("fillId", 0),
                            new XAttribute("borderId", 0),
                            new XAttribute("xfId", 0),
                            new XAttribute("applyNumberFormat", 1))),
                    new XElement(SpreadsheetNamespace + "cellStyles",
                        new XAttribute("count", 1),
                        new XElement(SpreadsheetNamespace + "cellStyle",
                            new XAttribute("name", "Normal"),
                            new XAttribute("xfId", 0),
                            new XAttribute("builtinId", 0)))));

            return document.Declaration + document.ToString(SaveOptions.DisableFormatting);
        }

        private static string BuildWorksheetXml(PcBuilderBuildDetailDto build)
        {
            var rows = new List<XElement>
            {
                BuildTextRow(1, "A", "Tên cấu hình", "B", build.BuildName),
                BuildTextRow(2, "A", "Mã build", "B", build.BuildCode ?? string.Empty),
                BuildTextRow(3, "A", "Xuất lúc", "B", DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                BuildNumberRow(4, "A", "Tổng tiền", "B", build.TotalPrice, true),
                BuildNumberRow(5, "A", "Công suất ước tính", "B", build.EstimatedPower, false),
                new XElement(SpreadsheetNamespace + "row",
                    new XAttribute("r", 7),
                    InlineCell("A7", "Loại linh kiện"),
                    InlineCell("B7", "Product ID"),
                    InlineCell("C7", "Tên sản phẩm"),
                    InlineCell("D7", "Số lượng"),
                    InlineCell("E7", "Đơn giá"),
                    InlineCell("F7", "Thành tiền"))
            };

            var rowIndex = 8;
            foreach (var item in build.Items)
            {
                rows.Add(new XElement(SpreadsheetNamespace + "row",
                    new XAttribute("r", rowIndex),
                    InlineCell($"A{rowIndex}", item.ComponentType.ToString()),
                    NumberCell($"B{rowIndex}", item.ProductId, false),
                    InlineCell($"C{rowIndex}", item.Product.Name),
                    NumberCell($"D{rowIndex}", item.Quantity, false),
                    NumberCell($"E{rowIndex}", item.Product.Price, true),
                    NumberCell($"F{rowIndex}", item.Product.Price * item.Quantity, true)));
                rowIndex++;
            }

            var worksheet = new XDocument(
                new XElement(SpreadsheetNamespace + "worksheet",
                    new XAttribute(XNamespace.Xmlns + "r", DocumentRelationshipNamespace),
                    new XElement(SpreadsheetNamespace + "dimension", new XAttribute("ref", $"A1:F{Math.Max(7, rowIndex - 1)}")),
                    new XElement(SpreadsheetNamespace + "sheetViews",
                        new XElement(SpreadsheetNamespace + "sheetView", new XAttribute("workbookViewId", 0))),
                    new XElement(SpreadsheetNamespace + "sheetFormatPr", new XAttribute("defaultRowHeight", 15)),
                    new XElement(SpreadsheetNamespace + "cols",
                        new XElement(SpreadsheetNamespace + "col", new XAttribute("min", 1), new XAttribute("max", 1), new XAttribute("width", 18), new XAttribute("customWidth", 1)),
                        new XElement(SpreadsheetNamespace + "col", new XAttribute("min", 2), new XAttribute("max", 2), new XAttribute("width", 14), new XAttribute("customWidth", 1)),
                        new XElement(SpreadsheetNamespace + "col", new XAttribute("min", 3), new XAttribute("max", 3), new XAttribute("width", 42), new XAttribute("customWidth", 1)),
                        new XElement(SpreadsheetNamespace + "col", new XAttribute("min", 4), new XAttribute("max", 4), new XAttribute("width", 12), new XAttribute("customWidth", 1)),
                        new XElement(SpreadsheetNamespace + "col", new XAttribute("min", 5), new XAttribute("max", 6), new XAttribute("width", 16), new XAttribute("customWidth", 1))),
                    new XElement(SpreadsheetNamespace + "sheetData", rows)));

            return worksheet.Declaration + worksheet.ToString(SaveOptions.DisableFormatting);
        }

        private static XElement BuildTextRow(int rowNumber, string labelColumn, string label, string valueColumn, string value)
        {
            return new XElement(SpreadsheetNamespace + "row",
                new XAttribute("r", rowNumber),
                InlineCell($"{labelColumn}{rowNumber}", label),
                InlineCell($"{valueColumn}{rowNumber}", value));
        }

        private static XElement BuildNumberRow(int rowNumber, string labelColumn, string label, string valueColumn, decimal value, bool currency)
        {
            return new XElement(SpreadsheetNamespace + "row",
                new XAttribute("r", rowNumber),
                InlineCell($"{labelColumn}{rowNumber}", label),
                NumberCell($"{valueColumn}{rowNumber}", value, currency));
        }

        private static XElement InlineCell(string reference, string value)
        {
            return new XElement(SpreadsheetNamespace + "c",
                new XAttribute("r", reference),
                new XAttribute("t", "inlineStr"),
                new XElement(SpreadsheetNamespace + "is",
                    new XElement(SpreadsheetNamespace + "t", value ?? string.Empty)));
        }

        private static XElement NumberCell(string reference, decimal value, bool currency)
        {
            return new XElement(SpreadsheetNamespace + "c",
                new XAttribute("r", reference),
                new XAttribute("s", currency ? 1 : 0),
                new XElement(SpreadsheetNamespace + "v", value.ToString(CultureInfo.InvariantCulture)));
        }

        private static async Task<string?> ReadWorksheetXmlAsync(ZipArchive archive, CancellationToken cancellationToken)
        {
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            var workbookRelsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

            if (workbookEntry == null || workbookRelsEntry == null)
            {
                return await ReadEntryAsync(archive.GetEntry("xl/worksheets/sheet1.xml"), cancellationToken);
            }

            var workbookXml = await ReadEntryAsync(workbookEntry, cancellationToken);
            var relsXml = await ReadEntryAsync(workbookRelsEntry, cancellationToken);
            if (string.IsNullOrWhiteSpace(workbookXml) || string.IsNullOrWhiteSpace(relsXml))
            {
                return await ReadEntryAsync(archive.GetEntry("xl/worksheets/sheet1.xml"), cancellationToken);
            }

            var workbook = XDocument.Parse(workbookXml);
            var relationships = XDocument.Parse(relsXml);
            var firstSheet = workbook.Root?
                .Element(SpreadsheetNamespace + "sheets")?
                .Elements(SpreadsheetNamespace + "sheet")
                .FirstOrDefault();

            if (firstSheet == null)
            {
                return await ReadEntryAsync(archive.GetEntry("xl/worksheets/sheet1.xml"), cancellationToken);
            }

            var relationshipId = firstSheet.Attribute(DocumentRelationshipNamespace + "id")?.Value
                ?? firstSheet.Attribute("id")?.Value;

            var target = relationships.Root?
                .Elements(RelationshipNamespace + "Relationship")
                .FirstOrDefault(x => string.Equals(x.Attribute("Id")?.Value, relationshipId, StringComparison.OrdinalIgnoreCase))
                ?.Attribute("Target")?.Value;

            if (string.IsNullOrWhiteSpace(target))
            {
                return await ReadEntryAsync(archive.GetEntry("xl/worksheets/sheet1.xml"), cancellationToken);
            }

            var normalizedTarget = target.Replace('\\', '/').TrimStart('/');
            if (!normalizedTarget.StartsWith("worksheets/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTarget = $"worksheets/{normalizedTarget}";
            }

            return await ReadEntryAsync(archive.GetEntry($"xl/{normalizedTarget}"), cancellationToken);
        }

        private static async Task<List<string>> ReadSharedStringsAsync(ZipArchive archive, CancellationToken cancellationToken)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            var content = await ReadEntryAsync(entry, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<string>();
            }

            var document = XDocument.Parse(content);
            return document.Root?
                .Elements(SpreadsheetNamespace + "si")
                .Select(ReadSharedStringItem)
                .ToList()
                ?? new List<string>();
        }

        private static string ReadSharedStringItem(XElement item)
        {
            var directText = item.Element(SpreadsheetNamespace + "t");
            if (directText != null)
            {
                return directText.Value;
            }

            return string.Concat(item
                .Elements(SpreadsheetNamespace + "r")
                .Select(x => x.Element(SpreadsheetNamespace + "t")?.Value ?? string.Empty));
        }

        private static Dictionary<string, string> ParseRow(XElement row)
        {
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in row.Elements(SpreadsheetNamespace + "c"))
            {
                var reference = cell.Attribute("r")?.Value;
                if (string.IsNullOrWhiteSpace(reference))
                {
                    continue;
                }

                var column = new string(reference.TakeWhile(char.IsLetter).ToArray());
                if (string.IsNullOrWhiteSpace(column))
                {
                    continue;
                }

                cells[column] = ReadCellRawValue(cell);
            }

            return cells;
        }

        private static string? ExtractBuildName(IReadOnlyList<Dictionary<string, string>> rows)
        {
            foreach (var row in rows.Take(5))
            {
                if (!row.TryGetValue("A", out var label))
                {
                    continue;
                }

                var normalized = NormalizeHeader(label);
                if (normalized is "tencauhinh" or "buildname")
                {
                    return row.TryGetValue("B", out var value) ? value : null;
                }
            }

            return null;
        }

        private static int FindHeaderRowIndex(IReadOnlyList<Dictionary<string, string>> rows)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var normalizedValues = rows[i]
                    .Values
                    .Select(NormalizeHeader)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                var hasComponent = normalizedValues.Contains("loailinhkien") || normalizedValues.Contains("componenttype");
                var hasProduct = normalizedValues.Contains("productid") || normalizedValues.Contains("idsanpham");
                var hasQuantity = normalizedValues.Contains("soluong") || normalizedValues.Contains("quantity");

                if (hasComponent && hasProduct && hasQuantity)
                {
                    return i;
                }
            }

            return -1;
        }

        private static PcBuildWorkbookRowModel? MapImportedRow(
            IReadOnlyDictionary<string, string> row,
            IReadOnlyDictionary<string, string> header,
            IReadOnlyList<string> sharedStrings)
        {
            var mapped = new PcBuildWorkbookRowModel
            {
                ComponentType = ParseComponentType(GetValueByHeader(row, header, sharedStrings, "loailinhkien", "componenttype")),
                ProductId = ParseInt(GetValueByHeader(row, header, sharedStrings, "productid", "idsanpham")),
                ProductName = GetValueByHeader(row, header, sharedStrings, "tensanpham", "productname"),
                Quantity = ParseInt(GetValueByHeader(row, header, sharedStrings, "soluong", "quantity")) ?? 1
            };

            if (mapped.ComponentType == null &&
                !mapped.ProductId.HasValue &&
                string.IsNullOrWhiteSpace(mapped.ProductName))
            {
                return null;
            }

            mapped.ProductName = mapped.ProductName?.Trim();
            return mapped;
        }

        private static string? GetValueByHeader(
            IReadOnlyDictionary<string, string> row,
            IReadOnlyDictionary<string, string> header,
            IReadOnlyList<string> sharedStrings,
            params string[] expectedHeaders)
        {
            foreach (var column in header.Keys)
            {
                var normalizedHeader = NormalizeHeader(ResolveSharedString(header[column], sharedStrings));
                if (!expectedHeaders.Contains(normalizedHeader))
                {
                    continue;
                }

                return row.TryGetValue(column, out var value)
                    ? ResolveSharedString(value, sharedStrings)
                    : null;
            }

            return null;
        }

        private static string ReadCellRawValue(XElement cell)
        {
            var type = cell.Attribute("t")?.Value;
            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                var inlineString = cell.Element(SpreadsheetNamespace + "is");
                if (inlineString == null)
                {
                    return string.Empty;
                }

                var directText = inlineString.Element(SpreadsheetNamespace + "t");
                if (directText != null)
                {
                    return directText.Value;
                }

                return string.Concat(inlineString
                    .Elements(SpreadsheetNamespace + "r")
                    .Select(x => x.Element(SpreadsheetNamespace + "t")?.Value ?? string.Empty));
            }

            return cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
        }

        private static string ResolveSharedString(string rawValue, IReadOnlyList<string> sharedStrings)
        {
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
                index >= 0 &&
                index < sharedStrings.Count)
            {
                return sharedStrings[index];
            }

            return rawValue;
        }

        private static PcComponentType? ParseComponentType(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            if (Enum.TryParse<PcComponentType>(rawValue.Trim(), true, out var componentType))
            {
                return componentType;
            }

            return NormalizeHeader(rawValue) switch
            {
                "cpu" => PcComponentType.CPU,
                "mainboard" => PcComponentType.Mainboard,
                "ram" => PcComponentType.RAM,
                "gpu" => PcComponentType.GPU,
                "ssd" => PcComponentType.SSD,
                "psu" => PcComponentType.PSU,
                "cooler" => PcComponentType.Cooler,
                "case" => PcComponentType.Case,
                "monitor" => PcComponentType.Monitor,
                "hdd" => PcComponentType.HDD,
                _ => null
            };
        }

        private static int? ParseInt(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var normalized = rawValue
                .Trim()
                .Replace(",", string.Empty);

            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static string NormalizeHeader(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().ToLowerInvariant()
                .Replace("đ", "d")
                .Replace("Đ", "D");

            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized.Normalize(NormalizationForm.FormD))
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private static async Task<string?> ReadEntryAsync(ZipArchiveEntry? entry, CancellationToken cancellationToken)
        {
            if (entry == null)
            {
                return null;
            }

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
#if NET10_0_OR_GREATER
            return await reader.ReadToEndAsync(cancellationToken);
#else
            cancellationToken.ThrowIfCancellationRequested();
            return await reader.ReadToEndAsync();
#endif
        }
    }
}
