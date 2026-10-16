using System.Text.Json;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.Transformation;

public sealed class TransformationEvaluatorTests
{
    [Fact]
    public async Task Evaluate_applies_golden_assignment_semantics()
    {
        var source = SourceTable(
            "Code",
            "Empty",
            "Missing",
            "Blank",
            "Amount",
            "RawDate",
            "Enabled");
        var target = TargetTable(
            Column("DIRECT_TEXT", ColumnDataType.Text),
            Column("EMPTY_DEFAULT", ColumnDataType.Text),
            Column("NULL_COALESCE", ColumnDataType.Text),
            Column("OPTIONAL_BLANK", ColumnDataType.Text),
            Column("AMOUNT", ColumnDataType.Decimal),
            Column("EFFECTIVE_DATE", ColumnDataType.Date),
            Column("ENABLED", ColumnDataType.Boolean),
            Column("EXPLICIT_TRIM", ColumnDataType.Text));
        var program = Program(
            Assignment("DIRECT_TEXT", Source("Code")),
            Assignment(
                "EMPTY_DEFAULT",
                new DefaultIfBlankExpression
                {
                    Value = Source("Empty"),
                    Fallback = Text("DEFAULT")
                }),
            Assignment(
                "NULL_COALESCE",
                new CoalesceExpression
                {
                    Values = [Source("Missing"), Text("MISSING")]
                }),
            Assignment("OPTIONAL_BLANK", Source("Blank")),
            Assignment("AMOUNT", Source("Amount")),
            Assignment(
                "EFFECTIVE_DATE",
                Source("RawDate"),
                "yyyyMMdd"),
            Assignment("ENABLED", Source("Enabled")),
            Assignment(
                "EXPLICIT_TRIM",
                new TrimExpression { Value = Source("Code") }));
        var row = Row(
            "TR-A01",
            ("Code", "  AbC  "),
            ("Empty", ""),
            ("Missing", null),
            ("Blank", " \t "),
            ("Amount", " +1234.50 "),
            ("RawDate", "20261006"),
            ("Enabled", "TRUE"));

        var result = await Evaluate(program, source, target, row);

        var evaluatedRow = Assert.Single(result.Rows);
        Assert.Equal(TransformationRowStatus.Done, evaluatedRow.Status);
        Assert.Empty(evaluatedRow.Errors);
        var values = Assert.Single(evaluatedRow.Outputs).Values;
        Assert.Equal("AbC", values["DIRECT_TEXT"]);
        Assert.Equal("DEFAULT", values["EMPTY_DEFAULT"]);
        Assert.Equal("MISSING", values["NULL_COALESCE"]);
        Assert.Null(values["OPTIONAL_BLANK"]);
        Assert.Equal(1234.50m, values["AMOUNT"]);
        Assert.Equal(new DateOnly(2026, 10, 6), values["EFFECTIVE_DATE"]);
        Assert.Equal(true, values["ENABLED"]);
        Assert.Equal("AbC", values["EXPLICIT_TRIM"]);
    }

    [Fact]
    public async Task Evaluate_returns_all_assignment_errors_with_exact_paths()
    {
        var source = SourceTable("RequiredText", "Amount", "Date");
        var target = TargetTable(
            Column("REQUIRED_TEXT", ColumnDataType.Text, required: true),
            Column("AMOUNT", ColumnDataType.Decimal, required: true),
            Column("EFFECTIVE_DATE", ColumnDataType.Date, required: true));
        var program = Program(
            Assignment("REQUIRED_TEXT", Source("RequiredText")),
            Assignment("AMOUNT", Source("Amount")),
            Assignment("EFFECTIVE_DATE", Source("Date"), "dd/MM/yyyy"));

        var result = await Evaluate(
            program,
            source,
            target,
            Row(
                "TR-A02-A03",
                ("RequiredText", "   "),
                ("Amount", "1,234.50"),
                ("Date", "31/02/2026")));

        var evaluatedRow = Assert.Single(result.Rows);
        Assert.Equal(TransformationRowStatus.Invalid, evaluatedRow.Status);
        Assert.Empty(evaluatedRow.Outputs);
        Assert.Collection(
            evaluatedRow.Errors,
            error => AssertError(
                error,
                "default",
                "REQUIRED_TEXT",
                "outputs[0].fields[0].value",
                "Required"),
            error => AssertError(
                error,
                "default",
                "AMOUNT",
                "outputs[0].fields[1].value",
                "InvalidDecimal"),
            error => AssertError(
                error,
                "default",
                "EFFECTIVE_DATE",
                "outputs[0].fields[2].value",
                "InvalidDate"));
    }

