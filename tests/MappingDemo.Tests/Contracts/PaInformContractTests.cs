using System.Text.Json;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Tests.Contracts;

public sealed class PaInformContractTests
{
    [Fact]
    public void Contract_compiles_and_binds_processing_metadata()
    {
        var schemaJson = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            "pa-inform-v1.json"));
        var schema = new ContractSchemaModule().Compile(schemaJson);

        Assert.True(schema.IsSuccess, Diagnostics(schema.Diagnostics));

        var mapped = new Dictionary<string, object?>
        {
            ["inform"] = new Dictionary<string, object?>
            {
                ["partnerCode"] = "MTLS",
                ["saleChannel"] = "FILE",
                ["sourceReference"] = "PA0000000001",
                ["transactionType"] = "NEW",
                ["sourceTransactionType"] = "P",
                ["effectiveDate"] = new DateOnly(2026, 10, 9),
                ["expiryDate"] = new DateOnly(2027, 10, 8),
                ["coverStartTime"] = new TimeOnly(10, 30),
                ["campaignCode"] = "MTLSPA",
                ["packageCode"] = "MTL2",
                ["agentCode"] = "74000490",
                ["saleId"] = null,
                ["totalPremium"] = 180m,
                ["salesBranchCode"] = "0001",
                ["salesBranchName"] = "สาขาตัวอย่าง",
                ["insured"] = new Dictionary<string, object?>
                {
                    ["isPolicyholder"] = true,
                    ["relation"] = "SELF",
                    ["fullName"] = "นางสาวสมหญิง ใจดี",
                    ["idNo"] = "1234567890123",
                    ["birthDate"] = new DateOnly(1991, 5, 20),
                    ["declaredAge"] = 35,
                    ["occupation"] = "พนักงานบริษัท",
                    ["address"] = "99 ต.ในเมือง อ.เมือง จ.ขอนแก่น 40000"
                },
                ["beneficiaries"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["seq"] = 1,
                        ["fullName"] = "สมชาย ใจดี",
                        ["relationship"] = "บิดา"
                    }
                }
            }
        };
        var bindings = new[]
        {
            new PayloadBinding("contractVersion", RuntimeValueSource.ContractReference),
            new PayloadBinding("fileJobId", RuntimeValueSource.FileJobId),
            new PayloadBinding("fileName", RuntimeValueSource.FileName),
            new PayloadBinding("sourceRowId", RuntimeValueSource.SourceRowId),
            new PayloadBinding("outputKey", RuntimeValueSource.OutputKey),
            new PayloadBinding("sourceRowNo", RuntimeValueSource.SourceRowNumber),
            new PayloadBinding(
                "mappingConfigVersionId",
                RuntimeValueSource.ConfigVersionReference)
        };

        var result = new PayloadBindingModule().Bind(
            schema.TargetSchema!,
            mapped,
            bindings,
            new PayloadBindingContext(
                "pa-inform/1",
                FileJobId: 42,
                SourceRowId: 101,
                SourceRowNumber: 7,
                ConfigVersionReference: "3",
                OutputKey: "default",
                FileName: "MTLSPA_20261021.xls"));

        Assert.True(result.IsSuccess, Diagnostics(result.Diagnostics));
        using var payload = JsonDocument.Parse(result.Payload!.Json);
        var root = payload.RootElement;
        Assert.Equal("pa-inform/1", root.GetProperty("contractVersion").GetString());
        Assert.Equal("42", root.GetProperty("fileJobId").GetString());
        Assert.Equal(
            "MTLSPA_20261021.xls",
            root.GetProperty("fileName").GetString());
        Assert.Equal("101", root.GetProperty("sourceRowId").GetString());
        Assert.Equal("default", root.GetProperty("outputKey").GetString());
        Assert.Equal(7, root.GetProperty("sourceRowNo").GetInt32());
        Assert.Equal("3", root.GetProperty("mappingConfigVersionId").GetString());
        Assert.Empty(root.GetProperty("inform").GetProperty("extras").EnumerateObject());
    }

    private static string Diagnostics(
        IEnumerable<ContractDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item =>
            $"{item.Path}: {item.Code}: {item.Message}"));
}
