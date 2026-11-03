using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.Contracts;

public sealed class JsonFileContractParityTests
{
    public static TheoryData<ProfileCase> Profiles =>
        new()
        {
            new ProfileCase(
                "proposal/1",
                "proposal",
                "insured.fullName",
                "tags",
                "beneficiaries",
                "envelope.fileJobId"),
            new ProfileCase(
                "claims/2",
                "case",
                "claimant.name",
                "labels",
                "participants",
                "meta.batch")
        };

    [Theory]
    [MemberData(nameof(Profiles))]
    public async Task Api_and_json_file_build_equal_documents_from_same_contract(
        ProfileCase profileCase)
    {
        var binding = new PayloadBinding(
            profileCase.SystemPath,
            RuntimeValueSource.FileJobId);
        var apiProfile = ContractTestData.Profile(
            $"api-{profileCase.BusinessRoot}",
            profileCase.ContractReference) with
        {
            PayloadBindings = [binding]
        };
        var fileProfile = FileDestinationProfileCatalogTests.Profile(
            $"file-{profileCase.BusinessRoot}",
            profileCase.ContractReference) with
        {
            PayloadBindings = [binding]
        };
        var fetcher = new FixtureFetcher(
            profileCase.ContractReference,
            Schema(profileCase));
        var registry = new ContractRegistryModule(
            new ApiDestinationProfileCatalog([apiProfile]),
            fetcher,
            fileProfiles: new FileDestinationProfileCatalog([fileProfile]));
        var contractParts = profileCase.ContractReference.Split('/');
        var contract = new ContractReference(
            contractParts[0],
            contractParts[1],
            profileCase.ContractReference,
            string.Empty);

        var apiSnapshot = await registry.ResolveAsync(
            new ApiContractDestination(apiProfile.Id, contract),
            CancellationToken.None);
        var fileSnapshot = await registry.ResolveAsync(
            new JsonFileDestination(
                fileProfile.Id,
                contract,
                profileCase.BusinessRoot,
                "{sourceBaseName}.{fileJobId}.json",
                "compact"),
            CancellationToken.None);

        Assert.True(apiSnapshot.IsSuccess, Diagnostics(apiSnapshot.Diagnostics));
        Assert.True(fileSnapshot.IsSuccess, Diagnostics(fileSnapshot.Diagnostics));
        Assert.Equal(apiSnapshot.SchemaHash, fileSnapshot.SchemaHash);
        var apiDocument = await BuildDocumentAsync(apiSnapshot, profileCase);
        var fileDocument = await BuildDocumentAsync(fileSnapshot, profileCase);

        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(apiDocument),
            JsonNode.Parse(fileDocument)));
    }

    private static async Task<string> BuildDocumentAsync(
        ContractSnapshotResult snapshot,
        ProfileCase profileCase)
    {
        var source = new TableDefinition(
            "source_rows",
            TableKind.Source,
            [
                new ColumnDefinition("name", ColumnDataType.Text, false, 1),
                new ColumnDefinition("person_1", ColumnDataType.Text, false, 2),
                new ColumnDefinition("person_2", ColumnDataType.Text, false, 3)
            ]);
        var program = new TransformationProgram
        {
            LanguageVersion = 2,
            SemanticsProfile =
                TransformationCapabilities.SqlServerLegacyProfile,
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "main",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            TargetPath =
                                $"{profileCase.BusinessRoot}.{profileCase.NamePath}",
                            Value = new SourceExpression { Field = "name" }
                        },
                        new FieldAssignment
                        {
                            TargetPath =
                                $"{profileCase.BusinessRoot}.{profileCase.FixedArrayPath}[0]",
                            Value = new LiteralExpression
                            {
                                Type = ExpressionValueType.Text,
                                Value = Json("primary")
                            }
                        }
                    ],
                    ArrayGroups =
                    [
                        new ArrayGroup
                        {
                            TargetPath =
                                $"{profileCase.BusinessRoot}.{profileCase.GroupPath}",
                            SequencePath = "seq",
                            EmptyItemPolicy = ArrayEmptyItemPolicy
                                .SkipWhenAllBusinessFieldsEmpty,
                            SourceSlots =
                            [
                                Slot(1, "person_1"),
                                Slot(2, "person_2")
                            ]
                        }
                    ]
                }
            ]
        };
        var module = new TransformationModule();
        var compilation = module.Compile(
            program,
            new TransformationSchema(
                source,
                snapshot.TargetSchema!,
                snapshot.PayloadBindings
                    .Select(item => item.TargetPath)
                    .ToHashSet(StringComparer.Ordinal)));
        Assert.True(
            compilation.IsSuccess,
            Diagnostics(compilation.Diagnostics));
        var result = await module.EvaluateAsync(
            compilation.CompiledProgram!,
            new TransformationBatch(
            [
                new TransformationSourceRow(
                    "row-1",
                    new Dictionary<string, string?>
                    {
                        ["name"] = "Alice",
                        ["person_1"] = "-",
                        ["person_2"] = "Bob"
                    },
                    new Dictionary<string, object?>(),
                    DateTimeOffset.Parse("2026-10-21T00:00:00Z"))
            ]),
            CancellationToken.None);
        var mapped = Assert.Single(Assert.Single(result.Rows).Outputs).Values;
        var bound = new PayloadBindingModule().Bind(
            snapshot.TargetSchema!,
            mapped,
            snapshot.PayloadBindings,
            new PayloadBindingContext(
                profileCase.ContractReference,
                FileJobId: 42,
                SourceRowId: 101,
                SourceRowNumber: 7,
                ConfigVersionReference: "config-9-v3",
                OutputKey: "main"));

        Assert.True(bound.IsSuccess, Diagnostics(bound.Diagnostics));
        return bound.Payload!.Json;
    }

    private static ArraySourceSlot Slot(int number, string field) =>
        new()
        {
            SourceSlot = number,
            Fields =
            [
                new ArrayItemFieldAssignment
                {
                    TargetPath = "name",
                    Value = new SourceExpression { Field = field }
                }
            ]
        };

    private static JsonElement Json(string value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();

    private static string Schema(ProfileCase profileCase) =>
        """
        {
          "type":"object",
          "additionalProperties":false,
          "required":["BUSINESS_ROOT","SYSTEM_ROOT"],
          "properties":{
            "BUSINESS_ROOT":{
              "type":"object",
              "additionalProperties":false,
              "required":["NAME_ROOT","FIXED_ARRAY","GROUP_ARRAY"],
              "properties":{
                "NAME_ROOT":{"type":"object","additionalProperties":false,"required":["NAME_LEAF"],"properties":{"NAME_LEAF":{"type":"string"}}},
                "FIXED_ARRAY":{"type":"array","items":{"type":"string"}},
                "GROUP_ARRAY":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["seq","name"],"properties":{"seq":{"type":"integer"},"name":{"type":"string"}}}}
              }
            },
            "SYSTEM_ROOT":{"type":"object","additionalProperties":false,"required":["SYSTEM_LEAF"],"properties":{"SYSTEM_LEAF":{"type":"integer"}}}
          }
        }
        """
        .Replace("BUSINESS_ROOT", profileCase.BusinessRoot, StringComparison.Ordinal)
        .Replace("NAME_ROOT", FirstSegment(profileCase.NamePath), StringComparison.Ordinal)
        .Replace("NAME_LEAF", LastSegment(profileCase.NamePath), StringComparison.Ordinal)
        .Replace("FIXED_ARRAY", profileCase.FixedArrayPath, StringComparison.Ordinal)
        .Replace("GROUP_ARRAY", profileCase.GroupPath, StringComparison.Ordinal)
        .Replace("SYSTEM_ROOT", SystemRoot(profileCase), StringComparison.Ordinal)
        .Replace("SYSTEM_LEAF", SystemLeaf(profileCase), StringComparison.Ordinal);

    private static string FirstSegment(string path) => path.Split('.')[0];

    private static string LastSegment(string path) => path.Split('.')[1];

    private static string SystemRoot(ProfileCase profileCase) =>
        profileCase.SystemPath.Split('.')[0];

    private static string SystemLeaf(ProfileCase profileCase) =>
        profileCase.SystemPath.Split('.')[1];

    private static string Diagnostics(
        IEnumerable<ContractDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item =>
            $"{item.Path}: {item.Code}: {item.Message}"));

    private static string Diagnostics(
        IEnumerable<TransformationDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item =>
            $"{item.Path}: {item.Code}: {item.Message}"));

    public sealed record ProfileCase(
        string ContractReference,
        string BusinessRoot,
        string NamePath,
        string FixedArrayPath,
        string GroupPath,
        string SystemPath);

    private sealed class FixtureFetcher(
        string contractReference,
        string schema) : IContractSchemaFetcher
    {
        public ValueTask<ContractSchemaFetchResult> FetchAsync(
            IContractProfile profile,
            ContractReference contract,
            CancellationToken cancellationToken)
        {
            Assert.Equal(contractReference, contract.Reference);
            return ValueTask.FromResult(new ContractSchemaFetchResult(
                Encoding.UTF8.GetBytes(schema),
                "application/schema+json",
                DateTimeOffset.Parse("2026-10-21T00:00:00Z")));
        }
    }
}