    [Theory]
    [InlineData(null, "<null>")]
    [InlineData("", "<empty>")]
    [InlineData(" \t ", "<empty>")]
    [InlineData("  value  ", "value")]
    public async Task Explicit_trim_distinguishes_null_empty_and_text(
        string? input,
        string? expected)
    {
        var trimmed = new TrimExpression { Value = Source("Raw") };
        var observableValue = new CaseExpression
        {
            Cases =
            [
                new CaseArm
                {
                    When = new IsNullExpression { Value = trimmed },
                    Then = Text("<null>")
                },
                new CaseArm
                {
                    When = new EqualExpression
                    {
                        Left = trimmed,
                        Right = Text("")
                    },
                    Then = Text("<empty>")
                }
            ],
            Else = trimmed
        };
        var result = await Evaluate(
            Program(Assignment("VALUE", observableValue)),
            SourceTable("Raw"),
            TargetTable(Column("VALUE", ColumnDataType.Text)),
            Row("TR-A04", ("Raw", input)));

        var values = Assert.Single(Assert.Single(result.Rows).Outputs).Values;
        Assert.Equal(expected, values["VALUE"]);
    }

    [Fact]
    public async Task Coalesce_only_falls_back_for_null_while_default_if_blank_handles_empty()
    {
        var coalesced = new CoalesceExpression
        {
            Values = [Source("Raw"), Text("fallback")]
        };
        var observableCoalesce = new CaseExpression
        {
            Cases =
            [
                new CaseArm
                {
                    When = Equal(coalesced, Text("")),
                    Then = Text("<empty>")
                }
            ],
            Else = coalesced
        };
        var defaulted = new DefaultIfBlankExpression
        {
            Value = Source("Raw"),
            Fallback = Text("fallback")
        };
        var batch = new TransformationBatch(
            [Row("empty", ("Raw", "")), Row("null", ("Raw", null))]);

        var result = await Evaluate(
            Program(
                Assignment("COALESCED", observableCoalesce),
                Assignment("DEFAULTED", defaulted)),
            SourceTable("Raw"),
            TargetTable(
                Column("COALESCED", ColumnDataType.Text),
                Column("DEFAULTED", ColumnDataType.Text)),
            batch);

        Assert.Equal("<empty>", result.Rows[0].Outputs[0].Values["COALESCED"]);
        Assert.Equal("fallback", result.Rows[0].Outputs[0].Values["DEFAULTED"]);
        Assert.Equal("fallback", result.Rows[1].Outputs[0].Values["COALESCED"]);
        Assert.Equal("fallback", result.Rows[1].Outputs[0].Values["DEFAULTED"]);
    }

    [Theory]
    [MemberData(nameof(BooleanCases))]
    public async Task Boolean_operators_follow_three_valued_truth_table(
        BooleanExpression predicate,
        bool negate,
        TransformationRowStatus expectedStatus)
    {
        var sourceWhen = negate
            ? new NotExpression { Value = predicate }
            : predicate;
        var program = Program(Assignment("VALUE", Text("ok"))) with
        {
            SourceWhen = sourceWhen
        };

        var result = await Evaluate(
            program,
            SourceTable("Raw"),
            TargetTable(Column("VALUE", ColumnDataType.Text)),
            Row("boolean", ("Raw", null)));

        Assert.Equal(expectedStatus, Assert.Single(result.Rows).Status);
    }

