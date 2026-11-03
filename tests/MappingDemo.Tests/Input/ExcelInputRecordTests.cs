using System.IO.Compression;
using System.Text;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Input;

public sealed class ExcelInputRecordTests
{
    [Fact]
    public async Task Xlsx_reads_selected_sheet_with_exact_rows_and_cached_formula()
    {
        await using var stream = GoldenWorkbook.Xlsx();
        var module = new InputRecordModule();
        var compiled = module.Compile(Layout(), SourceTable()).CompiledLayout!;

        var result = await module.ReadAsync(
            stream,
            compiled,
            ".XLSX",
            InputReadLimits.Default);

        Assert.True(result.IsSuccess, $"{result.Error?.Code}:{result.Error?.Scalar}");
        Assert.Equal(["ข้อมูล", "Orders"], result.Workbook!.Sheets);
        Assert.Equal("Orders", result.Workbook.SelectedSheet);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal(4, result.Records[0].WorksheetRowNumber);
        Assert.Equal(1, result.Records[0].RowNumber);
        Assert.Equal("คำสั่งซื้อ-๑", result.Records[0].Values["order_no"]);
        Assert.Equal("12.5", result.Records[0].Values["amount"]);
        Assert.Equal("true", result.Records[0].Values["active"]);
        Assert.Equal("2", result.Records[0].Values["formula"]);
        Assert.Equal(6, result.Records[1].PhysicalLineNumber);
    }

    [Fact]
    public async Task Xlsx_rejects_wrong_extension_before_parsing()
    {
        await using var stream = GoldenWorkbook.Xlsx();
        var module = new InputRecordModule();
        var compiled = module.Compile(Layout(), SourceTable()).CompiledLayout!;

        var result = await module.ReadAsync(
            stream, compiled, ".xls", InputReadLimits.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal("WorkbookSignatureMismatch", result.Error!.Code);
        Assert.Empty(result.Records);
    }

    [Fact]
    public async Task Xls_biff_fixture_has_the_same_source_text_policy()
    {
        await using var stream = GoldenWorkbook.Xls();
        var layout = Layout() with
        {
            WorkbookKind = ExcelWorkbookKind.Xls,
            Sheet = "Sheet",
            HeaderRow = 1,
            DataStartRow = 2
        };
        var module = new InputRecordModule();
        var compiled = module.Compile(layout, SourceTable()).CompiledLayout!;

        var result = await module.ReadAsync(
            stream, compiled, ".xls", InputReadLimits.Default);

        Assert.True(result.IsSuccess, $"{result.Error?.Code}:{result.Error?.Scalar}");
        var record = Assert.Single(result.Records);
        Assert.Equal("ORD-XLS", record.Values["order_no"]);
        Assert.Equal("12.5", record.Values["amount"]);
        Assert.Equal("true", record.Values["active"]);
        Assert.Equal("2", record.Values["formula"]);
        Assert.Equal(2, record.WorksheetRowNumber);
    }

    [Fact]
    public async Task Xls_repairs_windows_874_text_misdecoded_as_windows_1252()
    {
        await using var stream = GoldenWorkbook.ThaiXls();
        var layout = Layout() with
        {
            WorkbookKind = ExcelWorkbookKind.Xls,
            Sheet = "Sheet",
            HeaderRow = 1,
            DataStartRow = 2,
            Fields = [new ExcelInputField("ชื่อ", "order_no")]
        };
        var module = new InputRecordModule();
        var compiled = module.Compile(layout, SourceTable()).CompiledLayout!;

        var result = await module.ReadAsync(
            stream, compiled, ".xls", InputReadLimits.Default);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal("สมชาย", Assert.Single(result.Records).Values["order_no"]);
    }

    [Fact]
    public async Task Xlsx_rejects_unknown_sheet_and_missing_header_without_rows()
    {
        var module = new InputRecordModule();
        await using var unknownSheet = GoldenWorkbook.Xlsx();
        var unknownCompiled = module.Compile(
            Layout() with { Sheet = "missing" }, SourceTable()).CompiledLayout!;
        var unknown = await module.ReadAsync(
            unknownSheet, unknownCompiled, ".xlsx", InputReadLimits.Default);
        Assert.Equal("WorkbookSheetNotFound", unknown.Error!.Code);
        Assert.Empty(unknown.Records);

        await using var missingHeader = GoldenWorkbook.Xlsx();
        var missingCompiled = module.Compile(
            Layout() with
            {
                Fields = [new ExcelInputField("does-not-exist", "order_no")]
            }, SourceTable()).CompiledLayout!;
        var missing = await module.ReadAsync(
            missingHeader, missingCompiled, ".xlsx", InputReadLimits.Default);
        Assert.Equal("RequiredWorkbookHeaderMissing", missing.Error!.Code);
        Assert.Empty(missing.Records);
    }

    [Fact]
    public async Task Xlsx_formats_date_and_time_invariantly_and_rejects_error_cells()
    {
        var module = new InputRecordModule();
        var layout = Layout() with
        {
            Fields =
            [
                new ExcelInputField("Date", "date"),
                new ExcelInputField("Time", "time")
            ]
        };
        var compiled = module.Compile(layout, SourceTable()).CompiledLayout!;
        await using var valid = GoldenWorkbook.Xlsx();
        var result = await module.ReadAsync(
            valid, compiled, ".xlsx", InputReadLimits.Default);
        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal("2026-10-19T00:00:00Z", result.Records[0].Values["date"]);
        Assert.Equal("1899-12-31T13:14:15Z", result.Records[0].Values["time"]);

        await using var invalid = GoldenWorkbook.Xlsx(withError: true);
        var error = await module.ReadAsync(
            invalid, compiled, ".xlsx", InputReadLimits.Default);
        Assert.Equal("WorkbookCellError", error.Error!.Code);
        Assert.Equal(4, error.Error.PhysicalLineNumber);
        Assert.Equal(7, error.Error.Position);
        Assert.Empty(error.Records);
    }

    [Fact]
    public void Compile_rejects_invalid_excel_contract_at_exact_paths()
    {
        var result = new InputRecordModule().Compile(
            Layout() with
            {
                Sheet = " ",
                HeaderRow = 0,
                DataStartRow = 0,
                Fields =
                [
                    new ExcelInputField("Order No", "missing"),
                    new ExcelInputField("Order No", "amount")
                ]
            },
            SourceTable());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Path == "sheet" && d.Code == "SheetRequired");
        Assert.Contains(result.Diagnostics, d => d.Path == "headerRow" && d.Code == "InvalidHeaderRow");
        Assert.Contains(result.Diagnostics, d => d.Path == "dataStartRow" && d.Code == "DataStartRowMustFollowHeader");
        Assert.Contains(result.Diagnostics, d => d.Path == "fields[0].sourceColumn" && d.Code == "SourceColumnNotFound");
        Assert.Contains(result.Diagnostics, d => d.Path == "fields[1].inputHeader" && d.Code == "DuplicateInputHeader");
    }

