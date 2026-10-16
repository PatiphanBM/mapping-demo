using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Input;

public sealed class InputRecordModuleTests
{
    [Fact]
    public void Compile_accepts_delimited_layout_for_existing_csv_mapping()
    {
        var layout = new DelimitedInputLayout(
            Encoding: "utf-8",
            Delimiter: ",",
            PreambleLines: 0,
            HeaderPolicy: DelimitedHeaderPolicy.SingleRow,
            Fields:
            [
                new DelimitedInputField("Order No", "order_no"),
                new DelimitedInputField("Amount", "amount")
            ]);

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CompiledLayout);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Compile_rejects_unknown_delimited_encoding_at_exact_path()
    {
        var layout = ValidDelimitedLayout() with
        {
            Encoding = "windows-874"
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.False(result.IsSuccess);
        Assert.Null(result.CompiledLayout);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("encoding", diagnostic.Path);
        Assert.Equal("UnknownEncoding", diagnostic.Code);
        Assert.Equal(InputLayoutDiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Compile_reports_all_invalid_delimited_fields_at_exact_paths()
    {
        var layout = ValidDelimitedLayout() with
        {
            PreambleLines = -1,
            Fields =
            [
                new DelimitedInputField("Order No", "order_no"),
                new DelimitedInputField("Order No", "missing_column"),
                new DelimitedInputField("Amount", "order_no")
            ]
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.False(result.IsSuccess);
        Assert.Collection(
            result.Diagnostics,
            diagnostic => AssertDiagnostic(
                diagnostic,
                "preambleLines",
                "InvalidPreambleLines"),
            diagnostic => AssertDiagnostic(
                diagnostic,
                "fields[1].sourceColumn",
                "SourceColumnNotFound"),
            diagnostic => AssertDiagnostic(
                diagnostic,
                "fields[1].inputHeader",
                "DuplicateInputHeader"),
            diagnostic => AssertDiagnostic(
                diagnostic,
                "fields[2].sourceColumn",
                "DuplicateSourceColumn"));
    }

    [Fact]
    public void Compile_rejects_incomplete_delimited_structure()
    {
        var layout = ValidDelimitedLayout() with
        {
            Delimiter = "\r\n",
            HeaderPolicy = (DelimitedHeaderPolicy)999,
            Fields = []
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.False(result.IsSuccess);
        AssertHasDiagnostic(result, "delimiter", "InvalidDelimiter");
        AssertHasDiagnostic(
            result,
            "headerPolicy",
            "UnknownHeaderPolicy");
        AssertHasDiagnostic(result, "fields", "InputFieldsRequired");
    }

    [Fact]
    public void Compile_rejects_unknown_fixed_width_encoding_and_position_unit()
    {
        var layout = ValidFixedWidthLayout() with
        {
            Encoding = "windows-874",
            PositionUnit = "byte"
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.False(result.IsSuccess);
        Assert.Collection(
            result.Diagnostics,
            diagnostic => AssertDiagnostic(
                diagnostic,
                "encoding",
                "UnknownEncoding"),
            diagnostic => AssertDiagnostic(
                diagnostic,
                "positionUnit",
                "UnknownPositionUnit"));
    }

    [Fact]
    public void Compile_rejects_invalid_fixed_width_structure()
    {
        var validRule = ValidFixedWidthLayout().RecordRules.Single();
        var layout = ValidFixedWidthLayout() with
        {
            RecordRules =
            [
                validRule,
                new FixedWidthRecordRule(
                    "detail",
                    new FixedWidthDiscriminator(1, 1, "X"),
                    (InputRecordAction)999,
                    ExpectedLength: 20,
                    Fields: [])
            ],
            RecordSequence = [],
            UnmatchedRecordPolicy = (UnmatchedRecordPolicy)999
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.False(result.IsSuccess);
        AssertHasDiagnostic(
            result,
            "recordRules[1].key",
            "DuplicateRecordRuleKey");
        AssertHasDiagnostic(
            result,
            "recordRules[1].action",
            "UnknownRecordAction");
        AssertHasDiagnostic(
            result,
            "recordSequence",
            "RecordSequenceRequired");
        AssertHasDiagnostic(
            result,
            "unmatchedRecordPolicy",
            "UnknownUnmatchedRecordPolicy");
    }

    [Fact]
    public void Compile_rejects_invalid_fixed_width_field_slices()
    {
        var validRule = ValidFixedWidthLayout().RecordRules.Single();
        var layout = ValidFixedWidthLayout() with
        {
            RecordRules =
            [
                validRule with
                {
                    Fields =
                    [
                        new FixedWidthInputField("order_no", 0, 5),
                        new FixedWidthInputField("amount", 2, 0),
                        new FixedWidthInputField("missing_column", 15, 10),
                        new FixedWidthInputField("order_no", 2, 10)
                    ]
                }
            ]
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.False(result.IsSuccess);
        AssertHasDiagnostic(
            result,
            "recordRules[0].fields[0].start",
            "InvalidSliceStart");
        AssertHasDiagnostic(
            result,
            "recordRules[0].fields[1].length",
            "InvalidSliceLength");
        AssertHasDiagnostic(
            result,
            "recordRules[0].fields[2].sourceColumn",
            "SourceColumnNotFound");
        AssertHasDiagnostic(
            result,
            "recordRules[0].fields[2]",
            "SliceExceedsExpectedLength");
        AssertHasDiagnostic(
            result,
            "recordRules[0].fields[3].sourceColumn",
            "DuplicateSourceColumn");
    }

    [Fact]
    public void Compile_rejects_invalid_record_and_discriminator_geometry()
    {
        var layout = ValidFixedWidthLayout() with
        {
            RecordRules =
            [
                new FixedWidthRecordRule(
                    "empty",
                    new FixedWidthDiscriminator(1, 1, "D"),
                    InputRecordAction.Ignore,
                    ExpectedLength: 0,
                    Fields: []),
                new FixedWidthRecordRule(
                    "bad-discriminator",
                    new FixedWidthDiscriminator(0, 0, "X"),
                    InputRecordAction.Ignore,
                    ExpectedLength: 5,
                    Fields: []),
                new FixedWidthRecordRule(
                    "past-end",
                    new FixedWidthDiscriminator(4, 3, "ABC"),
                    InputRecordAction.Ignore,
                    ExpectedLength: 5,
                    Fields: [])
            ],
            RecordSequence =
            [
                new InputRecordSequenceItem("empty", 0, 1),
                new InputRecordSequenceItem("bad-discriminator", 0, 1),
                new InputRecordSequenceItem("past-end", 0, 1)
            ]
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.False(result.IsSuccess);
        AssertHasDiagnostic(
            result,
            "recordRules[0].expectedLength",
            "InvalidExpectedLength");
        AssertHasDiagnostic(
            result,
            "recordRules[0].discriminator",
            "DiscriminatorExceedsExpectedLength");
        AssertHasDiagnostic(
            result,
            "recordRules[1].discriminator.start",
            "InvalidSliceStart");
        AssertHasDiagnostic(
            result,
            "recordRules[1].discriminator.length",
            "InvalidSliceLength");
        AssertHasDiagnostic(
            result,
            "recordRules[1].discriminator.value",
            "DiscriminatorLengthMismatch");
        AssertHasDiagnostic(
            result,
            "recordRules[2].discriminator",
            "DiscriminatorExceedsExpectedLength");
    }

    [Fact]
    public void Compile_rejects_discriminators_that_can_match_the_same_record()
    {
        var layout = ValidFixedWidthLayout() with
        {
            RecordRules =
            [
                new FixedWidthRecordRule(
                    "first",
                    new FixedWidthDiscriminator(1, 1, "H"),
                    InputRecordAction.Ignore,
                    ExpectedLength: 5,
                    Fields: []),
                new FixedWidthRecordRule(
                    "second",
                    new FixedWidthDiscriminator(2, 1, "D"),
                    InputRecordAction.Ignore,
                    ExpectedLength: 5,
                    Fields: [])
            ],
            RecordSequence =
            [
                new InputRecordSequenceItem("first", 0, 1),
                new InputRecordSequenceItem("second", 0, 1)
            ]
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.False(result.IsSuccess);
        AssertHasDiagnostic(
            result,
            "recordRules[1].discriminator",
            "AmbiguousDiscriminator");
    }

    [Fact]
    public void Compile_rejects_invalid_record_sequence_at_exact_paths()
    {
        var validRule = ValidFixedWidthLayout().RecordRules.Single();
        var layout = ValidFixedWidthLayout() with
        {
            RecordRules =
            [
                validRule,
                new FixedWidthRecordRule(
                    "orphan",
                    new FixedWidthDiscriminator(1, 1, "X"),
                    InputRecordAction.Ignore,
                    ExpectedLength: 20,
                    Fields: [])
            ],
            RecordSequence =
            [
                new InputRecordSequenceItem("missing", -1, -2),
                new InputRecordSequenceItem("detail", 1, 0),
                new InputRecordSequenceItem("detail", 0, 1)
            ]
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.False(result.IsSuccess);
        AssertHasDiagnostic(
            result,
            "recordSequence[0].recordRule",
            "RecordRuleNotFound");
        AssertHasDiagnostic(
            result,
            "recordSequence[0].minOccurs",
            "InvalidMinOccurs");
        AssertHasDiagnostic(
            result,
            "recordSequence[0].maxOccurs",
            "InvalidMaxOccurs");
        AssertHasDiagnostic(
            result,
            "recordSequence[1].maxOccurs",
            "MaxOccursLessThanMinOccurs");
        AssertHasDiagnostic(
            result,
            "recordSequence[2].recordRule",
            "DuplicateRecordRuleReference");
        AssertHasDiagnostic(
            result,
            "recordRules[1].key",
            "RecordRuleNotReferenced");
    }

    [Fact]
    public void Compile_reports_gap_and_overlap_as_warnings_without_failing()
    {
        var validRule = ValidFixedWidthLayout().RecordRules.Single();
        var layout = ValidFixedWidthLayout() with
        {
            RecordRules =
            [
                validRule with
                {
                    Fields =
                    [
                        new FixedWidthInputField("order_no", 2, 5),
                        new FixedWidthInputField("amount", 5, 3),
                        new FixedWidthInputField("note", 10, 2)
                    ]
                }
            ]
        };

        var result = new InputRecordModule().Compile(
            layout,
            SourceTable());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CompiledLayout);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Path ==
                              "recordRules[0].fields[1]" &&
                          diagnostic.Code == "OverlappingFieldSlice" &&
                          diagnostic.Severity ==
                              InputLayoutDiagnosticSeverity.Warning);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Path ==
                              "recordRules[0].fields[2]" &&
                          diagnostic.Code == "GappedFieldSlice" &&
                          diagnostic.Severity ==
                              InputLayoutDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Compile_accepts_compact_fixed_width_layout()
    {
        var result = new InputRecordModule().Compile(
            ValidFixedWidthLayout(),
            SourceTable());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CompiledLayout);
        Assert.Empty(result.Diagnostics);
    }

    private static DelimitedInputLayout ValidDelimitedLayout()
    {
        return new DelimitedInputLayout(
            Encoding: "utf-8",
            Delimiter: ",",
            PreambleLines: 0,
            HeaderPolicy: DelimitedHeaderPolicy.SingleRow,
            Fields:
            [
                new DelimitedInputField("Order No", "order_no"),
                new DelimitedInputField("Amount", "amount")
            ]);
    }

    private static FixedWidthInputLayout ValidFixedWidthLayout()
    {
        return new FixedWidthInputLayout(
            Encoding: "utf-8",
            PositionUnit: "utf16CodeUnit",
            RecordRules:
            [
                new FixedWidthRecordRule(
                    "detail",
                    new FixedWidthDiscriminator(1, 1, "D"),
                    InputRecordAction.Import,
                    ExpectedLength: 20,
                    Fields:
                    [
                        new FixedWidthInputField("order_no", 2, 10),
                        new FixedWidthInputField("amount", 12, 9)
                    ])
            ],
            RecordSequence:
            [
                new InputRecordSequenceItem(
                    "detail",
                    MinOccurs: 1,
                    MaxOccurs: null)
            ],
            UnmatchedRecordPolicy: UnmatchedRecordPolicy.Error);
    }

    private static TableDefinition SourceTable()
    {
        return new TableDefinition(
            "src_orders",
            TableKind.Source,
            [
                new ColumnDefinition(
                    "order_no",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 1),
                new ColumnDefinition(
                    "amount",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 2),
                new ColumnDefinition(
                    "note",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 3)
            ]);
    }

    private static void AssertDiagnostic(
        InputLayoutDiagnostic diagnostic,
        string path,
        string code)
    {
        Assert.Equal(path, diagnostic.Path);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal(InputLayoutDiagnosticSeverity.Error, diagnostic.Severity);
    }

    private static void AssertHasDiagnostic(
        InputLayoutCompilationResult result,
        string path,
        string code)
    {
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Path == path &&
                          diagnostic.Code == code &&
                          diagnostic.Severity ==
                              InputLayoutDiagnosticSeverity.Error);
    }
}