    [Theory]
    [InlineData("ย้ายโอนย้ายงาน", "%โอนย้าย%", true)]
    [InlineData("A1", "a_", true)]
    [InlineData("A12", "A_", false)]
    [InlineData("B", "[abc]", true)]
    [InlineData("C", "[a-c]", true)]
    [InlineData("D", "[^a-c]", true)]
    [InlineData(null, "%x%", false)]
    public async Task Like_matches_sql_server_style_patterns(
        string? value,
        string pattern,
        bool expectedEmit)
    {
        var program = Program(Assignment("VALUE", Text("ok"))) with
        {
            SourceWhen = new LikeExpression
            {
                Value = Source("Raw"),
                Pattern = Text(pattern)
            }
        };

        var result = await Evaluate(
            program,
            SourceTable("Raw"),
            TargetTable(Column("VALUE", ColumnDataType.Text)),
            Row("like", ("Raw", value)));

        Assert.Equal(
            expectedEmit
                ? TransformationRowStatus.Done
                : TransformationRowStatus.Filtered,
            Assert.Single(result.Rows).Status);
    }

    [Fact]
    public async Task Text_equality_and_in_are_ordinal_case_insensitive()
    {
        var program = Program(Assignment("VALUE", Text("ok"))) with
        {
            SourceWhen = new AndExpression
            {
                Operands =
                [
                    new EqualExpression
                    {
                        Left = Source("Product"),
                        Right = Text("pmx")
                    },
                    new InExpression
                    {
                        Value = Source("System"),
                        Values = [Text("TIB"), Text("PMIB")]
                    }
                ]
            }
        };

        var result = await Evaluate(
            program,
            SourceTable("Product", "System"),
            TargetTable(Column("VALUE", ColumnDataType.Text)),
            Row("text", ("Product", "PMX"), ("System", "pMiB")));

        Assert.Equal(
            TransformationRowStatus.Done,
            Assert.Single(result.Rows).Status);
    }

    [Theory]
    [MemberData(nameof(AdditionalOperatorCases))]
    public async Task Evaluate_supports_remaining_pure_boolean_operators(
        BooleanExpression predicate,
        TransformationRowStatus expectedStatus)
    {
        var program = Program(Assignment("VALUE", Text("ok"))) with
        {
            SourceWhen = predicate
        };

        var result = await Evaluate(
            program,
            SourceTable(),
            TargetTable(Column("VALUE", ColumnDataType.Text)),
            Row("operators"));

        Assert.Equal(expectedStatus, Assert.Single(result.Rows).Status);
    }

    [Theory]
    [MemberData(nameof(AdditionalTargetConversions))]
    public async Task Assignment_converts_remaining_target_types(
        ColumnDataType targetType,
        string input,
        string? inputFormat,
        object expected)
    {
        var result = await Evaluate(
            Program(Assignment("VALUE", Source("Raw"), inputFormat)),
            SourceTable("Raw"),
            TargetTable(Column("VALUE", targetType)),
            Row("conversion", ("Raw", input)));

        var value = Assert.Single(Assert.Single(result.Rows).Outputs)
            .Values["VALUE"];
        Assert.Equal(expected, value);
    }

    [Theory]
    [MemberData(nameof(InvalidTargetConversions))]
    public async Task Assignment_returns_stable_conversion_error_codes(
        ColumnDataType targetType,
        string input,
        string? inputFormat,
        string expectedReason)
    {
        var result = await Evaluate(
            Program(Assignment("VALUE", Source("Raw"), inputFormat)),
            SourceTable("Raw"),
            TargetTable(Column("VALUE", targetType, required: true)),
            Row("invalid-conversion", ("Raw", input)));

        var error = Assert.Single(Assert.Single(result.Rows).Errors);
        AssertError(
            error,
            "default",
            "VALUE",
            "outputs[0].fields[0].value",
            expectedReason);
    }

