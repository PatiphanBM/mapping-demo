using System.Text;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Api.Services;

public sealed class JsonFilePreviewModule(PayloadBindingModule payloadBindings)
{
    public async Task<JsonFilePreviewResponse> BuildAsync(
        JsonFilePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payloads = new List<string>();
        var diagnostics = new List<JsonFilePreviewDiagnosticResponse>();
        var orderedRecords = request.Preview.Records
            .Select((record, inputOrdinal) => new
            {
                Record = record,
                InputOrdinal = inputOrdinal
            })
            .OrderBy(item => item.Record.RowNumber ?? int.MaxValue)
            .ThenBy(item => item.InputOrdinal);

        foreach (var item in orderedRecords)
        {
            var record = item.Record;
            if (record.Transformation is null)
            {
                continue;
            }
            foreach (var error in record.Transformation.Errors)
            {
                diagnostics.Add(new JsonFilePreviewDiagnosticResponse(
                    record.RowKey,
                    error.OutputKey,
                    error.ExpressionPath,
                    "TransformationError",
                    error.Reason));
            }
            if (record.Transformation.Status == TransformationRowStatus.Invalid)
            {
                continue;
            }

            var recordPayloads = new List<string>();
            var recordHasBindingErrors = false;
            foreach (var output in record.Transformation.Outputs)
            {
                var binding = payloadBindings.Bind(
                    request.TargetSchema,
                    output.Values,
                    request.PayloadBindings,
                    new PayloadBindingContext(
                        request.Destination.Contract.Reference,
                        FileJobId: 0,
                        SourceRowId: item.InputOrdinal + 1,
                        SourceRowNumber: record.RowNumber
                            ?? item.InputOrdinal + 1,
                        ConfigVersionReference:
                            $"preview:{request.ConfigId}",
                        output.OutputKey,
                        FileName: request.SourceFileName));
                if (binding.Payload is not null)
                {
                    recordPayloads.Add(binding.Payload.Json);
                }
                recordHasBindingErrors |= !binding.IsSuccess;
                diagnostics.AddRange(binding.Diagnostics.Select(diagnostic =>
                    new JsonFilePreviewDiagnosticResponse(
                        record.RowKey,
                        output.OutputKey,
                        diagnostic.Path,
                        diagnostic.Code,
                        diagnostic.Message)));
            }
            if (!recordHasBindingErrors)
            {
                payloads.AddRange(recordPayloads);
            }
        }

        var artifact = await JsonArtifactSerializer.SerializeAsync(
            payloads,
            request.Destination.Formatting,
            cancellationToken);
        var relativeDisplayPath = JsonExportArtifactPath.Resolve(
            request.Destination,
            request.SourceFileName,
            fileJobId: 0,
            configVersion: 0,
            configVersionId: 0,
            artifactRevision: 1);
        return new JsonFilePreviewResponse(
            request.Destination.FileDestinationProfileId,
            request.Destination.Contract.Reference,
            request.Destination.Contract.SchemaHash,
            request.Destination.Formatting,
            relativeDisplayPath,
            Encoding.UTF8.GetString(artifact.Bytes),
            artifact.Metadata.ObjectCount,
            artifact.Metadata.ByteCount,
            diagnostics);
    }
}

public sealed record JsonFilePreviewRequest(
    JsonFileDestination Destination,
    TargetSchema TargetSchema,
    IReadOnlyList<PayloadBinding> PayloadBindings,
    PreviewMappingConfigVersionResponse Preview,
    long ConfigId,
    string SourceFileName);
