using System.Text.Json;
using MappingDemo.Api;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MappingDemo.Tests.Transformation;

public sealed class AddConcatExpressionTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Add_and_concat_round_trip_through_the_value_expression_contract()
    {
        const string addJson = """
            {
              "kind": "add",
              "values": [
                { "kind": "literal", "type": "integer", "value": 1 },
                { "kind": "literal", "type": "decimal", "value": 2.5 }
              ]
            }
            """;
        const string concatJson = """
            {
              "kind": "concat",
              "values": [
                { "kind": "literal", "type": "text", "value": "ไทย" },
                { "kind": "literal", "type": "text", "value": "文" }
              ],
              "separator": " / "
            }
            """;

        var add = JsonSerializer.Deserialize<ValueExpression>(addJson, JsonOptions);
        var concat = JsonSerializer.Deserialize<ValueExpression>(concatJson, JsonOptions);

        Assert.IsType<AddExpression>(add);
        Assert.IsType<ConcatExpression>(concat);
        Assert.IsType<AddExpression>(JsonSerializer.Deserialize<ValueExpression>(
            JsonSerializer.Serialize(add, JsonOptions),
            JsonOptions));
        Assert.IsType<ConcatExpression>(JsonSerializer.Deserialize<ValueExpression>(
            JsonSerializer.Serialize(concat, JsonOptions),
            JsonOptions));
    }

    [Theory]
    [InlineData("{ \"kind\": \"add\", \"values\": [], \"extra\": true }")]
    [InlineData("{ \"kind\": \"add\" }")]
    [InlineData("{ \"kind\": \"concat\", \"values\": [] }")]
    [InlineData("{ \"kind\": \"concat\", \"values\": [], \"separator\": \"\", \"extra\": true }")]
    [InlineData("{ \"kind\": \"unknownAdd\", \"values\": [] }")]
    public void Add_and_concat_reject_unknown_missing_and_extra_fields(string json)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ValueExpression>(json, JsonOptions));
    }

    [Fact]
    public void OpenApi_value_expression_union_includes_add_and_concat()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSwaggerGen(OpenApiConfiguration.Configure);
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<ISchemaGenerator>();
        var repository = new SchemaRepository();

        var schema = generator.GenerateSchema(
            typeof(ValueExpression),
            repository);

        Assert.NotNull(schema.OneOf);
        Assert.Contains("AddExpression", repository.Schemas.Keys);
        Assert.Contains("ConcatExpression", repository.Schemas.Keys);
    }

    [Fact]
    public void Compiler_infers_add_promotion_and_concat_result_types()
    {
        var program = Program(
            Assignment("integer_result", new AddExpression
            {
                Values = [Integer(1), Integer(2)]
            }),
            Assignment("decimal_result", new AddExpression
            {
                Values = [Integer(1), Decimal(2.5m)]
            }),
            Assignment("text_result", new ConcatExpression
            {
                Values = [Text("ก"), Text("ข")],
                Separator = "-"
            }));

        var result = new TransformationModule().Compile(program, Schema());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Compiler_rejects_short_and_mistyped_operands_at_exact_paths()
    {
        var program = Program(
            Assignment("integer_result", new AddExpression
            {
                Values = [Integer(1)]
            }),
            Assignment("decimal_result", new AddExpression
            {
                Values = [Integer(1), Text("2")]
            }),
            Assignment("text_result", new ConcatExpression
            {
                Values = [Text("a"), Integer(2)],
                Separator = ""
            }),
            Assignment("nullable_text", new ConcatExpression
            {
                Values = [Text("a"), Text("b")],
                Separator = null!
            }));

        var result = new TransformationModule().Compile(program, Schema());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Path == "outputs[0].fields[0].value.values" &&
            diagnostic.Code == "AddOperandsRequired");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Path == "outputs[0].fields[1].value.values[1]" &&
            diagnostic.Code == "NumericValueRequired");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Path == "outputs[0].fields[2].value.values[1]" &&
            diagnostic.Code == "TextValueRequired");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Path == "outputs[0].fields[3].value.separator" &&
            diagnostic.Code == "ConcatSeparatorRequired");
    }

    [Fact]
    public void Compiler_counts_add_operands_for_dependencies_and_limits()
    {
        var sourceSchema = TransformationSchema.FromLegacyTable(
            new TableDefinition(
                "source_rows",
                TableKind.Source,
                [
                    new ColumnDefinition("left", ColumnDataType.Text, false, 1),
                    new ColumnDefinition("right", ColumnDataType.Text, false, 2)
                ]),
            new TableDefinition(
                "normalized_rows",
                TableKind.Normalized,
                [
                    new ColumnDefinition(
                        "decimal_result",
                        ColumnDataType.Decimal,
                        false,
                        1)
                ]));
        var program = Program(Assignment("decimal_result", new AddExpression
        {
            Values =
            [
                Convert("left", ExpressionValueType.Decimal),
                Convert("right", ExpressionValueType.Decimal)
            ]
        }));
        var module = new TransformationModule();

        var compilation = module.Compile(program, sourceSchema);

        Assert.True(compilation.IsSuccess);
        Assert.Equal(
            ["left", "right"],
            module.GetSourceDependencies(compilation.CompiledProgram!));

        var overCollectionLimit = Program(Assignment(
            "integer_result",
            new AddExpression
            {
                Values = Enumerable.Range(
                        0,
                        TransformationCapabilities.MaxCollectionItems + 1)
                    .Select(value => Integer(value))
                    .Cast<ValueExpression>()
                    .ToArray()
            }));
        var collectionResult = module.Compile(overCollectionLimit, Schema());
        Assert.Contains(collectionResult.Diagnostics, diagnostic =>
            diagnostic.Path == "outputs[0].fields[0].value.values" &&
            diagnostic.Code == "CollectionLimitExceeded");
    }

    [Fact]
    public void Compiler_counts_nested_add_nodes_and_depth_at_operand_paths()
    {
        ValueExpression deep = Integer(1);
        for (var index = 0;
             index < TransformationCapabilities.MaxExpressionDepth;
             index++)
        {
            deep = new AddExpression { Values = [Integer(0), deep] };
        }

        var depthResult = new TransformationModule().Compile(
            Program(Assignment("integer_result", deep)),
            Schema());
        var expectedDepthPath = "outputs[0].fields[0].value" + string.Concat(
            Enumerable.Repeat(
                ".values[1]",
                TransformationCapabilities.MaxExpressionDepth));
        Assert.Contains(depthResult.Diagnostics, diagnostic =>
            diagnostic.Path == expectedDepthPath &&
            diagnostic.Code == "ExpressionDepthExceeded");

        var wideValues = Enumerable.Range(0, 9)
            .Select(_ => (ValueExpression)new AddExpression
            {
                Values = Enumerable.Range(
                        0,
                        TransformationCapabilities.MaxCollectionItems)
                    .Select(value => Integer(value))
                    .Cast<ValueExpression>()
                    .ToArray()
            })
            .ToArray();
        var nodeResult = new TransformationModule().Compile(
            Program(Assignment(
                "integer_result",
                new AddExpression { Values = wideValues })),
            Schema());
        Assert.Contains(nodeResult.Diagnostics, diagnostic =>
            diagnostic.Path.StartsWith(
                "outputs[0].fields[0].value.values[",
                StringComparison.Ordinal) &&
            diagnostic.Code == "ExpressionNodeLimitExceeded");
    }

    [Fact]
    public async Task Evaluator_returns_typed_add_and_unicode_concat_results()
    {
        var program = Program(
            Assignment("integer_result", new AddExpression
            {
                Values =
                [
                    Integer(-2),
                    Integer(0),
                    new AddExpression
                    {
                        Values = [Integer(1), Integer(3)]
                    }
                ]
            }),
            Assignment("decimal_result", new AddExpression
            {
                Values = [Integer(1), Decimal(2.5m)]
            }),
            Assignment("text_result", new ConcatExpression
            {
                Values = [Text("ไทย"), Text("文")],
                Separator = " / "
            }),
            Assignment("nullable_text", new ConcatExpression
            {
                Values = [Text("ไทย"), Text("文")],
                Separator = ""
            }));

        var row = await EvaluateAsync(program);

        Assert.Equal(2, Assert.IsType<int>(row.Outputs[0].Values["integer_result"]));
        Assert.Equal(
            3.5m,
            Assert.IsType<decimal>(row.Outputs[0].Values["decimal_result"]));
        Assert.Equal("ไทย / 文", row.Outputs[0].Values["text_result"]);
        Assert.Equal("ไทย文", row.Outputs[0].Values["nullable_text"]);
    }

    [Fact]
    public async Task Evaluator_propagates_null_and_coalesce_must_supply_defaults()
    {
        var nullableAdd = new AddExpression
        {
            Values = [Integer(null), Integer(2)]
        };
        var program = Program(
            Assignment("integer_result", nullableAdd),
            Assignment("decimal_result", new CoalesceExpression
            {
                Values =
                [
                    new AddExpression
                    {
                        Values = [Decimal(null), Integer(2)]
                    },
                    Decimal(7m)
                ]
            }),
            Assignment("text_result", new ConcatExpression
            {
                Values = [Text("ไทย"), Text(null)],
                Separator = ""
            }));

        var row = await EvaluateAsync(program);

        Assert.Null(row.Outputs[0].Values["integer_result"]);
        Assert.Equal(7m, row.Outputs[0].Values["decimal_result"]);
        Assert.Null(row.Outputs[0].Values["text_result"]);
    }

    [Fact]
    public async Task Evaluator_reports_checked_integer_and_decimal_overflow()
    {
        var program = Program(
            Assignment("integer_result", new AddExpression
            {
                Values = [Integer(int.MaxValue), Integer(1)]
            }),
            Assignment("decimal_result", new AddExpression
            {
                Values = [Decimal(decimal.MaxValue), Decimal(1m)]
            }));

        var row = await EvaluateAsync(program);

        Assert.Equal(TransformationRowStatus.Invalid, row.Status);
        Assert.Contains(row.Errors, error =>
            error.ExpressionPath ==
                "outputs[0].fields[0].value.values[1]" &&
            error.Reason == "IntegerOverflow");
        Assert.Contains(row.Errors, error =>
            error.ExpressionPath ==
                "outputs[0].fields[1].value.values[1]" &&
            error.Reason == "DecimalOverflow");
    }

    private static TransformationProgram Program(params FieldAssignment[] fields) =>
        new()
        {
            SemanticsProfile = TransformationCapabilities.SqlServerLegacyProfile,
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    Fields = fields
                }
            ]
        };

    private static FieldAssignment Assignment(
        string target,
        ValueExpression value) =>
        new()
        {
            NormalizedColumn = target,
            Value = value
        };

    private static TransformationSchema Schema() =>
        TransformationSchema.FromLegacyTable(
            new TableDefinition(
                "source_rows",
                TableKind.Source,
                [new ColumnDefinition("raw", ColumnDataType.Text, false, 1)]),
            new TableDefinition(
                "normalized_rows",
                TableKind.Normalized,
                [
                    new ColumnDefinition(
                        "integer_result",
                        ColumnDataType.Integer,
                        false,
                        1),
                    new ColumnDefinition(
                        "decimal_result",
                        ColumnDataType.Decimal,
                        false,
                        2),
                    new ColumnDefinition(
                        "text_result",
                        ColumnDataType.Text,
                        false,
                        3),
                    new ColumnDefinition(
                        "nullable_text",
                        ColumnDataType.Text,
                        false,
                        4)
                ]));

    private static async Task<TransformationRowResult> EvaluateAsync(
        TransformationProgram program)
    {
        var module = new TransformationModule();
        var compilation = module.Compile(program, Schema());
        Assert.True(compilation.IsSuccess);
        var result = await module.EvaluateAsync(
            compilation.CompiledProgram!,
            new TransformationBatch(
            [
                new TransformationSourceRow(
                    "row-1",
                    new Dictionary<string, string?> { ["raw"] = "unused" },
                    new Dictionary<string, object?>(),
                    DateTimeOffset.UnixEpoch)
            ]));
        return Assert.Single(result.Rows);
    }

    private static LiteralExpression Integer(int? value) =>
        Literal(ExpressionValueType.Integer, value);

    private static LiteralExpression Decimal(decimal? value) =>
        Literal(ExpressionValueType.Decimal, value);

    private static LiteralExpression Text(string? value) =>
        Literal(ExpressionValueType.Text, value);

    private static LiteralExpression Literal(
        ExpressionValueType type,
        object? value) =>
        new()
        {
            Type = type,
            Value = JsonSerializer.SerializeToElement(
                value,
                value?.GetType() ?? typeof(object))
        };

    private static ConvertExpression Convert(
        string field,
        ExpressionValueType type) =>
        new()
        {
            Value = new SourceExpression { Field = field },
            Type = type
        };
}
