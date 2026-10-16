using System.Text.Json;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.Transformation;

public sealed class TransformationLookupTests
{
    [Theory]
    [InlineData(null, "PMX", TransformationRowStatus.Done)]
    [InlineData(1L, "PMX", TransformationRowStatus.Done)]
    [InlineData(2L, "PMX", TransformationRowStatus.Filtered)]
    [InlineData(2L, "CTP", TransformationRowStatus.Done)]
    public async Task Anti_lookup_excludes_only_the_current_source_row(
        long? matchingSourceRowId,
        string matchingProduct,
        TransformationRowStatus expectedStatus)
    {
        var resolver = new InMemoryLookupResolver();
        if (matchingSourceRowId.HasValue)
        {
            resolver.Matches.Add(new TransformationLookupMatch(
                Request(productName: matchingProduct),
                matchingSourceRowId.Value));
        }

        var result = await EvaluateAsync(resolver, [Row("TR-L01-L04", 1)]);

        Assert.Equal(expectedStatus, Assert.Single(result.Rows).Status);
    }

    [Fact]
    public async Task Lookup_keys_are_deduplicated_and_resolved_once_per_batch()
    {
        var resolver = new InMemoryLookupResolver();

        var result = await EvaluateAsync(
            resolver,
            [Row("first", 1), Row("second", 2)]);

        Assert.All(
            result.Rows,
            row => Assert.Equal(TransformationRowStatus.Done, row.Status));
        Assert.Equal(1, resolver.ResolveCalls);
        Assert.Equal(Request(), Assert.Single(resolver.LastRequests));
    }

    [Fact]
    public async Task Lookup_timeout_propagates_as_a_retryable_system_error()
    {
        var resolver = new InMemoryLookupResolver
        {
            ResolveException = new TimeoutException("lookup timed out")
        };

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
            await EvaluateAsync(resolver, [Row("timeout", 1)]));

        Assert.Equal("lookup timed out", exception.Message);
    }

    [Fact]
    public async Task Dependency_validation_reports_the_lookup_expression_path()
    {
        var resolver = new InMemoryLookupResolver
        {
            Validation = new TransformationLookupValidationResult(
                false,
                "Required PMIB index was not found.")
        };
        var module = new TransformationModule(resolver);
        var compilation = module.Compile(
            Program(),
            new TransformationSchema(SourceTable(), TargetTable()));

        var diagnostic = Assert.Single(await module.ValidateDependenciesAsync(
            compilation.CompiledProgram!));

        Assert.Equal("outputs[0].emitWhen.lookup", diagnostic.Path);
        Assert.Equal("LookupDependencyUnavailable", diagnostic.Code);
        Assert.Equal("Required PMIB index was not found.", diagnostic.Message);
    }

    private static async Task<TransformationBatchResult> EvaluateAsync(
        ITransformationLookupResolver resolver,
        IReadOnlyList<TransformationSourceRow> rows)
    {
        var module = new TransformationModule(resolver);
        var compilation = module.Compile(
            Program(),
            new TransformationSchema(SourceTable(), TargetTable()));
        Assert.True(compilation.IsSuccess);

        return await module.EvaluateAsync(
            compilation.CompiledProgram!,
            new TransformationBatch(rows));
    }

    private static TransformationProgram Program()
    {
        return new TransformationProgram
        {
            SemanticsProfile = TransformationCapabilities.SqlServerLegacyProfile,
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    EmitWhen = new NotExistsExpression
                    {
                        Lookup = TransformationCapabilities.MtiOriginalPolicyExistsV1,
                        Arguments = new Dictionary<string, ValueExpression>
                        {
                            ["systemId"] = new SourceExpression { Field = "SYSTEM_ID" },
                            ["productName"] = new SourceExpression { Field = "CPRODUCTNAME" },
                            ["loanNumber"] = new SourceExpression { Field = "CLOANNUMBER" }
                        }
                    },
                    Fields =
                    [
                        new FieldAssignment
                        {
                            NormalizedColumn = "VALUE",
                            Value = Text("accepted")
                        }
                    ]
                }
            ]
        };
    }

    private static TransformationSourceRow Row(string key, long sourceRowId)
    {
        return new TransformationSourceRow(
            key,
            new Dictionary<string, string?>
            {
                ["SYSTEM_ID"] = "SYS",
                ["CPRODUCTNAME"] = "PMX",
                ["CLOANNUMBER"] = "LN-001"
            },
            new Dictionary<string, object?>(),
            DateTimeOffset.UnixEpoch,
            sourceRowId);
    }

    private static TransformationLookupRequest Request(string productName = "PMX")
    {
        return new TransformationLookupRequest(
            TransformationCapabilities.MtiOriginalPolicyExistsV1,
            "SYS",
            productName,
            "LN-001");
    }

    private static TableDefinition SourceTable()
    {
        return new TableDefinition(
            "source_pmib",
            TableKind.Source,
            new[] { "SYSTEM_ID", "CPRODUCTNAME", "CLOANNUMBER" }
                .Select((name, index) => new ColumnDefinition(
                    name,
                    ColumnDataType.Text,
                    false,
                    index + 1))
                .ToArray());
    }

    private static TableDefinition TargetTable()
    {
        return new TableDefinition(
            "mti_original",
            TableKind.Normalized,
            [new ColumnDefinition("VALUE", ColumnDataType.Text, true, 1)]);
    }

    private static LiteralExpression Text(string value)
    {
        return new LiteralExpression
        {
            Type = ExpressionValueType.Text,
            Value = JsonSerializer.SerializeToElement(value)
        };
    }

    private sealed class InMemoryLookupResolver : ITransformationLookupResolver
    {
        public List<TransformationLookupMatch> Matches { get; } = [];

        public Exception? ResolveException { get; init; }

        public TransformationLookupValidationResult Validation { get; init; } =
            new(true, null);

        public int ResolveCalls { get; private set; }

        public IReadOnlyList<TransformationLookupRequest> LastRequests { get; private set; } =
            [];

        public ValueTask<TransformationLookupValidationResult> ValidateAsync(
            string capability,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Validation);
        }

        public ValueTask<IReadOnlyList<TransformationLookupMatch>> ResolveAsync(
            IReadOnlyList<TransformationLookupRequest> requests,
            CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            LastRequests = requests;
            if (ResolveException is not null)
            {
                throw ResolveException;
            }

            return ValueTask.FromResult<IReadOnlyList<TransformationLookupMatch>>(
                Matches);
        }
    }
}
