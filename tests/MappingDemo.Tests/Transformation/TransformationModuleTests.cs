using System.Reflection;
using System.Text.Json;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.Transformation;

public sealed class TransformationModuleTests
{
    [Fact]
    public void Legacy_table_adapter_builds_an_immutable_flat_target_tree()
    {
        var columns = new List<ColumnDefinition>
        {
            new("amount", ColumnDataType.Decimal, false, 2, Precision: 18, Scale: 2),
            new("order_no", ColumnDataType.Text, true, 1, Length: 40)
        };
        var table = new TableDefinition(
            "norm_orders",
            TableKind.Normalized,
            columns);

        var schema = new LegacyTableTargetSchemaAdapter().Adapt(table);
        columns.Clear();

        Assert.Equal(TargetNodeKind.Object, schema.Root.Kind);
        Assert.Equal("norm_orders", schema.Root.Name);
        Assert.Empty(schema.Root.PathTokens);
        Assert.Equal(
            new[] { "order_no", "amount" },
            schema.Root.Properties.Select(property => property.Name));

        var orderNo = schema.Root.Properties[0];
        Assert.Equal(TargetNodeKind.Scalar, orderNo.Kind);
        Assert.Equal(TargetScalarType.Text, orderNo.ScalarType);
        Assert.True(orderNo.IsRequired);
        Assert.Equal(new[] { "order_no" }, orderNo.PathTokens);
        Assert.Equal(40, orderNo.Constraints?.Length);

        var amount = schema.Root.Properties[1];
        Assert.Equal(TargetScalarType.Decimal, amount.ScalarType);
        Assert.Equal(18, amount.Constraints?.Precision);
        Assert.Equal(2, amount.Constraints?.Scale);
    }

    [Fact]
    public async Task Transformation_module_uses_a_target_tree_without_legacy_table_knowledge()
    {
        var source = new TableDefinition(
            "src_orders",
            TableKind.Source,
            [new ColumnDefinition("raw_order_no", ColumnDataType.Text, true, 1)]);
        var targetSchema = new TargetSchema(
            new TargetNode(
                "contract",
                TargetNodeKind.Object,
                isRequired: true,
                pathTokens: [],
                properties:
                [
                    new TargetNode(
                        "order_no",
                        TargetNodeKind.Scalar,
                        isRequired: true,
                        pathTokens: ["order_no"],
                        scalarType: TargetScalarType.Text)
                ]));
        var schema = new TransformationSchema(source, targetSchema);
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
                        new FieldAssignment
                        {
                            NormalizedColumn = "order_no",
                            Value = new SourceExpression { Field = "raw_order_no" }
                        }
                    ]
                }
            ]
        };
        var module = new TransformationModule();

        var compilation = module.Compile(program, schema);
        var result = await module.EvaluateAsync(
            compilation.CompiledProgram!,
            new TransformationBatch(
            [
                new TransformationSourceRow(
                    "row-1",
                    new Dictionary<string, string?>
                    {
                        ["raw_order_no"] = "A-001"
                    },
                    new Dictionary<string, object?>(),
                    DateTimeOffset.UnixEpoch)
            ]));

        Assert.True(compilation.IsSuccess);
        Assert.Equal("A-001", result.Rows[0].Outputs[0].Values["order_no"]);
        Assert.DoesNotContain(
            typeof(TransformationSchema).GetProperties(),
            property => property.Name == "NormalizedTable");
    }

    [Fact]
    public void Compile_returns_an_opaque_program_through_the_public_facade()
    {
        const string json = """
            {
              "semanticsProfile": "sqlserver-legacy-v1",
              "outputs": [
                {
                  "outputKey": "pmx",
                  "fields": [
                    {
                      "normalizedColumn": "order_no",
                      "value": { "kind": "source", "field": "order_no" }
                    }
                  ]
                }
              ]
            }
            """;
        var program = JsonSerializer.Deserialize<TransformationProgram>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var schema = TransformationSchema.FromLegacyTable(
            new TableDefinition(
                "src_orders",
                TableKind.Source,
                [new ColumnDefinition("order_no", ColumnDataType.Text, true, 1)]),
            new TableDefinition(
                "norm_orders",
                TableKind.Normalized,
                [new ColumnDefinition("order_no", ColumnDataType.Text, true, 1)]));

        var result = new TransformationModule().Compile(program!, schema);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CompiledProgram);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(
            typeof(CompiledTransformationProgram).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(
            typeof(CompiledTransformationProgram).GetProperties(
                BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Compiler_implementation_is_not_exported_from_shared()
    {
        var exportedTypeNames = typeof(TransformationModule)
            .Assembly
            .GetExportedTypes()
            .Select(type => type.FullName)
            .ToArray();

        Assert.DoesNotContain(
            "MappingDemo.Shared.Transformation.TransformationCompiler",
            exportedTypeNames);
        Assert.DoesNotContain(
            "MappingDemo.Shared.Transformation.TransformationEvaluator",
            exportedTypeNames);
    }

    [Fact]
    public void Source_dependencies_are_exposed_only_through_the_facade()
    {
        var program = new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            SourceWhen = new NotBlankExpression
            {
                Value = new SourceExpression { Field = "gate" }
            },
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
                            Value = new SourceExpression { Field = "raw" }
                        }
                    ]
                }
            ]
        };
        var schema = TransformationSchema.FromLegacyTable(
            new TableDefinition(
                "src_orders",
                TableKind.Source,
                [
                    new ColumnDefinition("raw", ColumnDataType.Text, false, 1),
                    new ColumnDefinition("gate", ColumnDataType.Text, false, 2)
                ]),
            new TableDefinition(
                "norm_orders",
                TableKind.Normalized,
                [new ColumnDefinition("value", ColumnDataType.Text, true, 1)]));
        var module = new TransformationModule();
        var compilation = module.Compile(program, schema);

        var dependencies = module.GetSourceDependencies(
            compilation.CompiledProgram!);

        Assert.Equal(new[] { "gate", "raw" }, dependencies.Order());
    }
}
