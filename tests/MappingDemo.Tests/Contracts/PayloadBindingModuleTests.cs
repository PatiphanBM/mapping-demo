using MappingDemo.Shared.Contracts;

namespace MappingDemo.Tests.Contracts;

public sealed class PayloadBindingModuleTests
{
    [Theory]
    [InlineData("envelope.fileJobId", "payload.content.outputKey")]
    [InlineData("meta.batch", "case.result.key")]
    public void Bind_supports_two_profiles_without_hard_coded_envelope_names(
        string fileJobPath,
        string outputPath)
    {
        var schema = new ContractSchemaModule().Compile("""
            {
              "type":"object","additionalProperties":false,
              "properties":{
                "envelope":{"type":"object","additionalProperties":false,"properties":{"fileJobId":{"type":"integer"}}},
                "payload":{"type":"object","additionalProperties":false,"properties":{"content":{"type":"object","additionalProperties":false,"properties":{"outputKey":{"type":"string"}}}}},
                "meta":{"type":"object","additionalProperties":false,"properties":{"batch":{"type":"integer"}}},
                "case":{"type":"object","additionalProperties":false,"properties":{"result":{"type":"object","additionalProperties":false,"properties":{"key":{"type":"string"}}}}}
              }
            }
            """).TargetSchema!;
        var bindings = new[]
        {
            new PayloadBinding(fileJobPath, RuntimeValueSource.FileJobId),
            new PayloadBinding(outputPath, RuntimeValueSource.OutputKey)
        };
        var context = new PayloadBindingContext(
            "claims/2",
            42,
            101,
            7,
            "config-9-v3",
            "main");

        var result = new PayloadBindingModule().Bind(
            schema,
            new Dictionary<string, object?>(),
            bindings,
            context);

        Assert.True(result.IsSuccess, Diagnostics(result.Diagnostics));
        Assert.StartsWith("sha256:", result.Payload!.Hash);
        Assert.Contains("42", result.Payload.Json);
        Assert.Contains("main", result.Payload.Json);
    }

    [Fact]
    public void Bind_rejects_system_mapped_conflict_with_exact_path()
    {
        var schema = new ContractSchemaModule().Compile("""
            {"type":"object","additionalProperties":false,"properties":{"meta":{"type":"object","additionalProperties":false,"properties":{"fileId":{"type":"integer"}}}}}
            """).TargetSchema!;
        var mapped = new Dictionary<string, object?>
        {
            ["meta"] = new Dictionary<string, object?> { ["fileId"] = 9 }
        };

        var result = new PayloadBindingModule().Bind(
            schema,
            mapped,
            [new PayloadBinding("meta.fileId", RuntimeValueSource.FileJobId)],
            new PayloadBindingContext("x", 42, 1, 1, "v1", "main"));

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("meta.fileId", diagnostic.Path);
        Assert.Equal("SystemMappedPathConflict", diagnostic.Code);
    }

    [Fact]
    public void Bind_runs_final_contract_constraints()
    {
        var schema = new ContractSchemaModule().Compile("""
            {"type":"object","additionalProperties":false,"required":["code"],"properties":{"code":{"type":"string","maxLength":3,"pattern":"^[A-Z]+$"}}}
            """).TargetSchema!;

        var result = new PayloadBindingModule().Bind(
            schema,
            new Dictionary<string, object?> { ["code"] = "abcd" },
            [],
            new PayloadBindingContext("x", 1, 1, 1, "v1", "main"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Code == "StringLengthExceeded");
        Assert.Contains(result.Diagnostics, item => item.Code == "PatternMismatch");
    }

    private static string Diagnostics(IReadOnlyList<ContractDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item =>
            $"{item.Path}: {item.Code}: {item.Message}"));
}
