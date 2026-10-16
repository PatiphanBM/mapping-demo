using System.Text.Json;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.Transformation;

public sealed class TransformationCompilerTests
{
    [Fact]
    public void Compile_resolves_default_language_version_and_known_profile()
    {
        var result = Compile(Source("raw"), Target("value", ColumnDataType.Text));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(null, "SemanticsProfileRequired")]
    [InlineData("", "SemanticsProfileRequired")]
    [InlineData("unknown-v1", "UnknownSemanticsProfile")]
    public void Compile_rejects_missing_or_unknown_profile(
        string? profile,
        string expectedCode)
    {
        var program = Program(Source("raw")) with
        {
            SemanticsProfile = profile!
        };

        var result = Compile(
            program,
            SourceTable(),
            Target("value", ColumnDataType.Text));

        AssertDiagnostic(result, "semanticsProfile", expectedCode);
    }

    [Fact]
    public void Compile_rejects_unknown_language_version()
    {
        var program = Program(Source("raw")) with { LanguageVersion = 2 };

        var result = Compile(
            program,
            SourceTable(),
            Target("value", ColumnDataType.Text));

        AssertDiagnostic(result, "languageVersion", "UnknownLanguageVersion");
    }

    [Theory]
    [InlineData(ColumnDataType.Text)]
    [InlineData(ColumnDataType.Date)]
    [InlineData(ColumnDataType.DateTime)]
    [InlineData(ColumnDataType.Decimal)]
    [InlineData(ColumnDataType.Integer)]
    [InlineData(ColumnDataType.Boolean)]
    [InlineData(ColumnDataType.Guid)]
    public void Compile_accepts_implicit_text_assignment_coercion(
        ColumnDataType targetType)
    {
        var result = Compile(Source("raw"), Target("value", targetType));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(TypedLiterals))]
    public void Compile_accepts_already_typed_assignment_without_conversion(
        ColumnDataType targetType,
        ExpressionValueType expressionType,
        object value)
    {
        var result = Compile(
            Literal(expressionType, value),
            Target("value", targetType));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Compile_does_not_mutate_program_when_adding_assignment_coercion()
    {
        var program = Program(
            Source("raw"),
            inputFormat: "yyyyMMdd");
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var before = JsonSerializer.Serialize(program, jsonOptions);

        var result = Compile(
            program,
            SourceTable(),
            Target("value", ColumnDataType.Date));

        var after = JsonSerializer.Serialize(program, jsonOptions);
        Assert.True(result.IsSuccess);
        Assert.Equal(before, after);
        Assert.IsType<SourceExpression>(program.Outputs[0].Fields[0].Value);
    }

    [Theory]
    [InlineData(ColumnDataType.Date, "yyyyMMdd")]
    [InlineData(ColumnDataType.DateTime, "yyyy-MM-ddTHH:mm:ss")]
    public void Compile_accepts_input_format_for_text_to_temporal_assignment(
        ColumnDataType targetType,
        string inputFormat)
    {
        var program = Program(
            Source("raw"),
            inputFormat);

        var result = Compile(program, SourceTable(), Target("value", targetType));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Compile_rejects_input_format_for_non_temporal_target()
    {
        var program = Program(Source("raw"), inputFormat: "0.00");

        var result = Compile(
            program,
            SourceTable(),
            Target("value", ColumnDataType.Decimal));

        AssertDiagnostic(
            result,
            "outputs[0].fields[0].inputFormat",
            "InputFormatNotSupported");
    }

    [Fact]
    public void Compile_rejects_input_format_for_already_typed_value()
    {
        var program = Program(
            Literal(ExpressionValueType.Date, "2026-10-12"),
            inputFormat: "yyyy-MM-dd");

        var result = Compile(
            program,
            SourceTable(),
            Target("value", ColumnDataType.Date));

        AssertDiagnostic(
            result,
            "outputs[0].fields[0].inputFormat",
            "InputFormatRequiresText");
    }

    [Fact]
    public void Compile_rejects_unknown_source_field_at_exact_path()
    {
        var result = Compile(
            Source("missing"),
            Target("value", ColumnDataType.Text));

        AssertDiagnostic(
            result,
            "outputs[0].fields[0].value.field",
            "UnknownSourceField");
    }

    [Fact]
    public void Compile_rejects_trim_of_non_text_at_exact_path()
    {
        var expression = new TrimExpression
        {
            Value = Literal(ExpressionValueType.Integer, 1)
        };

        var result = Compile(expression, Target("value", ColumnDataType.Text));

        AssertDiagnostic(
            result,
            "outputs[0].fields[0].value.value",
            "TextValueRequired");
    }

    [Fact]
    public void Compile_rejects_assignment_type_mismatch()
    {
        var result = Compile(
            Literal(ExpressionValueType.Boolean, true),
            Target("value", ColumnDataType.Date));

        AssertDiagnostic(
            result,
            "outputs[0].fields[0].value",
            "AssignmentTypeMismatch");
    }

    [Fact]
    public void Compile_rejects_missing_required_target()
    {
        var target = new TableDefinition(
            "norm_rows",
            TableKind.Normalized,
            [
                new ColumnDefinition("value", ColumnDataType.Text, false, 1),
                new ColumnDefinition("required_value", ColumnDataType.Text, true, 2)
            ]);

        var result = Compile(Source("raw"), target);

        AssertDiagnostic(
            result,
            "outputs[0].fields",
            "RequiredTargetMissing");
    }

    [Fact]
    public void Compile_rejects_duplicate_output_key()
    {
        var output = Output("same", "value", Source("raw"));
        var program = new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            Outputs = [output, output]
        };

        var result = Compile(
            program,
            SourceTable(),
            Target("value", ColumnDataType.Text));

        AssertDiagnostic(
            result,
            "outputs[1].outputKey",
            "DuplicateOutputKey");
    }

    [Fact]
    public void Compile_rejects_duplicate_target_in_an_output()
    {
        var program = new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    Fields =
                    [
                        Assignment("value", Source("raw")),
                        Assignment("value", Source("raw"))
                    ]
                }
            ]
        };

        var result = Compile(
            program,
            SourceTable(),
            Target("value", ColumnDataType.Text));

        AssertDiagnostic(
            result,
            "outputs[0].fields[1].normalizedColumn",
            "DuplicateTarget");
    }

    [Fact]
    public void Compile_validates_named_function_and_lookup_catalogs()
    {
        var program = new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            SourceWhen = new ExistsExpression
            {
                Lookup = "unknownLookup@1",
                Arguments = new Dictionary<string, ValueExpression>()
            },
            Outputs =
            [
                Output(
                    "default",
                    "value",
                    new CallExpression
                    {
                        Function = "unknownFunction@1",
                        Arguments = [Source("raw")]
                    })
            ]
        };

        var result = Compile(
            program,
            SourceTable(),
            Target("value", ColumnDataType.Text));

        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Path == "sourceWhen.lookup"
                && diagnostic.Code == "UnknownLookup");
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Path == "outputs[0].fields[0].value.function"
                && diagnostic.Code == "UnknownFunction");
    }

    [Fact]
    public void Compile_accepts_known_function_and_lookup_signatures()
    {
        var program = new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            SourceWhen = new NotExistsExpression
            {
                Lookup = "mtiOriginalPolicyExists@1",
                Arguments = new Dictionary<string, ValueExpression>
                {
                    ["systemId"] = Source("raw"),
                    ["productName"] = Source("raw"),
                    ["loanNumber"] = Source("raw")
                }
            },
            Outputs =
            [
                Output(
                    "default",
                    "value",
                    new CallExpression
                    {
                        Function = "formatRegNo@1",
                        Arguments = [Source("raw")]
                    })
            ]
        };

        var result = Compile(
            program,
            SourceTable(),
            Target("value", ColumnDataType.Text));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Compile_rejects_unregistered_function_version()
    {
        var result = Compile(
            new CallExpression
            {
                Function = "formatRegNo@2",
                Arguments = [Source("raw")]
            },
            Target("value", ColumnDataType.Text));

        AssertDiagnostic(
            result,
            "outputs[0].fields[0].value.function",
            "UnknownFunction");
    }

    [Fact]
    public void Compile_rejects_expression_deeper_than_limit_with_exact_path()
    {
        ValueExpression expression = Source("raw");
        for (var index = 0; index < 33; index++)
        {
            expression = new TrimExpression { Value = expression };
        }

        var result = Compile(expression, Target("value", ColumnDataType.Text));
        var expectedPath = "outputs[0].fields[0].value"
            + string.Concat(Enumerable.Repeat(".value", 32));

        AssertDiagnostic(result, expectedPath, "ExpressionDepthExceeded");
    }

    public static TheoryData<ColumnDataType, ExpressionValueType, object>
        TypedLiterals => new()
        {
            { ColumnDataType.Text, ExpressionValueType.Text, "text" },
            { ColumnDataType.Date, ExpressionValueType.Date, "2026-10-12" },
            {
                ColumnDataType.DateTime,
                ExpressionValueType.DateTime,
                "2026-10-12T10:30:00+07:00"
            },
            { ColumnDataType.Decimal, ExpressionValueType.Decimal, 12.5m },
            { ColumnDataType.Integer, ExpressionValueType.Integer, 12 },
            { ColumnDataType.Boolean, ExpressionValueType.Boolean, true },
            {
                ColumnDataType.Guid,
                ExpressionValueType.Guid,
                "b381a1c8-11f8-42fc-a585-992482b4fcb6"
            }
        };

    private static TransformationCompilationResult Compile(
        ValueExpression expression,
        TableDefinition target)
    {
        return Compile(Program(expression), SourceTable(), target);
    }

    private static TransformationCompilationResult Compile(
        TransformationProgram program,
        TableDefinition source,
        TableDefinition target)
    {
        return new TransformationModule().Compile(
            program,
            new TransformationSchema(source, target));
    }

    private static TransformationProgram Program(
        ValueExpression expression,
        string? inputFormat = null)
    {
        return new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            NormalizedColumn = "value",
                            InputFormat = inputFormat,
                            Value = expression
                        }
                    ]
                }
            ]
        };
    }

    private static TransformationOutput Output(
        string outputKey,
        string target,
        ValueExpression value)
    {
        return new TransformationOutput
        {
            OutputKey = outputKey,
            Fields = [Assignment(target, value)]
        };
    }

    private static FieldAssignment Assignment(
        string target,
        ValueExpression value)
    {
        return new FieldAssignment
        {
            NormalizedColumn = target,
            Value = value
        };
    }

    private static SourceExpression Source(string field)
    {
        return new SourceExpression { Field = field };
    }

    private static LiteralExpression Literal(
        ExpressionValueType type,
        object value)
    {
        return new LiteralExpression
        {
            Type = type,
            Value = JsonSerializer.SerializeToElement(value, value.GetType())
        };
    }

    private static TableDefinition SourceTable()
    {
        return new TableDefinition(
            "src_rows",
            TableKind.Source,
            [new ColumnDefinition("raw", ColumnDataType.Text, false, 1)]);
    }

    private static TableDefinition Target(string name, ColumnDataType type)
    {
        return new TableDefinition(
            "norm_rows",
            TableKind.Normalized,
            [new ColumnDefinition(name, type, false, 1)]);
    }

    private static void AssertDiagnostic(
        TransformationCompilationResult result,
        string expectedPath,
        string expectedCode)
    {
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedPath, diagnostic.Path);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(TransformationDiagnosticSeverity.Error, diagnostic.Severity);
    }
}
