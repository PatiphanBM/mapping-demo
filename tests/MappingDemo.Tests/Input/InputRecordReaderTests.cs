using System.Text;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Input;

public sealed class InputRecordReaderTests
{
    private const string Header = "HHEADER-0001";
    private const string ThaiDetail =
        "DMTI บริษัท      20261006TAIL-A ";
    private const string AsciiDetail =
        "DPMIBASCII       20261007TAIL-B ";
    private const string Trailer = "TTRAILER-001";

    [Fact]
    public async Task ReadAsync_reads_delimited_records_through_module_contract()
    {
        var layout = new DelimitedInputLayout(
            Encoding: "utf-8",
            Delimiter: ",",
            PreambleLines: 2,
            HeaderPolicy: DelimitedHeaderPolicy.SingleRow,
            Fields:
            [
                new DelimitedInputField("Name", "name"),
                new DelimitedInputField("Note", "note")
            ]);
        var module = new InputRecordModule();
        var compiled = Compile(module, layout, DelimitedSourceTable());
        const string contents =
            "export generated at 10:00\r\n" +
            "do not import\r\n" +
            "Name,Note\r\n" +
            "First,\"line one\r\nline two\"\r\n" +
            "Second, value \r\n";
        await using var stream = Utf8Stream(contents);

        var result = await module.ReadAsync(stream, compiled);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Collection(
            result.Records,
            first =>
            {
                Assert.Equal(InputRecordAction.Import, first.Action);
                Assert.Equal(1, first.RowNumber);
                Assert.Equal(4, first.PhysicalLineNumber);
                Assert.Equal("First", first.Values["name"]);
                Assert.Equal("line one\r\nline two", first.Values["note"]);
            },
            second =>
            {
                Assert.Equal(2, second.RowNumber);
                Assert.Equal(6, second.PhysicalLineNumber);
                Assert.Equal("Second", second.Values["name"]);
                Assert.Equal(" value ", second.Values["note"]);
            });
    }

