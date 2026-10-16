using System.Reflection;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Controllers;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Tests.Api;

public sealed class MappingDraftServiceTests
{
    [Fact]
    public async Task Validate_returns_exact_compiler_paths()
    {
        var program = ValidProgram() with
        {
            SemanticsProfile = "unknown",
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            NormalizedColumn = "effective_date",
                            InputFormat = "MM-dd-yyyy",
                            Value = new SourceExpression { Field = "raw" }
                        }
                    ]
                }
            ]
        };

        var profileResponse = await CreateService().ValidateAsync(
            ValidLayout(),
            program,
            Schema());

        Assert.False(profileResponse.IsValid);
        Assert.Contains(profileResponse.Diagnostics, diagnostic =>
            diagnostic.Path == "transformationProgram.semanticsProfile" &&
            diagnostic.Code == "UnknownSemanticsProfile");
        var inputFormatResponse = await CreateService().ValidateAsync(
            ValidLayout(),
            program with
            {
                SemanticsProfile =
                    TransformationCapabilities.SqlServerLegacyProfile
            },
            Schema());
        Assert.False(inputFormatResponse.IsValid);
        Assert.Contains(inputFormatResponse.Diagnostics, diagnostic =>
            diagnostic.Path ==
                "transformationProgram.outputs[0].fields[0].inputFormat" &&
            diagnostic.Code == "UnknownInputFormat");
        var layoutResponse = await CreateService().ValidateAsync(
            ValidLayout() with { Encoding = "windows-874" },
            ValidProgram(),
            Schema());
        Assert.False(layoutResponse.IsValid);
        Assert.Contains(layoutResponse.Diagnostics, diagnostic =>
            diagnostic.Path == "inputLayout.encoding" &&
            diagnostic.Code == "UnknownEncoding");
    }

    [Fact]
    public async Task Preview_rejects_evaluation_time_without_offset()
    {
        var result = await CreateService().PreviewAsync(
            Request(
                "2026-10-08T10:00:00",
                new SourceRowsPreviewSample { Rows = [] }),
            Schema());

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("evaluationAt", diagnostic.Path);
        Assert.Equal("InvalidEvaluationAt", diagnostic.Code);
    }

    [Theory]
    [InlineData("2026-10-08T16:59:59Z", 8)]
    [InlineData("2026-10-08T17:00:00Z", 9)]
    public async Task Preview_echoes_instant_and_uses_Asia_Bangkok_current_date(
        string instant,
        int expectedDay)
    {
        var request = Request(
            instant,
            new SourceRowsPreviewSample
            {
                Rows =
                [
                    new PreviewSourceRow
                    {
                        Values = new Dictionary<string, string?>
                        {
                            ["raw"] = "value"
                        }
                    }
                ]
            },
            CurrentDateProgram());

        var result = await CreateService().PreviewAsync(request, Schema());

        Assert.True(result.IsSuccess);
        Assert.Equal(DateTimeOffset.Parse(instant), result.Response!.EvaluationAt);
        var output = Assert.Single(
            Assert.Single(result.Response.Records).Transformation!.Outputs);
        Assert.Equal(
            new DateOnly(2026, 10, expectedDay),
            output.Values["effective_date"]);
    }

    [Fact]
    public async Task Preview_accepts_exactly_one_MiB_of_UTF8_text()
    {
        var content = "A" + new string('x', MappingDraftService.MaxSampleBytes - 1);
        var layout = SingleRecordLayout(content.Length);

        var result = await CreateService().PreviewAsync(
            Request(
                "2026-10-08T10:00:00Z",
                new TextFilePreviewSample
                {
                    FileName = "sample.txt",
                    Content = content
                },
                inputLayout: layout),
            Schema());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Response!.Records);
    }

    [Fact]
    public async Task Preview_rejects_text_over_one_MiB_without_truncating()
    {
        var content = new string('A', MappingDraftService.MaxSampleBytes + 1);

        var result = await CreateService().PreviewAsync(
            Request(
                "2026-10-08T10:00:00Z",
                new TextFilePreviewSample
                {
                    FileName = "sample.txt",
                    Content = content
                }),
            Schema());

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("sample.content", diagnostic.Path);
        Assert.Equal("SampleTooLarge", diagnostic.Code);
    }

    [Fact]
    public async Task Preview_accepts_the_real_23_record_TIB_sample()
    {
        var samplePath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "docs",
                "isoMTI-N7780.txt"));
        var content = await File.ReadAllTextAsync(samplePath);

        var result = await CreateService().PreviewAsync(
            Request(
                "2026-10-08T10:00:00Z",
                new TextFilePreviewSample
                {
                    FileName = "isoMTI-N7780.txt",
                    Content = content
                },
                inputLayout: TibLayout()),
            Schema());

        Assert.True(result.IsSuccess);
        Assert.Equal(23, result.Response!.Records.Count);
        Assert.Equal(
            21,
            result.Response.Records.Count(record =>
                record.Action == InputRecordAction.Import));
        Assert.Null(result.Response.Records[0].Transformation);
        Assert.NotNull(result.Response.Records[1].Transformation);
    }

    [Fact]
    public async Task Preview_rejects_invalid_UTF8_scalar_content()
    {
        var result = await CreateService().PreviewAsync(
            Request(
                "2026-10-08T10:00:00Z",
                new TextFilePreviewSample
                {
                    FileName = "sample.txt",
                    Content = "\ud800"
                }),
            Schema());

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("sample.content", diagnostic.Path);
        Assert.Equal("InvalidSampleEncoding", diagnostic.Code);
    }

    [Fact]
    public async Task Preview_rejects_the_51st_parsed_record_without_truncating()
    {
        var service = CreateService();
        var layout = SingleRecordLayout(1, maxOccurs: null);
        var fifty = await service.PreviewAsync(
            Request(
                "2026-10-08T10:00:00Z",
                new TextFilePreviewSample
                {
                    FileName = "sample.txt",
                    Content = string.Join('\n', Enumerable.Repeat("A", 50))
                },
                inputLayout: layout),
            Schema());
        var content = string.Join('\n', Enumerable.Repeat("A", 51));

        var result = await service.PreviewAsync(
            Request(
                "2026-10-08T10:00:00Z",
                new TextFilePreviewSample
                {
                    FileName = "sample.txt",
                    Content = content
                },
                inputLayout: layout),
            Schema());

        Assert.True(fifty.IsSuccess);
        Assert.Equal(50, fifty.Response!.Records.Count);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("sample.content", diagnostic.Path);
        Assert.Equal("TooManySampleRecords", diagnostic.Code);
    }

    [Fact]
    public async Task Preview_treats_file_name_as_metadata_and_returns_decoded_record()
    {
        var fileName = $"../missing-{Guid.NewGuid():N}.txt";
        var result = await CreateService().PreviewAsync(
            Request(
                "2026-10-08T10:00:00+00:00",
                new TextFilePreviewSample
                {
                    FileName = fileName,
                    Content = "hello"
                }),
            Schema());

        Assert.True(result.IsSuccess);
        var record = Assert.Single(result.Response!.Records);
        Assert.Equal("hello", record.RawRecord);
        Assert.Equal("hello", record.SourceValues["raw"]);
        Assert.Equal(
            "hello",
            Assert.Single(record.Transformation!.Outputs).Values["value"]);
    }

    [Fact]
    public async Task Preview_uses_synthetic_ids_but_honors_explicit_self_exclusion()
    {
        var request = Request(
            "2026-10-08T10:00:00Z",
            new SourceRowsPreviewSample
            {
                Rows =
                [
                    LookupRow(sourceRowId: null),
                    LookupRow(sourceRowId: 42)
                ]
            },
            LookupProgram());
        var service = new MappingDraftService(
            new InputRecordModule(),
            new TransformationModule(new MatchingLookupResolver()));

        var result = await service.PreviewAsync(request, LookupSchema());

        Assert.True(result.IsSuccess);
        Assert.Equal(
            TransformationRowStatus.Filtered,
            result.Response!.Records[0].Transformation!.Status);
        Assert.Equal(
            TransformationRowStatus.Done,
            result.Response.Records[1].Transformation!.Status);
    }

    [Fact]
    public void Preview_endpoint_has_three_MiB_body_limit()
    {
        var method = typeof(MappingConfigsController).GetMethod(
            nameof(MappingConfigsController.PreviewVersion));
        var limit = Assert.Single(
            method!.GetCustomAttributes<RequestSizeLimitAttribute>());

        Assert.Equal(
            MappingDraftService.MaxRequestBodyBytes,
            ((IRequestSizeLimitMetadata)limit).MaxRequestBodySize);
    }

    private static MappingDraftService CreateService()
    {
        return new MappingDraftService(
            new InputRecordModule(),
            new TransformationModule());
    }

    private static PreviewMappingConfigVersionRequest Request(
        string evaluationAt,
        MappingPreviewSample sample,
        TransformationProgram? program = null,
        InputLayoutDefinition? inputLayout = null)
    {
        return new PreviewMappingConfigVersionRequest
        {
            EvaluationAt = evaluationAt,
            Sample = sample,
            InputLayout = inputLayout ?? ValidLayout(),
            TransformationProgram = program ?? ValidProgram()
        };
    }

    private static DelimitedInputLayout ValidLayout()
    {
        return new DelimitedInputLayout(
            InputCapabilities.Utf8Encoding,
            ",",
            0,
            DelimitedHeaderPolicy.None,
            [new DelimitedInputField("raw", "raw")]);
    }

    private static FixedWidthInputLayout SingleRecordLayout(
        int expectedLength,
        int? maxOccurs = 1)
    {
        return new FixedWidthInputLayout(
            InputCapabilities.Utf8Encoding,
            InputCapabilities.Utf16CodeUnitPosition,
            [
                new FixedWidthRecordRule(
                    "detail",
                    new FixedWidthDiscriminator(1, 1, "A"),
                    InputRecordAction.Import,
                    expectedLength,
                    [new FixedWidthInputField("raw", 1, 1)])
            ],
            [new InputRecordSequenceItem("detail", 1, maxOccurs)],
            UnmatchedRecordPolicy.Error);
    }

    private static FixedWidthInputLayout TibLayout()
    {
        return new FixedWidthInputLayout(
            InputCapabilities.Utf8Encoding,
            InputCapabilities.Utf16CodeUnitPosition,
            [
                new FixedWidthRecordRule(
                    "header",
                    new FixedWidthDiscriminator(1, 1, "H"),
                    InputRecordAction.Ignore,
                    1500,
                    []),
                new FixedWidthRecordRule(
                    "detail",
                    new FixedWidthDiscriminator(1, 1, "D"),
                    InputRecordAction.Import,
                    3761,
                    [new FixedWidthInputField("raw", 1, 1)]),
                new FixedWidthRecordRule(
                    "trailer",
                    new FixedWidthDiscriminator(1, 1, "T"),
                    InputRecordAction.Ignore,
                    1500,
                    [])
            ],
            [
                new InputRecordSequenceItem("header", 1, 1),
                new InputRecordSequenceItem("detail", 21, 21),
                new InputRecordSequenceItem("trailer", 1, 1)
            ],
            UnmatchedRecordPolicy.Error);
    }

    private static TransformationProgram ValidProgram()
    {
        return new TransformationProgram
        {
            LanguageVersion = TransformationCapabilities.LanguageVersion,
            SemanticsProfile =
                TransformationCapabilities.SqlServerLegacyProfile,
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            NormalizedColumn = "value",
                            Value = new SourceExpression { Field = "raw" }
                        }
                    ]
                }
            ]
        };
    }

    private static TransformationProgram CurrentDateProgram()
    {
        return new TransformationProgram
        {
            LanguageVersion = TransformationCapabilities.LanguageVersion,
            SemanticsProfile =
                TransformationCapabilities.SqlServerLegacyProfile,
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            NormalizedColumn = "effective_date",
                            Value = new CurrentDateExpression()
                        }
                    ]
                }
            ]
        };
    }

    private static TransformationProgram LookupProgram()
    {
        return new TransformationProgram
        {
            LanguageVersion = TransformationCapabilities.LanguageVersion,
            SemanticsProfile =
                TransformationCapabilities.SqlServerLegacyProfile,
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    EmitWhen = new NotExistsExpression
                    {
                        Lookup =
                            TransformationCapabilities.MtiOriginalPolicyExistsV1,
                        Arguments = new Dictionary<string, ValueExpression>
                        {
                            ["systemId"] = new SourceExpression
                            {
                                Field = "system_id"
                            },
                            ["productName"] = new SourceExpression
                            {
                                Field = "product_name"
                            },
                            ["loanNumber"] = new SourceExpression
                            {
                                Field = "loan_number"
                            }
                        }
                    },
                    Fields =
                    [
                        new FieldAssignment
                        {
                            NormalizedColumn = "value",
                            Value = new SourceExpression { Field = "raw" }
                        }
                    ]
                }
            ]
        };
    }

    private static PreviewSourceRow LookupRow(long? sourceRowId)
    {
        return new PreviewSourceRow
        {
            SourceRowId = sourceRowId,
            Values = new Dictionary<string, string?>
            {
                ["raw"] = "value",
                ["system_id"] = "SYS",
                ["product_name"] = "PRODUCT",
                ["loan_number"] = "LOAN"
            }
        };
    }

    private static TransformationSchema Schema()
    {
        return new TransformationSchema(
            new TableDefinition(
                "src_preview",
                TableKind.Source,
                [new ColumnDefinition("raw", ColumnDataType.Text, false, 1)]),
            new TableDefinition(
                "norm_preview",
                TableKind.Normalized,
                [
                    new ColumnDefinition(
                        "value",
                        ColumnDataType.Text,
                        false,
                        1),
                    new ColumnDefinition(
                        "effective_date",
                        ColumnDataType.Date,
                        false,
                        2)
                ]));
    }

    private static TransformationSchema LookupSchema()
    {
        return new TransformationSchema(
            new TableDefinition(
                "src_preview",
                TableKind.Source,
                [
                    new ColumnDefinition("raw", ColumnDataType.Text, false, 1),
                    new ColumnDefinition(
                        "system_id",
                        ColumnDataType.Text,
                        false,
                        2),
                    new ColumnDefinition(
                        "product_name",
                        ColumnDataType.Text,
                        false,
                        3),
                    new ColumnDefinition(
                        "loan_number",
                        ColumnDataType.Text,
                        false,
                        4)
                ]),
            new TableDefinition(
                "norm_preview",
                TableKind.Normalized,
                [new ColumnDefinition("value", ColumnDataType.Text, false, 1)]));
    }

    private sealed class MatchingLookupResolver : ITransformationLookupResolver
    {
        public ValueTask<TransformationLookupValidationResult> ValidateAsync(
            string capability,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                new TransformationLookupValidationResult(true, null));
        }

        public ValueTask<IReadOnlyList<TransformationLookupMatch>> ResolveAsync(
            IReadOnlyList<TransformationLookupRequest> requests,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TransformationLookupMatch> matches = requests
                .Select(request => new TransformationLookupMatch(request, 42))
                .ToArray();
            return ValueTask.FromResult(matches);
        }
    }
}
