using System.Text.Json;
using System.Text.Json.Nodes;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.Transformation;

public sealed class TransformationProgramSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static TheoryData<string> ValueExpressionCases => new()
    {
        """{ "kind": "metadata", "name": "fileName" }""",
        """
        {
          "kind": "coalesce",
          "values": [
            { "kind": "source", "field": "OptionalCode" },
            { "kind": "literal", "type": "text", "value": "fallback" }
          ]
        }
        """,
        """
        {
          "kind": "defaultIfBlank",
          "value": { "kind": "source", "field": "OptionalCode" },
          "fallback": { "kind": "literal", "type": "text", "value": "fallback" }
        }
        """,
        """
        {
          "kind": "substring",
          "value": { "kind": "source", "field": "ApplicationId" },
          "start": 1,
          "length": 3
        }
        """,
        """
        {
          "kind": "convert",
          "value": { "kind": "source", "field": "RawDate" },
          "type": "date",
          "inputFormat": "yyyyMMdd"
        }
        """,
        """{ "kind": "currentDate" }"""
    };

    public static TheoryData<string> BooleanExpressionCases => new()
    {
        BinaryComparison("ne"),
        BinaryComparison("gt"),
        BinaryComparison("lt"),
        BinaryComparison("lte"),
        UnaryValuePredicate("isNull"),
        UnaryValuePredicate("isBlank"),
        UnaryValuePredicate("notBlank"),
        """
        {
          "kind": "like",
          "value": { "kind": "source", "field": "Remark" },
          "pattern": { "kind": "literal", "type": "text", "value": "%โอนย้าย%" }
        }
        """,
        SetPredicate("in"),
        SetPredicate("notIn"),
        LogicalPredicate("and"),
        LogicalPredicate("or"),
        """
        {
          "kind": "not",
          "value": {
            "kind": "isNull",
            "value": { "kind": "source", "field": "OrderNo" }
          }
        }
        """,
        """
        {
          "kind": "exists",
          "lookup": "mtiOriginalPolicyExists@1",
          "arguments": {
            "loanNumber": { "kind": "source", "field": "LoanNumber" }
          }
        }
        """
    };

    public static TheoryData<string, string, string> InvalidPayloadCases =>
        new()
        {
            {
                """{ "outputs": [] }""",
                "$",
                "semanticsProfile"
            },
            {
                """
                {
                  "semanticsProfile": {},
                  "outputs": []
                }
                """,
                "$.semanticsProfile",
                "String"
            },
            {
                """
                {
                  "semanticsProfile": "sqlserver-legacy-v1",
                  "sourceWhen": {
                    "kind": "eq",
                    "field": "OrderNo",
                    "value": "A001"
                  },
                  "outputs": []
                }
                """,
                "$.sourceWhen.field",
                "field"
            },
            {
                ProgramWithValue(
                    """{ "kind": "literal", "value": "missing type" }"""),
                "$.outputs[0].fields[0].value",
                "type"
            },
            {
                ProgramWithValue("""{ "kind": "runSql" }"""),
                "$.outputs[0].fields[0].value",
                "runSql"
            }
        };

    [Fact]
    public void Direct_mapping_round_trips_with_explicit_discriminators()
    {
        const string json = """
            {
              "languageVersion": 1,
              "semanticsProfile": "sqlserver-legacy-v1",
              "outputs": [
                {
                  "outputKey": "pmx",
                  "fields": [
                    {
                      "normalizedColumn": "ORDER_NO",
                      "value": {
                        "kind": "source",
                        "field": "OrderNo"
                      }
                    }
                  ]
                }
              ]
            }
            """;

        var program = JsonSerializer.Deserialize<TransformationProgram>(
            json,
            JsonOptions);
        var serialized = JsonSerializer.Serialize(program, JsonOptions);

        Assert.NotNull(program);
        Assert.Equal("sqlserver-legacy-v1", program.SemanticsProfile);
        Assert.IsType<SourceExpression>(
            program.Outputs.Single().Fields.Single().Value);
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(json),
                JsonNode.Parse(serialized)));
    }

    [Fact]
    public void Workbook_examples_round_trip_as_one_declarative_program()
    {
        const string json = """
            {
              "languageVersion": 1,
              "semanticsProfile": "sqlserver-legacy-v1",
              "sourceWhen": {
                "kind": "eq",
                "left": { "kind": "source", "field": "RecordType" },
                "right": { "kind": "literal", "type": "text", "value": "D" }
              },
              "outputs": [
                {
                  "outputKey": "pmx",
                  "emitWhen": {
                    "kind": "notExists",
                    "lookup": "mtiOriginalPolicyExists@1",
                    "arguments": {
                      "systemId": { "kind": "literal", "type": "text", "value": "PMIB" },
                      "productName": { "kind": "literal", "type": "text", "value": "PMX" },
                      "loanNumber": { "kind": "source", "field": "LoanNumber" }
                    }
                  },
                  "fields": [
                    {
                      "normalizedColumn": "CWORKSHOP",
                      "value": {
                        "kind": "case",
                        "cases": [
                          {
                            "when": {
                              "kind": "eq",
                              "left": {
                                "kind": "trim",
                                "value": { "kind": "source", "field": "GarageType" }
                              },
                              "right": {
                                "kind": "literal",
                                "type": "text",
                                "value": "อู่ในเครือ"
                              }
                            },
                            "then": {
                              "kind": "literal",
                              "type": "text",
                              "value": "ซ่อมอู่"
                            }
                          }
                        ],
                        "else": { "kind": "literal", "type": "text", "value": "" }
                      }
                    },
                    {
                      "normalizedColumn": "CVEHCODE",
                      "value": {
                        "kind": "case",
                        "cases": [
                          {
                            "when": {
                              "kind": "gte",
                              "left": { "kind": "source", "field": "cTotalPremium" },
                              "right": { "kind": "literal", "type": "decimal", "value": 900 }
                            },
                            "then": { "kind": "literal", "type": "text", "value": "1.40A" }
                          },
                          {
                            "when": {
                              "kind": "eq",
                              "left": { "kind": "source", "field": "VehicleCode" },
                              "right": { "kind": "literal", "type": "text", "value": "310" }
                            },
                            "then": { "kind": "literal", "type": "text", "value": "320" }
                          }
                        ],
                        "else": { "kind": "source", "field": "VehicleCode" }
                      }
                    },
                    {
                      "normalizedColumn": "REGISTER_ID",
                      "value": {
                        "kind": "call",
                        "function": "formatRegNo@1",
                        "arguments": [
                          { "kind": "source", "field": "RegisterId" }
                        ]
                      }
                    },
                    {
                      "normalizedColumn": "EFFECTIVE_DATE",
                      "inputFormat": "yyyyMMdd",
                      "value": { "kind": "source", "field": "RawDate" }
                    }
                  ]
                },
                {
                  "outputKey": "ctp",
                  "emitWhen": {
                    "kind": "eq",
                    "left": { "kind": "source", "field": "Product" },
                    "right": { "kind": "literal", "type": "text", "value": "CTP" }
                  },
                  "fields": [
                    {
                      "normalizedColumn": "CLOANNUMBER",
                      "value": { "kind": "source", "field": "LoanNumber" }
                    }
                  ]
                }
              ]
            }
            """;

        var program = JsonSerializer.Deserialize<TransformationProgram>(
            json,
            JsonOptions);
        var serialized = JsonSerializer.Serialize(program, JsonOptions);

        Assert.NotNull(program);
        Assert.Equal(2, program.Outputs.Count);
        Assert.Equal(
            "yyyyMMdd",
            program.Outputs[0].Fields[3].InputFormat);
        Assert.Null(program.Outputs[0].Fields[0].InputFormat);
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(json),
                JsonNode.Parse(serialized)));
    }

    [Theory]
    [MemberData(nameof(ValueExpressionCases))]
    public void Every_value_expression_kind_round_trips(string expressionJson)
    {
        AssertProgramRoundTrips(ProgramWithValue(expressionJson));
    }

    [Theory]
    [MemberData(nameof(BooleanExpressionCases))]
    public void Every_boolean_expression_kind_round_trips(
        string expressionJson)
    {
        AssertProgramRoundTrips(ProgramWithSourceWhen(expressionJson));
    }

    [Theory]
    [MemberData(nameof(InvalidPayloadCases))]
    public void Invalid_payload_is_rejected_with_a_clear_path(
        string json,
        string expectedPath,
        string expectedMessageFragment)
    {
        var exception = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<TransformationProgram>(
                json,
                JsonOptions));

        Assert.Equal(expectedPath, exception.Path);
        Assert.Contains(
            expectedMessageFragment,
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertProgramRoundTrips(string json)
    {
        var program = JsonSerializer.Deserialize<TransformationProgram>(
            json,
            JsonOptions);
        var serialized = JsonSerializer.Serialize(program, JsonOptions);

        Assert.NotNull(program);
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(json),
                JsonNode.Parse(serialized)));
    }

    private static string ProgramWithValue(string expressionJson)
    {
        return $$"""
            {
              "semanticsProfile": "sqlserver-legacy-v1",
              "outputs": [
                {
                  "outputKey": "pmx",
                  "fields": [
                    {
                      "normalizedColumn": "RESULT",
                      "value": {{expressionJson}}
                    }
                  ]
                }
              ]
            }
            """;
    }

    private static string ProgramWithSourceWhen(string expressionJson)
    {
        return $$"""
            {
              "semanticsProfile": "sqlserver-legacy-v1",
              "sourceWhen": {{expressionJson}},
              "outputs": [
                {
                  "outputKey": "pmx",
                  "fields": [
                    {
                      "normalizedColumn": "RESULT",
                      "value": { "kind": "source", "field": "Value" }
                    }
                  ]
                }
              ]
            }
            """;
    }

    private static string BinaryComparison(string kind)
    {
        return $$"""
            {
              "kind": "{{kind}}",
              "left": { "kind": "source", "field": "Amount" },
              "right": { "kind": "literal", "type": "decimal", "value": 100 }
            }
            """;
    }

    private static string UnaryValuePredicate(string kind)
    {
        return $$"""
            {
              "kind": "{{kind}}",
              "value": { "kind": "source", "field": "OrderNo" }
            }
            """;
    }

    private static string SetPredicate(string kind)
    {
        return $$"""
            {
              "kind": "{{kind}}",
              "value": { "kind": "source", "field": "Product" },
              "values": [
                { "kind": "literal", "type": "text", "value": "PMX" },
                { "kind": "literal", "type": "text", "value": "CTP" }
              ]
            }
            """;
    }

    private static string LogicalPredicate(string kind)
    {
        return $$"""
            {
              "kind": "{{kind}}",
              "operands": [
                {
                  "kind": "isNull",
                  "value": { "kind": "source", "field": "DeletedAt" }
                },
                {
                  "kind": "notBlank",
                  "value": { "kind": "source", "field": "OrderNo" }
                }
              ]
            }
            """;
    }
}