    [Fact]
    public async Task ReadAsync_slices_golden_fixed_width_records_after_decoding_utf8()
    {
        var module = new InputRecordModule();
        var compiled = Compile(module, GoldenLayout(), GoldenSourceTable());
        var contents = string.Join(
            '\n',
            Header,
            ThaiDetail,
            AsciiDetail,
            Trailer);
        await using var stream = Utf8Stream(contents);

        var result = await module.ReadAsync(stream, compiled);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Records.Count(record =>
            record.Action == InputRecordAction.Import));
        Assert.Equal(2, result.Records.Count(record =>
            record.Action == InputRecordAction.Ignore));
        var imports = result.Records
            .Where(record => record.Action == InputRecordAction.Import)
            .ToArray();
        Assert.Collection(
            imports,
            first =>
            {
                Assert.Equal(1, first.RowNumber);
                Assert.Equal(2, first.PhysicalLineNumber);
                Assert.Equal("D", first.Values["RecordType"]);
                Assert.Equal("MTI ", first.Values["PartnerCode"]);
                Assert.Equal("บริษัท  ", first.Values["OwnerName"]);
                Assert.Equal("    ", first.Values["Padding"]);
                Assert.Equal("20261006", first.Values["RawDate"]);
                Assert.Equal("TAIL-A ", first.Values["Suffix"]);
                Assert.Equal(32, ThaiDetail.Length);
                Assert.Equal(44, Encoding.UTF8.GetByteCount(ThaiDetail));
            },
            second =>
            {
                Assert.Equal(2, second.RowNumber);
                Assert.Equal(3, second.PhysicalLineNumber);
                Assert.Equal("PMIB", second.Values["PartnerCode"]);
                Assert.Equal("ASCII   ", second.Values["OwnerName"]);
                Assert.Equal("TAIL-B ", second.Values["Suffix"]);
            });
    }

    [Theory]
    [InlineData("\n", false, false)]
    [InlineData("\r\n", true, false)]
    [InlineData("\n", true, true)]
    public async Task ReadAsync_accepts_supported_fixed_width_transports(
        string lineEnding,
        bool finalTerminator,
        bool bom)
    {
        var module = new InputRecordModule();
        var compiled = Compile(module, GoldenLayout(), GoldenSourceTable());
        var text = string.Join(
            lineEnding,
            Header,
            ThaiDetail,
            AsciiDetail,
            Trailer);
        if (finalTerminator)
        {
            text += lineEnding;
        }

        await using var stream = Utf8Stream(text, bom);

        var result = await module.ReadAsync(stream, compiled);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Records.Count);
    }

    [Fact]
    public async Task ReadAsync_accepts_multiple_headers_and_trailers_within_cardinality()
    {
        var module = new InputRecordModule();
        var compiled = Compile(module, GoldenLayout(), GoldenSourceTable());
        var text = string.Join(
            '\n',
            Header,
            "HHEADER-0002",
            ThaiDetail,
            AsciiDetail,
            Trailer,
            "TTRAILER-002");
        await using var stream = Utf8Stream(text);

        var result = await module.ReadAsync(stream, compiled);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Records.Count(record =>
            record.Action == InputRecordAction.Ignore));
        Assert.Equal(
            [3, 4],
            result.Records
                .Where(record => record.Action == InputRecordAction.Import)
                .Select(record => record.PhysicalLineNumber));
    }

    [Theory]
    [MemberData(nameof(InvalidFixedWidthCases))]
    public async Task ReadAsync_returns_stable_fixed_width_error(
        byte[] contents,
        InputRecordReadError expectedError)
    {
        var module = new InputRecordModule();
        var compiled = Compile(module, GoldenLayout(), GoldenSourceTable());
        await using var stream = new MemoryStream(contents);

        var result = await module.ReadAsync(stream, compiled);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Records);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public async Task ReadAsync_supports_at_least_115_fixed_width_fields()
    {
        var fields = Enumerable.Range(1, 115)
            .Select(index => new FixedWidthInputField(
                $"field_{index}",
                Start: index + 1,
                Length: 1))
            .ToArray();
        var columns = fields
            .Select((field, index) => new ColumnDefinition(
                field.SourceColumn,
                ColumnDataType.Text,
                IsRequired: false,
                Ordinal: index + 1))
            .ToArray();
        var layout = new FixedWidthInputLayout(
            "utf-8",
            "utf16CodeUnit",
            [
                new FixedWidthRecordRule(
                    "detail",
                    new FixedWidthDiscriminator(1, 1, "D"),
                    InputRecordAction.Import,
                    ExpectedLength: 116,
                    fields)
            ],
            [new InputRecordSequenceItem("detail", 1, 1)],
            UnmatchedRecordPolicy.Error);
        var module = new InputRecordModule();
        var compiled = Compile(
            module,
            layout,
            new TableDefinition("src_capacity", TableKind.Source, columns));
        var recordText = "D" + new string('X', 115);
        await using var stream = Utf8Stream(recordText);

        var result = await module.ReadAsync(stream, compiled);

        Assert.True(result.IsSuccess);
        var record = Assert.Single(result.Records);
        Assert.Equal("X", record.Values["field_1"]);
        Assert.Equal("X", record.Values["field_58"]);
        Assert.Equal("X", record.Values["field_115"]);
    }

    public static TheoryData<byte[], InputRecordReadError>
        InvalidFixedWidthCases()
    {
        return new TheoryData<byte[], InputRecordReadError>
        {
            {
                [0x48, 0xFF, 0x0A],
                new InputRecordReadError(
                    "InvalidEncoding",
                    1,
                    ByteOffset: 1)
            },
            {
                Bytes(Header, "DMTI 😀BCDEFG    20261006TAIL-A ", Trailer),
                new InputRecordReadError(
                    "UnsupportedCharacterForPositionUnit",
                    2,
                    Position: 6,
                    Scalar: "U+1F600")
            },
            {
                Bytes(Header, "XPMIBASCII       20261007TAIL-B ", Trailer),
                new InputRecordReadError(
                    "UnknownRecordType",
                    2,
                    Discriminator: "X")
            },
            {
                Bytes(Header, "DPMIBASCII       20261007TAIL-B", Trailer),
                new InputRecordReadError(
                    "UnexpectedRecordLength",
                    2,
                    "detail",
                    Expected: 32,
                    Actual: 31)
            },
            {
                Bytes(Header, "DPMIBASCII       20261007TAIL-B  ", Trailer),
                new InputRecordReadError(
                    "UnexpectedRecordLength",
                    2,
                    "detail",
                    Expected: 32,
                    Actual: 33)
            },
            {
                Bytes(Header, Trailer, AsciiDetail),
                new InputRecordReadError(
                    "UnexpectedRecordSequence",
                    2,
                    "trailer",
                    ExpectedNext: "detail")
            },
            {
                Bytes(
                    Header,
                    "HHEADER-0002",
                    "HHEADER-0003",
                    AsciiDetail,
                    Trailer),
                new InputRecordReadError(
                    "RecordCardinalityExceeded",
                    3,
                    "header",
                    MaxOccurs: 2)
            },
            {
                Bytes(Header, AsciiDetail),
                new InputRecordReadError(
                    "RecordCardinalityNotMet",
                    2,
                    "trailer",
                    Actual: 0,
                    MinOccurs: 1)
            },
            {
                Bytes(Header, string.Empty, AsciiDetail, Trailer),
                new InputRecordReadError(
                    "UnknownRecordType",
                    2,
                    Discriminator: string.Empty)
            },
            {
                Bytes(Header, "\uFEFF" + AsciiDetail, Trailer),
                new InputRecordReadError(
                    "UnexpectedByteOrderMark",
                    2,
                    Position: 1)
            }
        };
    }

    private static CompiledInputLayout Compile(
        InputRecordModule module,
        InputLayoutDefinition layout,
        TableDefinition sourceTable)
    {
        var result = module.Compile(layout, sourceTable);
        Assert.True(result.IsSuccess);
        return Assert.IsType<CompiledInputLayout>(result.CompiledLayout);
    }

    private static FixedWidthInputLayout GoldenLayout()
    {
        return new FixedWidthInputLayout(
            "utf-8",
            "utf16CodeUnit",
            [
                new FixedWidthRecordRule(
                    "header",
                    new FixedWidthDiscriminator(1, 1, "H"),
                    InputRecordAction.Ignore,
                    12,
                    []),
                new FixedWidthRecordRule(
                    "detail",
                    new FixedWidthDiscriminator(1, 1, "D"),
                    InputRecordAction.Import,
                    32,
                    [
                        new FixedWidthInputField("RecordType", 1, 1),
                        new FixedWidthInputField("PartnerCode", 2, 4),
                        new FixedWidthInputField("OwnerName", 6, 8),
                        new FixedWidthInputField("Padding", 14, 4),
                        new FixedWidthInputField("RawDate", 18, 8),
                        new FixedWidthInputField("Suffix", 26, 7)
                    ]),
                new FixedWidthRecordRule(
                    "trailer",
                    new FixedWidthDiscriminator(1, 1, "T"),
                    InputRecordAction.Ignore,
                    12,
                    [])
            ],
            [
                new InputRecordSequenceItem("header", 1, 2),
                new InputRecordSequenceItem("detail", 1, null),
                new InputRecordSequenceItem("trailer", 1, 2)
            ],
            UnmatchedRecordPolicy.Error);
    }

    private static TableDefinition GoldenSourceTable()
    {
        string[] names =
        [
            "RecordType",
            "PartnerCode",
            "OwnerName",
            "Padding",
            "RawDate",
            "Suffix"
        ];
        return new TableDefinition(
            "src_fixed_width",
            TableKind.Source,
            names.Select((name, index) => new ColumnDefinition(
                    name,
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: index + 1))
                .ToArray());
    }

    private static TableDefinition DelimitedSourceTable()
    {
        return new TableDefinition(
            "src_delimited",
            TableKind.Source,
            [
                new ColumnDefinition("name", ColumnDataType.Text, false, 1),
                new ColumnDefinition("note", ColumnDataType.Text, false, 2)
            ]);
    }

    private static MemoryStream Utf8Stream(string text, bool bom = false)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        if (!bom)
        {
            return new MemoryStream(payload);
        }

        return new MemoryStream([.. Encoding.UTF8.Preamble, .. payload]);
    }

    private static byte[] Bytes(params string[] records)
    {
        return Encoding.UTF8.GetBytes(string.Join('\n', records));
    }
}
