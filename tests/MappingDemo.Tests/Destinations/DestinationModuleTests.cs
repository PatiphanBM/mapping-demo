using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using MappingDemo.Worker.Destinations;
using Npgsql;

namespace MappingDemo.Tests.Destinations;

public sealed class DestinationModuleTests
{
    [Fact]
    public void Compile_returns_an_opaque_legacy_destination_through_the_module_interface()
    {
        IDestinationAdapter adapter = new LegacyTableDestinationAdapter();

        var result = adapter.Compile(
            new LegacyTableDestination(42),
            new DestinationCompilationContext(TargetSchema(), []));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CompiledDestination);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            typeof(LegacyTableDestination),
            adapter.DestinationType);
    }

    [Fact]
    public void Compile_rejects_api_contract_without_a_fake_legacy_delivery()
    {
        IDestinationAdapter adapter = new LegacyTableDestinationAdapter();
        var definition = new ApiContractDestination(
            "pa-proposals",
            new ContractReference(
                "proposal",
                "1",
                "proposal/1",
                "sha256:abc"));

        var result = adapter.Compile(
            definition,
            new DestinationCompilationContext(TargetSchema(), []));

        Assert.False(result.IsSuccess);
        Assert.Null(result.CompiledDestination);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("destination.kind", diagnostic.Path);
        Assert.Equal("UnsupportedDestinationKind", diagnostic.Code);
    }

    private static TargetSchema TargetSchema()
    {
        return new LegacyTableTargetSchemaAdapter().Adapt(
            new TableDefinition(
                "normalized_rows",
                TableKind.Normalized,
                [new ColumnDefinition("value", ColumnDataType.Integer, false, 1)]));
    }
}