    [Fact]
    public async Task Case_is_first_match_wins_and_skips_later_errors()
    {
        var expression = new CaseExpression
        {
            Cases =
            [
                new CaseArm
                {
                    When = new EqualExpression
                    {
                        Left = Source("GarageType"),
                        Right = Text("อู่ในเครือ")
                    },
                    Then = Text("ซ่อมอู่")
                },
                new CaseArm
                {
                    When = new GreaterThanExpression
                    {
                        Left = new ConvertExpression
                        {
                            Value = Source("BrokenNumber"),
                            Type = ExpressionValueType.Decimal
                        },
                        Right = Decimal(0m)
                    },
                    Then = Text("must-not-run")
                }
            ],
            Else = Text("")
        };

        var result = await Evaluate(
            Program(Assignment("CWORKSHOP", expression)),
            SourceTable("GarageType", "BrokenNumber"),
            TargetTable(Column("CWORKSHOP", ColumnDataType.Text)),
            Row(
                "TR-C01",
                ("GarageType", "อู่ในเครือ"),
                ("BrokenNumber", "not-a-number")));

        var row = Assert.Single(result.Rows);
        Assert.Equal(TransformationRowStatus.Done, row.Status);
        Assert.Equal(
            "ซ่อมอู่",
            Assert.Single(row.Outputs).Values["CWORKSHOP"]);
    }

    [Fact]
    public async Task Like_case_falls_back_to_one_based_substring()
    {
        var value = new CaseExpression
        {
            Cases =
            [
                new CaseArm
                {
                    When = new LikeExpression
                    {
                        Value = Source("REMARK"),
                        Pattern = Text("%โอนย้าย%")
                    },
                    Then = Text("N")
                }
            ],
            Else = new SubstringExpression
            {
                Value = Source("ApplicationID"),
                Start = 1,
                Length = 1
            }
        };
        var rows = new TransformationBatch(
            [
                Row(
                    "TR-E01",
                    ("REMARK", "รายการโอนย้ายเดิม"),
                    ("ApplicationID", "PMX123456")),
                Row(
                    "TR-E02",
                    ("REMARK", "รายการใหม่"),
                    ("ApplicationID", "PMX123456"))
            ]);

        var result = await Evaluate(
            Program(Assignment("PRODUCT", value)),
            SourceTable("REMARK", "ApplicationID"),
            TargetTable(Column("PRODUCT", ColumnDataType.Text)),
            rows);

        Assert.Equal("N", result.Rows[0].Outputs[0].Values["PRODUCT"]);
        Assert.Equal("P", result.Rows[1].Outputs[0].Values["PRODUCT"]);
    }

