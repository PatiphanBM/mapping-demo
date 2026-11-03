using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.Transformation;

public sealed class TransformationV2Tests
{
    [Fact]
    public async Task V2_assembles_nested_objects_fixed_indices_and_array_groups()
    {
        var schema = ContractSchema();
        var source = new TableDefinition(
            "source_rows",
            TableKind.Source,
            [
                new ColumnDefinition("name", ColumnDataType.Text, false, 1),
                new ColumnDefinition("beneficiary_1", ColumnDataType.Text, false, 2),
                new ColumnDefinition("beneficiary_2", ColumnDataType.Text, false, 3),
                new ColumnDefinition("flag_2", ColumnDataType.Text, false, 4)
            ]);
        var program = new TransformationProgram
        {
            LanguageVersion = 2,
            SemanticsProfile = TransformationCapabilities.SqlServerLegacyProfile,
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "main",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            TargetPath = "proposal.insured.fullName",
                            Value = new SourceExpression { Field = "name" }
                        },
                        new FieldAssignment
                        {
                            TargetPath = "proposal.tags[0]",
                            Value = new LiteralExpression
                            {
                                Type = ExpressionValueType.Text,
                                Value = Json("\"primary\"")
                            }
                        }
                    ],
                    ArrayGroups =
                    [
                        new ArrayGroup
                        {
                            TargetPath = "proposal.beneficiaries",
                            SequencePath = "seq",
                            EmptyItemPolicy = ArrayEmptyItemPolicy.SkipWhenAllBusinessFieldsEmpty,
                            SourceSlots =
                            [
                                Slot(1, "beneficiary_1"),
                                Slot(2, "beneficiary_2", "flag_2")
                            ]
                        }
                    ]
                }
            ]
        };
        var module = new TransformationModule();
        var compilation = module.Compile(program, new TransformationSchema(source, schema));

        Assert.True(compilation.IsSuccess, Diagnostics(compilation.Diagnostics));
        var result = await module.EvaluateAsync(
            compilation.CompiledProgram!,
            new TransformationBatch([
                new TransformationSourceRow(
                    "row-1",
                    new Dictionary<string, string?>
                    {
                        ["name"] = " Alice ",
                        ["beneficiary_1"] = " - ",
                        ["beneficiary_2"] = " Bob ",
                        ["flag_2"] = "false"
                    },
                    new Dictionary<string, object?>(),
                    DateTimeOffset.Parse("2026-10-20T00:00:00Z"))
            ]),
            CancellationToken.None);

        var output = Assert.Single(Assert.Single(result.Rows).Outputs);
        var proposal = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            output.Values["proposal"]);
        var insured = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            proposal["insured"]);
        Assert.Equal("Alice", insured["fullName"]);
        var tags = Assert.IsAssignableFrom<IReadOnlyList<object?>>(proposal["tags"]);
        Assert.Equal("primary", tags[0]);
        var beneficiaries = Assert.IsAssignableFrom<IReadOnlyList<object?>>(
            proposal["beneficiaries"]);
        var beneficiary = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            Assert.Single(beneficiaries));
        Assert.Equal(2, beneficiary["seq"]);
        Assert.Equal("Bob", beneficiary["name"]);
        Assert.Equal(false, beneficiary["active"]);
    }

    [Theory]
    [InlineData("proposal.unknown", "UnknownTargetPath")]
    [InlineData("proposal.insured", "TargetContainerNotAssignable")]
    public void V2_rejects_unknown_and_container_paths(string targetPath, string code)
    {
        var program = new TransformationProgram
        {
            LanguageVersion = 2,
            SemanticsProfile = TransformationCapabilities.SqlServerLegacyProfile,
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "main",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            TargetPath = targetPath,
                            Value = new LiteralExpression
                            {
                                Type = ExpressionValueType.Text,
                                Value = Json("\"x\"")
                            }
                        }
                    ]
                }
            ]
        };
        var source = new TableDefinition("source", TableKind.Source, []);

        var result = new TransformationModule().Compile(
            program,
            new TransformationSchema(source, ContractSchema()));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void V2_rejects_fixed_array_index_gaps()
    {
        var program = new TransformationProgram
        {
            LanguageVersion = 2,
            SemanticsProfile = TransformationCapabilities.SqlServerLegacyProfile,
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "main",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            TargetPath = "proposal.insured.fullName",
                            Value = Text("Alice")
                        },
                        new FieldAssignment
                        {
                            TargetPath = "proposal.tags[1]",
                            Value = Text("gap")
                        }
                    ],
                    ArrayGroups =
                    [
                        new ArrayGroup
                        {
                            TargetPath = "proposal.beneficiaries",
                            SequencePath = "seq",
                            EmptyItemPolicy = ArrayEmptyItemPolicy.SkipWhenAllBusinessFieldsEmpty,
                            SourceSlots = []
                        }
                    ]
                }
            ]
        };

        var result = new TransformationModule().Compile(
            program,
            new TransformationSchema(
                new TableDefinition("source", TableKind.Source, []),
                ContractSchema()));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == "ArrayIndexGap");
    }

    private static ArraySourceSlot Slot(
        int number,
        string nameField,
        string? flagField = null) =>
        new()
        {
            SourceSlot = number,
            Fields = new List<ArrayItemFieldAssignment>
            {
                new ArrayItemFieldAssignment
                {
                    TargetPath = "name",
                    Value = new SourceExpression { Field = nameField }
                }
            }.Concat(flagField is null
                ? []
                : [new ArrayItemFieldAssignment
                {
                    TargetPath = "active",
                    Value = new ConvertExpression
                    {
                        Type = ExpressionValueType.Boolean,
                        Value = new SourceExpression { Field = flagField }
                    }
                }]).ToArray()
        };

    private static TargetSchema ContractSchema()
    {
        const string schema = """
            {
              "type":"object","additionalProperties":false,"required":["proposal"],
              "properties":{"proposal":{"type":"object","additionalProperties":false,"required":["insured","beneficiaries"],"properties":{
                "insured":{"type":"object","additionalProperties":false,"required":["fullName"],"properties":{"fullName":{"type":"string"}}},
                "tags":{"type":"array","items":{"type":"string"}},
                "beneficiaries":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["seq","name"],"properties":{"seq":{"type":"integer"},"name":{"type":"string"},"active":{"type":"boolean"}}}}
              }}}
            }
            """;
        return new ContractSchemaModule().Compile(schema).TargetSchema!;
    }

    private static System.Text.Json.JsonElement Json(string value) =>
        System.Text.Json.JsonDocument.Parse(value).RootElement.Clone();

    private static LiteralExpression Text(string value) =>
        new()
        {
            Type = ExpressionValueType.Text,
            Value = Json(System.Text.Json.JsonSerializer.Serialize(value))
        };

    private static string Diagnostics(IReadOnlyList<TransformationDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item =>
            $"{item.Path}: {item.Code}: {item.Message}"));
}
