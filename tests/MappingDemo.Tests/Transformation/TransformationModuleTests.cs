using System.Reflection;
using System.Text.Json;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.Transformation;

public sealed class TransformationModuleTests
{
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
        var schema = new TransformationSchema(
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
        var schema = new TransformationSchema(
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
