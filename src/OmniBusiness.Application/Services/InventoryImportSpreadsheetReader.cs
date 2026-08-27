using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OmniBusiness.Application.Contracts;

namespace OmniBusiness.Application.Services;

internal static class InventoryImportSpreadsheetReader
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static InventoryImportParseResult Parse(InventoryImportFileDto file)
    {
        if (file.Content is not { Length: > 0 })
        {
            throw new InvalidOperationException("The selected import file is empty.");
        }

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        return extension switch
        {
            ".csv" => ParseCsv(file.Content),
            ".xlsx" => ParseXlsx(file.Content),
            ".xls" => throw new InvalidOperationException("Legacy .xls files are not supported. Save the sheet as .xlsx or .csv and try again."),
            _ => throw new InvalidOperationException("Unsupported file format. Upload an .xlsx or .csv inventory sheet.")
        };
    }

    private static InventoryImportParseResult ParseCsv(byte[] content)
    {
        using var reader = new StringReader(Encoding.UTF8.GetString(content));
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = ParseCsvLine(line);
            rows.Add(ToColumnMap(cells));
        }

        return BuildRows(rows);
    }

    private static InventoryImportParseResult ParseXlsx(byte[] content)
    {
        using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read, false);
        var sharedStrings = ReadSharedStrings(archive);
        var worksheetPath = ResolveFirstWorksheetPath(archive);
        var worksheetEntry = archive.GetEntry(worksheetPath)
            ?? throw new InvalidOperationException("The uploaded workbook does not contain a readable worksheet.");

        using var worksheetStream = worksheetEntry.Open();
        var worksheet = XDocument.Load(worksheetStream);
        var rowMaps = worksheet
            .Descendants(SpreadsheetNamespace + "row")
            .Select(row => row.Elements(SpreadsheetNamespace + "c")
                .ToDictionary(
                    cell => GetColumnReference(cell.Attribute("r")?.Value),
                    cell => ReadCellValue(cell, sharedStrings),
                    StringComparer.OrdinalIgnoreCase))
            .Where(row => row.Count > 0)
            .Cast<IReadOnlyDictionary<string, string?>>()
            .ToArray();

        return BuildRows(rowMaps);
    }

    private static InventoryImportParseResult BuildRows(IReadOnlyList<IReadOnlyDictionary<string, string?>> rowMaps)
    {
        if (rowMaps.Count == 0)
        {
            throw new InvalidOperationException("The import file does not contain any rows.");
        }

        var headerColumns = rowMaps[0]
            .OrderBy(item => ToColumnIndex(item.Key))
            .Select(item => (Column: item.Key, Header: NormalizeHeader(item.Value)))
            .ToArray();

        if (headerColumns.All(item => string.IsNullOrWhiteSpace(item.Header)))
        {
            throw new InvalidOperationException("The first row must contain column headers.");
        }

        var rows = new List<InventoryImportRow>();
        var warnings = new List<string>();
        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < rowMaps.Count; index++)
        {
            var rowNumber = index + 1;
            var values = rowMaps[index].Values.Select(value => value?.Trim()).ToArray();
            if (values.All(value => string.IsNullOrWhiteSpace(value)))
            {
                continue;
            }

            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var headerColumn in headerColumns)
            {
                if (string.IsNullOrWhiteSpace(headerColumn.Header))
                {
                    continue;
                }

                data[headerColumn.Header] = rowMaps[index].TryGetValue(headerColumn.Column, out var value)
                    ? value?.Trim()
                    : null;
            }

            var sku = GetValue(data, "sku", "code", "productcode", "itemcode", "barcode");
            var name = GetValue(data, "name", "productname", "itemname", "title");
            if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name))
            {
                warnings.Add($"Row {rowNumber} skipped because SKU or Name is missing.");
                continue;
            }

            if (!seenSkus.Add(sku))
            {
                warnings.Add($"Row {rowNumber} duplicates SKU '{sku}'. The last occurrence will be applied.");
            }

            rows.Add(new InventoryImportRow(
                rowNumber,
                sku,
                name,
                GetValue(data, "category", "group", "department"),
                ParseDecimal(GetValue(data, "unitprice", "price", "saleprice", "sellingprice")),
                GetValue(data, "warehouse", "location", "store", "branch"),
                ParseInt(GetValue(data, "inhand", "qty", "quantity", "stock", "openingstock", "onhand")),
                ParseInt(GetValue(data, "reserved", "reservedqty")),
                ParseInt(GetValue(data, "reorderlevel", "reorder", "minstock", "minimumstock")),
                ParseBool(GetValue(data, "isfavorite", "favorite", "fav")),
                ParseBool(GetValue(data, "isquicksale", "quicksale", "quick")),
                GetValue(data, "visualcode", "shortcode", "poscode")));
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("No valid inventory rows were found in the uploaded file.");
        }

        return new InventoryImportParseResult(rows, warnings);
    }

    private static IReadOnlyDictionary<string, string?> ToColumnMap(IReadOnlyList<string> cells)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < cells.Count; index++)
        {
            map[ToColumnLetter(index + 1)] = cells[index];
        }

        return map;
    }

    private static string? ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        return type switch
        {
            "s" => ParseSharedString(cell, sharedStrings),
            "inlineStr" => string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(node => node.Value)),
            "b" => cell.Element(SpreadsheetNamespace + "v")?.Value == "1" ? "true" : "false",
            _ => cell.Element(SpreadsheetNamespace + "v")?.Value
        };
    }

    private static string? ParseSharedString(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var value = cell.Element(SpreadsheetNamespace + "v")?.Value;
        return int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count
            ? sharedStrings[index]
            : null;
    }

    private static string ResolveFirstWorksheetPath(ZipArchive archive)
    {
        if (archive.GetEntry("xl/worksheets/sheet1.xml") is not null)
        {
            return "xl/worksheets/sheet1.xml";
        }

        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidOperationException("The uploaded workbook is missing workbook metadata.");
        using var workbookStream = workbookEntry.Open();
        var workbook = XDocument.Load(workbookStream);
        var firstSheet = workbook.Descendants(SpreadsheetNamespace + "sheet").FirstOrDefault()
            ?? throw new InvalidOperationException("The uploaded workbook does not contain any worksheets.");
        var relationshipId = firstSheet.Attribute(RelationshipNamespace + "id")?.Value
            ?? throw new InvalidOperationException("The workbook sheet relationship could not be resolved.");

        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidOperationException("The workbook relationships file is missing.");
        using var relationshipsStream = relationshipsEntry.Open();
        var relationships = XDocument.Load(relationshipsStream);
        var target = relationships
            .Descendants(PackageRelationshipNamespace + "Relationship")
            .FirstOrDefault(node => string.Equals(node.Attribute("Id")?.Value, relationshipId, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Target")
            ?.Value
            ?? throw new InvalidOperationException("The first worksheet target could not be resolved.");

        return target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : $"xl/{target.TrimStart('/')}";
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return Array.Empty<string>();
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);

        return document
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(node => node.Value)))
            .ToArray();
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var builder = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];
            if (current == '"')
            {
                if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                    continue;
                }

                insideQuotes = !insideQuotes;
                continue;
            }

            if (current == ',' && !insideQuotes)
            {
                cells.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(current);
        }

        cells.Add(builder.ToString());
        return cells;
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
                .Trim()
                .Where(char.IsLetterOrDigit)
                .ToArray())
            .ToLowerInvariant();
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> data, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (data.TryGetValue(candidate, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        return decimal.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "y" => true,
            "0" or "false" or "no" or "n" => false,
            _ => null
        };
    }

    private static string GetColumnReference(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return string.Empty;
        }

        return new string(cellReference
            .TakeWhile(char.IsLetter)
            .ToArray());
    }

    private static string ToColumnLetter(int index)
    {
        var value = index;
        var builder = new StringBuilder();

        while (value > 0)
        {
            value--;
            builder.Insert(0, (char)('A' + (value % 26)));
            value /= 26;
        }

        return builder.ToString();
    }

    private static int ToColumnIndex(string columnLetter)
    {
        if (string.IsNullOrWhiteSpace(columnLetter))
        {
            return int.MaxValue;
        }

        var result = 0;
        foreach (var character in columnLetter.ToUpperInvariant())
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            result = (result * 26) + (character - 'A' + 1);
        }

        return result;
    }
}

internal sealed record InventoryImportParseResult(
    IReadOnlyList<InventoryImportRow> Rows,
    IReadOnlyList<string> Warnings);

internal sealed record InventoryImportRow(
    int RowNumber,
    string Sku,
    string Name,
    string? Category,
    decimal? UnitPrice,
    string? Warehouse,
    int? InHand,
    int? Reserved,
    int? ReorderLevel,
    bool? IsFavorite,
    bool? IsQuickSale,
    string? VisualCode);