    private static ExcelInputLayout Layout() => new(
        ExcelWorkbookKind.AutoDetect,
        "Orders",
        HeaderRow: 2,
        DataStartRow: 4,
        Fields:
        [
            new ExcelInputField("Order No", "order_no"),
            new ExcelInputField("Amount", "amount"),
            new ExcelInputField("Active", "active"),
            new ExcelInputField("Formula", "formula")
        ],
        ExcelBlankRowPolicy.Skip);

    private static TableDefinition SourceTable() => new(
        "src_orders",
        TableKind.Source,
        [
            new ColumnDefinition("order_no", ColumnDataType.Text, false, 1),
            new ColumnDefinition("amount", ColumnDataType.Text, false, 2),
            new ColumnDefinition("active", ColumnDataType.Text, false, 3),
            new ColumnDefinition("formula", ColumnDataType.Text, false, 4),
            new ColumnDefinition("date", ColumnDataType.Text, false, 5),
            new ColumnDefinition("time", ColumnDataType.Text, false, 6)
        ]);

    internal static class GoldenWorkbook
    {
        internal static MemoryStream Xlsx(bool withError = false)
        {
            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                Add(archive, "[Content_Types].xml", """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                      <Default Extension="xml" ContentType="application/xml"/>
                      <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                      <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                      <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                    </Types>
                    """);
                Add(archive, "_rels/.rels", """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                    </Relationships>
                    """);
                Add(archive, "xl/workbook.xml", """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                      <sheets><sheet name="ข้อมูล" sheetId="1" r:id="rId1"/><sheet name="Orders" sheetId="2" r:id="rId2"/></sheets>
                    </workbook>
                    """);
                Add(archive, "xl/_rels/workbook.xml.rels", """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                      <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
                    </Relationships>
                    """);
                Add(archive, "xl/worksheets/sheet1.xml", Sheet("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>เมตาดาตา</t></is></c></row>"));
                var rows = """
                    <row r="1"><c r="A1" t="inlineStr"><is><t>รายงานคำสั่งซื้อ</t></is></c></row>
                    <row r="2"><c r="A2" t="inlineStr"><is><t>Order No</t></is></c><c r="B2" t="inlineStr"><is><t>Amount</t></is></c><c r="C2" t="inlineStr"><is><t>Active</t></is></c><c r="D2" t="inlineStr"><is><t>Formula</t></is></c><c r="E2" t="inlineStr"><is><t>Date</t></is></c><c r="F2" t="inlineStr"><is><t>Time</t></is></c><c r="G2" t="inlineStr"><is><t>Error</t></is></c></row>
                    <row r="4"><c r="A4" t="inlineStr"><is><t>คำสั่งซื้อ-๑</t></is></c><c r="B4"><v>12.5</v></c><c r="C4" t="b"><v>1</v></c><c r="D4"><f>1+1</f><v>2</v></c><c r="E4" t="d"><v>2026-10-19T00:00:00Z</v></c><c r="F4" t="d"><v>1899-12-31T13:14:15Z</v></c>__ERROR_CELL__</row>
                    <row r="5"></row>
                    <row r="6"><c r="A6" t="inlineStr"><is><t>ORD-2</t></is></c><c r="B6"><v>0</v></c><c r="C6" t="b"><v>0</v></c><c r="D6"><f>2+2</f><v>4</v></c></row>
                    """;
                rows = rows.Replace(
                    "__ERROR_CELL__",
                    withError ? "<c r=\"G4\" t=\"e\"><v>#DIV/0!</v></c>" : string.Empty,
                    StringComparison.Ordinal);
                Add(archive, "xl/worksheets/sheet2.xml", Sheet(
                    rows,
                    "<mergeCells count=\"1\"><mergeCell ref=\"A1:G1\"/></mergeCells>"));
            }
            stream.Position = 0;
            return stream;
        }