    [Fact]
    public async Task Source_filter_runs_before_output_filter()
    {
        var program = new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            SourceWhen = new EqualExpression
            {
                Left = Source("Gate"),
                Right = Text("pass")
            },
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    EmitWhen = new EqualExpression
                    {
                        Left = new ConvertExpression
                        {
                            Value = Source("BrokenDate"),
                            Type = ExpressionValueType.Date
                        },
                        Right = Date("2026-10-12")
                    },
                    Fields = [Assignment("VALUE", Text("ok"))]
                }
            ]
        };

        var result = await Evaluate(
            program,
            SourceTable("Gate", "BrokenDate"),
            TargetTable(Column("VALUE", ColumnDataType.Text)),
            Row("filter", ("Gate", "stop"), ("BrokenDate", "invalid")));

        var row = Assert.Single(result.Rows);
        Assert.Equal(TransformationRowStatus.Filtered, row.Status);
        Assert.Empty(row.Errors);
        Assert.Empty(row.Outputs);
    }

    [Fact]
    public async Task Evaluate_returns_every_emitted_output_with_its_key()
    {
        var program = new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "pmx",
                    Fields = [Assignment("VALUE", Text("PMX"))]
                },
                new TransformationOutput
                {
                    OutputKey = "ctp",
                    Fields = [Assignment("VALUE", Text("CTP"))]
                }
            ]
        };

        var result = await Evaluate(
            program,
            SourceTable(),
            TargetTable(Column("VALUE", ColumnDataType.Text)),
            Row("two-outputs"));

        var row = Assert.Single(result.Rows);
        Assert.Equal(TransformationRowStatus.Done, row.Status);
        Assert.Collection(
            row.Outputs,
            output => Assert.Equal("pmx", output.OutputKey),
            output => Assert.Equal("ctp", output.OutputKey));
    }

    [Fact]
    public async Task Source_when_error_has_no_output_or_field_but_keeps_path()
    {
        var program = Program(Assignment("VALUE", Text("ok"))) with
        {
            SourceWhen = new EqualExpression
            {
                Left = new ConvertExpression
                {
                    Value = Source("BrokenDate"),
                    Type = ExpressionValueType.Date
                },
                Right = Date("2026-10-12")
            }
        };

        var result = await Evaluate(
            program,
            SourceTable("BrokenDate"),
            TargetTable(Column("VALUE", ColumnDataType.Text)),
            Row("source-error", ("BrokenDate", "invalid")));

        var error = Assert.Single(Assert.Single(result.Rows).Errors);
        Assert.Null(error.OutputKey);
        Assert.Null(error.Field);
        Assert.Equal("sourceWhen.left", error.ExpressionPath);
        Assert.Equal("InvalidDate", error.Reason);
    }

    [Fact]
    public async Task Emit_when_error_has_output_but_no_field_and_keeps_path()
    {
        var program = new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "pmx",
                    EmitWhen = new EqualExpression
                    {
                        Left = new ConvertExpression
                        {
                            Value = Source("BrokenDate"),
                            Type = ExpressionValueType.Date
                        },
                        Right = Date("2026-10-12")
                    },
                    Fields = [Assignment("VALUE", Text("ok"))]
                }
            ]
        };

        var result = await Evaluate(
            program,
            SourceTable("BrokenDate"),
            TargetTable(Column("VALUE", ColumnDataType.Text)),
            Row("emit-error", ("BrokenDate", "invalid")));

        var error = Assert.Single(Assert.Single(result.Rows).Errors);
        Assert.Equal("pmx", error.OutputKey);
        Assert.Null(error.Field);
        Assert.Equal("outputs[0].emitWhen.left", error.ExpressionPath);
        Assert.Equal("InvalidDate", error.Reason);
    }

    [Fact]
    public async Task Explicit_convert_reports_the_expression_path()
    {
        var program = Program(
            Assignment(
                "AMOUNT",
                new ConvertExpression
                {
                    Value = Source("Amount"),
                    Type = ExpressionValueType.Decimal
                }));

        var result = await Evaluate(
            program,
            SourceTable("Amount"),
            TargetTable(Column("AMOUNT", ColumnDataType.Decimal)),
            Row("convert", ("Amount", "1,234.50")));

        var error = Assert.Single(Assert.Single(result.Rows).Errors);
        AssertError(
            error,
            "default",
            "AMOUNT",
            "outputs[0].fields[0].value",
            "InvalidDecimal");
    }

    [Fact]
    public async Task Evaluate_reads_typed_metadata_without_changing_source_values()
    {
        var program = Program(
            Assignment(
                "FILE_NAME",
                new MetadataExpression { Name = "fileName" }),
            Assignment(
                "ROW_NUMBER",
                new MetadataExpression { Name = "rowNumber" }));
        var row = new TransformationSourceRow(
            "metadata",
            new Dictionary<string, string?>(),
            new Dictionary<string, object?>
            {
                ["fileName"] = "sample.csv",
                ["rowNumber"] = 7
            },
            DateTimeOffset.UnixEpoch);

        var result = await Evaluate(
            program,
            SourceTable(),
            TargetTable(
                Column("FILE_NAME", ColumnDataType.Text),
                Column("ROW_NUMBER", ColumnDataType.Integer)),
            row);

        var values = Assert.Single(Assert.Single(result.Rows).Outputs).Values;
        Assert.Equal("sample.csv", values["FILE_NAME"]);
        Assert.Equal(7, values["ROW_NUMBER"]);
    }

    [Theory]
    [InlineData("2026-10-08T16:59:59Z", 2026, 10, 8)]
    [InlineData("2026-10-08T17:00:00Z", 2026, 10, 9)]
    public async Task Current_date_uses_persisted_instant_in_Asia_Bangkok(
        string evaluationAt,
        int year,
        int month,
        int day)
    {
        var result = await Evaluate(
            Program(Assignment("CURRENT_DATE", new CurrentDateExpression())),
            SourceTable(),
            TargetTable(Column("CURRENT_DATE", ColumnDataType.Date)),
            RowAt(
                "current-date",
                DateTimeOffset.Parse(
                    evaluationAt,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind)));

        var value = Assert.Single(Assert.Single(result.Rows).Outputs)
            .Values["CURRENT_DATE"];
        Assert.Equal(new DateOnly(year, month, day), value);
    }

    [Fact]
    public async Task Evaluation_time_metadata_uses_the_same_Bangkok_instant()
    {
        var evaluationAt = DateTimeOffset.Parse(
            "2026-10-08T17:00:00Z",
            null,
            System.Globalization.DateTimeStyles.RoundtripKind);
        var result = await Evaluate(
            Program(
                Assignment(
                    "EVALUATION_TIME",
                    new MetadataExpression { Name = "evaluationTime" })),
            SourceTable(),
            TargetTable(Column("EVALUATION_TIME", ColumnDataType.DateTime)),
            RowAt("evaluation-time", evaluationAt));

        var value = Assert.Single(Assert.Single(result.Rows).Outputs)
            .Values["EVALUATION_TIME"];
        Assert.Equal(new DateTime(2026, 10, 9, 0, 0, 0), value);
    }

    [Theory]
    [InlineData("  1กก 1234  ", "1กก 1234")]
    [InlineData("กข-99  01", "กข-99  01")]
    [InlineData(null, null)]
    public async Task Format_reg_no_v1_is_null_preserving_and_trims_only_edges(
        string? input,
        string? expected)
    {
        var result = await Evaluate(
            Program(
                Assignment(
                    "REGISTER_ID",
                    new CallExpression
                    {
                        Function = "formatRegNo@1",
                        Arguments = [Source("RegisterId")]
                    })),
            SourceTable("RegisterId"),
            TargetTable(Column("REGISTER_ID", ColumnDataType.Text)),
            Row("TR-F01-F03", ("RegisterId", input)));

        var value = Assert.Single(Assert.Single(result.Rows).Outputs)
            .Values["REGISTER_ID"];
        Assert.Equal(expected, value);
    }

    public static TheoryData<BooleanExpression, bool, TransformationRowStatus>
        BooleanCases => new()
        {
            {
                new EqualExpression
                {
                    Left = Source("Raw"),
                    Right = Text("x")
                },
                true,
                TransformationRowStatus.Filtered
            },
            {
                new AndExpression
                {
                    Operands =
                    [
                        Equal(Text("a"), Text("b")),
                        Equal(Source("Raw"), Text("x"))
                    ]
                },
                true,
                TransformationRowStatus.Done
            },
            {
                new AndExpression
                {
                    Operands =
                    [
                        Equal(Text("a"), Text("a")),
                        Equal(Source("Raw"), Text("x"))
                    ]
                },
                true,
                TransformationRowStatus.Filtered
            },
            {
                new OrExpression
                {
                    Operands =
                    [
                        Equal(Text("a"), Text("a")),
                        Equal(Source("Raw"), Text("x"))
                    ]
                },
                false,
                TransformationRowStatus.Done
            },
            {
                new OrExpression
                {
                    Operands =
                    [
                        Equal(Text("a"), Text("b")),
                        Equal(Source("Raw"), Text("x"))
                    ]
                },
                true,
                TransformationRowStatus.Filtered
            },
            {
                new NotExpression
                {
                    Value = Equal(Source("Raw"), Text("x"))
                },
                false,
                TransformationRowStatus.Filtered
            }
        };

    public static TheoryData<BooleanExpression, TransformationRowStatus>
        AdditionalOperatorCases => new()
        {
            {
                new NotEqualExpression
                {
                    Left = Text("A"),
                    Right = Text("b")
                },
                TransformationRowStatus.Done
            },
            {
                new GreaterThanExpression
                {
                    Left = Decimal(2m),
                    Right = Decimal(1m)
                },
                TransformationRowStatus.Done
            },
            {
                new GreaterThanOrEqualExpression
                {
                    Left = Date("2026-10-12"),
                    Right = Date("2026-10-12")
                },
                TransformationRowStatus.Done
            },
            {
                new LessThanExpression
                {
                    Left = Integer(1),
                    Right = Integer(2)
                },
                TransformationRowStatus.Done
            },
            {
                new LessThanOrEqualExpression
                {
                    Left = DateTimeLiteral("2026-10-12T10:30:00"),
                    Right = DateTimeLiteral("2026-10-12T10:30:00")
                },
                TransformationRowStatus.Done
            },
            {
                new IsBlankExpression { Value = Text(" \t ") },
                TransformationRowStatus.Done
            },
            {
                new IsBlankExpression { Value = Text(null) },
                TransformationRowStatus.Done
            },
            {
                new NotBlankExpression { Value = Text(" value ") },
                TransformationRowStatus.Done
            },
            {
                new NotBlankExpression { Value = Text(null) },
                TransformationRowStatus.Filtered
            },
            {
                new NotInExpression
                {
                    Value = Text("x"),
                    Values = [Text("a"), Text("b")]
                },
                TransformationRowStatus.Done
            }
        };

    public static TheoryData<ColumnDataType, string, string?, object>
        AdditionalTargetConversions => new()
        {
            { ColumnDataType.Integer, " +42 ", null, 42 },
            {
                ColumnDataType.DateTime,
                " 2026-10-08T23:59:01 ",
                null,
                new DateTime(2026, 10, 8, 23, 59, 1)
            },
            {
                ColumnDataType.Guid,
                " b381a1c8-11f8-42fc-a585-992482b4fcb6 ",
                null,
                Guid.Parse("b381a1c8-11f8-42fc-a585-992482b4fcb6")
            }
        };

    public static TheoryData<ColumnDataType, string, string?, string>
        InvalidTargetConversions => new()
        {
            {
                ColumnDataType.Date,
                "2026-02-30",
                null,
                "InvalidDate"
            },
            {
                ColumnDataType.DateTime,
                "2026-10-08 23:59:01",
                null,
                "InvalidDateTime"
            },
            {
                ColumnDataType.Integer,
                "2147483648",
                null,
                "InvalidInteger"
            },
            { ColumnDataType.Boolean, "yes", null, "InvalidBoolean" },
            { ColumnDataType.Guid, "not-a-guid", null, "InvalidGuid" }
        };

    private static async Task<TransformationBatchResult> Evaluate(
        TransformationProgram program,
        TableDefinition source,
        TableDefinition target,
        TransformationSourceRow row)
    {
        return await Evaluate(
            program,
            source,
            target,
            new TransformationBatch([row]));
    }

    private static async Task<TransformationBatchResult> Evaluate(
        TransformationProgram program,
        TableDefinition source,
        TableDefinition target,
        TransformationBatch batch)
    {
        var module = new TransformationModule();
        var compilation = module.Compile(
            program,
            new TransformationSchema(source, target));
        Assert.True(
            compilation.IsSuccess,
            string.Join(
                Environment.NewLine,
                compilation.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Path}: {diagnostic.Code}")));

        return await module.EvaluateAsync(compilation.CompiledProgram!, batch);
    }

    private static TransformationProgram Program(
        params FieldAssignment[] fields)
    {
        return new TransformationProgram
        {
            SemanticsProfile = "sqlserver-legacy-v1",
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    Fields = fields
                }
            ]
        };
    }

    private static FieldAssignment Assignment(
        string target,
        ValueExpression value,
        string? inputFormat = null)
    {
        return new FieldAssignment
        {
            NormalizedColumn = target,
            Value = value,
            InputFormat = inputFormat
        };
    }

    private static TransformationSourceRow Row(
        string key,
        params (string Field, string? Value)[] values)
    {
        return new TransformationSourceRow(
            key,
            values.ToDictionary(
                value => value.Field,
                value => value.Value,
                StringComparer.Ordinal),
            new Dictionary<string, object?>(),
            DateTimeOffset.UnixEpoch);
    }

    private static TransformationSourceRow RowAt(
        string key,
        DateTimeOffset evaluationAt,
        params (string Field, string? Value)[] values)
    {
        return new TransformationSourceRow(
            key,
            values.ToDictionary(
                value => value.Field,
                value => value.Value,
                StringComparer.Ordinal),
            new Dictionary<string, object?>(),
            evaluationAt);
    }

    private static TableDefinition SourceTable(params string[] fields)
    {
        return new TableDefinition(
            "source_rows",
            TableKind.Source,
            fields.Select((field, index) => new ColumnDefinition(
                field,
                ColumnDataType.Text,
                false,
                index + 1)).ToArray());
    }

    private static TableDefinition TargetTable(
        params ColumnDefinition[] columns)
    {
        return new TableDefinition(
            "normalized_rows",
            TableKind.Normalized,
            columns);
    }

    private static ColumnDefinition Column(
        string name,
        ColumnDataType type,
        bool required = false)
    {
        return new ColumnDefinition(name, type, required, 1);
    }

    private static SourceExpression Source(string field)
    {
        return new SourceExpression { Field = field };
    }

    private static LiteralExpression Text(string? value)
    {
        return Literal(ExpressionValueType.Text, value);
    }

    private static LiteralExpression Decimal(decimal value)
    {
        return Literal(ExpressionValueType.Decimal, value);
    }

    private static LiteralExpression Date(string value)
    {
        return Literal(ExpressionValueType.Date, value);
    }

    private static LiteralExpression DateTimeLiteral(string value)
    {
        return Literal(ExpressionValueType.DateTime, value);
    }

    private static LiteralExpression Integer(int value)
    {
        return Literal(ExpressionValueType.Integer, value);
    }

    private static LiteralExpression Literal(
        ExpressionValueType type,
        object? value)
    {
        return new LiteralExpression
        {
            Type = type,
            Value = JsonSerializer.SerializeToElement(value, value?.GetType()
                ?? typeof(object))
        };
    }

    private static EqualExpression Equal(
        ValueExpression left,
        ValueExpression right)
    {
        return new EqualExpression { Left = left, Right = right };
    }

    private static void AssertError(
        TransformationRowError error,
        string outputKey,
        string field,
        string path,
        string reason)
    {
        Assert.Equal(outputKey, error.OutputKey);
        Assert.Equal(field, error.Field);
        Assert.Equal(path, error.ExpressionPath);
        Assert.Equal(reason, error.Reason);
    }
}
