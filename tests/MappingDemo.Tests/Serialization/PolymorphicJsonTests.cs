using System.Text.Json;
using MappingDemo.Shared.Serialization;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.Serialization;

public sealed class PolymorphicJsonTests
{
    [Fact]
    public void Deserialize_WhenJsonbMovesCaseDiscriminator_ReadsNestedCase()
    {
        const string json = """
            {
              "outputs": [{
                "fields": [{
                  "value": {
                    "else": { "kind": "literal", "type": "text", "value": "other" },
                    "kind": "case",
                    "cases": [{
                      "then": { "kind": "literal", "type": "text", "value": "match" },
                      "when": {
                        "kind": "eq",
                        "left": { "kind": "source", "field": "code" },
                        "right": { "kind": "literal", "type": "text", "value": "A" }
                      }
                    }]
                  },
                  "normalizedColumn": "result"
                }],
                "outputKey": "main"
              }],
              "languageVersion": 1,
              "semanticsProfile": "sqlserver-legacy-v1"
            }
            """;

        var program = PolymorphicJson.Deserialize<TransformationProgram>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var expression = Assert.IsType<CaseExpression>(
            Assert.Single(Assert.Single(program!.Outputs).Fields).Value);
        Assert.Single(expression.Cases);
        Assert.IsType<LiteralExpression>(expression.Else);
    }
}