        internal static MemoryStream Xls()
        {
            var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
            Record(writer, 0x0009, [0x02, 0x00, 0x10, 0x00]);
            Record(writer, 0x0000, [0, 0, 2, 0, 0, 0, 4, 0]);
            Label(writer, 0, 0, "Order No");
            Label(writer, 0, 1, "Amount");
            Label(writer, 0, 2, "Active");
            Label(writer, 0, 3, "Formula");
            Label(writer, 1, 0, "ORD-XLS");
            Number(writer, 1, 1, 12.5);
            Bool(writer, 1, 2, true);
            Number(writer, 1, 3, 2);
            Record(writer, 0x000A, []);
            stream.Position = 0;
            return stream;
        }

        internal static MemoryStream ThaiXls()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var windows874 = Encoding.GetEncoding(874);
            var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
            Record(writer, 0x0009, [0x02, 0x00, 0x10, 0x00]);
            Record(writer, 0x0000, [0, 0, 2, 0, 0, 0, 1, 0]);
            Label(writer, 0, 0, "ชื่อ", windows874);
            Label(writer, 1, 0, "สมชาย", windows874);
            Record(writer, 0x000A, []);
            stream.Position = 0;
            return stream;
        }

        private static void Label(
            BinaryWriter writer,
            ushort row,
            ushort column,
            string value,
            Encoding? encoding = null)
        {
            var bytes = (encoding ?? Encoding.Latin1).GetBytes(value);
            using var payload = new MemoryStream();
            using (var cell = new BinaryWriter(payload, Encoding.Latin1, leaveOpen: true))
            {
                cell.Write(row);
                cell.Write(column);
                cell.Write(new byte[3]);
                cell.Write((byte)bytes.Length);
                cell.Write(bytes);
            }
            Record(writer, 0x0004, payload.ToArray());
        }

        private static void Number(BinaryWriter writer, ushort row, ushort column, double value)
        {
            using var payload = new MemoryStream();
            using (var cell = new BinaryWriter(payload, Encoding.Latin1, leaveOpen: true))
            {
                cell.Write(row);
                cell.Write(column);
                cell.Write(new byte[3]);
                cell.Write(value);
            }
            Record(writer, 0x0003, payload.ToArray());
        }

        private static void Bool(BinaryWriter writer, ushort row, ushort column, bool value)
        {
            using var payload = new MemoryStream();
            using (var cell = new BinaryWriter(payload, Encoding.Latin1, leaveOpen: true))
            {
                cell.Write(row);
                cell.Write(column);
                cell.Write(new byte[3]);
                cell.Write(value ? (byte)1 : (byte)0);
                cell.Write((byte)0);
            }
            Record(writer, 0x0005, payload.ToArray());
        }

        private static void Record(BinaryWriter writer, ushort id, byte[] payload)
        {
            writer.Write(id);
            writer.Write((ushort)payload.Length);
            writer.Write(payload);
        }

        private static string Sheet(string rows, string extra = "") => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>{rows}</sheetData>{extra}</worksheet>
            """;

        private static void Add(ZipArchive archive, string path, string content)
        {
            using var writer = new StreamWriter(
                archive.CreateEntry(path).Open(),
                new UTF8Encoding(false));
            writer.Write(content);
        }
    }
}
