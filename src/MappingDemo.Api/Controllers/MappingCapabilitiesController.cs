using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Transformation;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("mapping-capabilities")]
public sealed class MappingCapabilitiesController : ControllerBase
{
    private const string SqlServerLegacySummary =
        "Immutable SQL Server legacy subset: three-valued boolean logic, " +
        "ordinal case-insensitive text comparison, invariant conversions, " +
        "and currentDate in Asia/Bangkok.";

    [HttpGet]
    public ActionResult<MappingCapabilitiesResponse> Get()
    {
        return Ok(new MappingCapabilitiesResponse(
            new InputCapabilitiesResponse(
                InputCapabilities.Formats,
                InputCapabilities.Encodings,
                InputCapabilities.PositionUnits),
            new TransformationCapabilitiesResponse(
                [TransformationCapabilities.LanguageVersion],
                [
                    new SemanticsProfileCapabilityResponse(
                        TransformationCapabilities.SqlServerLegacyProfile,
                        Immutable: true,
                        SqlServerLegacySummary)
                ],
                TransformationCapabilities.TargetConversionTypes,
                new InputFormatCapabilitiesResponse(
                    TransformationCapabilities.DateInputFormats,
                    TransformationCapabilities.DateTimeInputFormats),
                TransformationCapabilities.ValueExpressions,
                TransformationCapabilities.BooleanOperators,
                TransformationCapabilities.Functions,
                TransformationCapabilities.Lookups,
                TransformationCapabilities.MetadataFields,
                new TransformationLimitsResponse(
                    TransformationCapabilities.MaxOutputs,
                    TransformationCapabilities.MaxFieldsPerOutput,
                    TransformationCapabilities.MaxCollectionItems,
                    TransformationCapabilities.MaxExpressionDepth,
                    TransformationCapabilities.MaxExpressionNodes))));
    }
}
