import type {
  BooleanExpression,
  ColumnDataType,
  ExpressionValueType,
  MappingCapabilities,
  MappingDraftDiagnostic,
  TableDefinition,
  ValueExpression,
} from "@/lib/api/contracts";
import {
  booleanExpressionStats,
  defaultLiteralValue,
  diagnosticsAtPath,
  expressionTypeForColumn,
  expressionValueType,
  valueExpressionStats,
} from "@/lib/transformation-draft";

interface EditorContextProps {
  sourceTable: TableDefinition;
  capabilities: MappingCapabilities;
  diagnostics: MappingDraftDiagnostic[];
  programNodeCount: number;
  depth?: number;
  readOnly?: boolean;
}

interface ValueExpressionEditorProps extends EditorContextProps {
  value: ValueExpression | null;
  targetType?: ColumnDataType;
  expectedType?: ExpressionValueType;
  allowNone?: boolean;
  allowLiteralTypeChange?: boolean;
  path: string | null;
  onChange: (value: ValueExpression | null) => void;
}

interface BooleanExpressionEditorProps extends EditorContextProps {
  value: BooleanExpression | null;
  allowNone?: boolean;
  path: string;
  onChange: (value: BooleanExpression | null) => void;
}

type ComparisonExpression = {
  kind: "eq" | "ne" | "gt" | "gte" | "lt" | "lte";
  left: ValueExpression;
  right: ValueExpression;
};

type UnaryValueExpression = {
  kind: "isNull" | "isBlank" | "notBlank";
  value: ValueExpression;
};

type LookupExpression = {
  kind: "exists" | "notExists";
  lookup: string;
  arguments: Record<string, ValueExpression>;
};

const inputClassName =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-950 outline-none transition focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100 disabled:bg-slate-100 disabled:text-slate-500";

const expressionTypes: ExpressionValueType[] = [
  "text",
  "decimal",
  "integer",
  "date",
  "dateTime",
  "boolean",
  "guid",
];

const typeToColumn: Record<ExpressionValueType, ColumnDataType> = {
  text: "Text",
  decimal: "Decimal",
  integer: "Integer",
  date: "Date",
  dateTime: "DateTime",
  boolean: "Boolean",
  guid: "Guid",
};

const valueLabels: Record<ValueExpression["kind"], string> = {
  source: "Direct field",
  metadata: "Metadata",
  literal: "Constant",
  trim: "Trim",
  substring: "Substring",
  convert: "Convert",
  coalesce: "Coalesce",
  defaultIfBlank: "Default if blank",
  case: "CASE",
  currentDate: "Current date",
  call: "Named transform",
};

const booleanLabels: Record<BooleanExpression["kind"], string> = {
  eq: "Equals",
  ne: "Not equal",
  gt: "Greater than",
  gte: "Greater than or equal",
  lt: "Less than",
  lte: "Less than or equal",
  isNull: "Is null",
  isBlank: "Is blank",
  notBlank: "Is not blank",
  like: "LIKE",
  in: "IN",
  notIn: "NOT IN",
  and: "AND",
  or: "OR",
  not: "NOT",
  exists: "Named lookup exists",
  notExists: "Named lookup does not exist",
};

function Messages({ messages }: { messages: MappingDraftDiagnostic[] }) {
  if (messages.length === 0) return null;

  return (
    <ul className="mt-1 space-y-1 text-xs font-medium text-rose-700">
      {messages.map((diagnostic, index) => (
        <li key={`${diagnostic.path}:${diagnostic.code}:${index}`}>
          {diagnostic.code}: {diagnostic.message}
        </li>
      ))}
    </ul>
  );
}

function parseNumericLiteral(value: string, integer: boolean) {
  if (value.trim() === "") return null;
  const parsed = integer ? Number.parseInt(value, 10) : Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function literal(type: ExpressionValueType): ValueExpression {
  return {
    kind: "literal",
    type,
    value: defaultLiteralValue(typeToColumn[type]),
  };
}

function defaultValue(
  type: ExpressionValueType,
  sourceTable: TableDefinition,
): ValueExpression {
  const matchingSource = sourceTable.columns.find(
    (column) => expressionTypeForColumn(column.dataType) === type,
  );
  return matchingSource
    ? { kind: "source", field: matchingSource.name }
    : literal(type);
}

function defaultNamedArgument(
  argumentName: string,
  sourceTable: TableDefinition,
): ValueExpression {
  const canonicalName = argumentName.replaceAll("_", "").toLowerCase();
  const matchingSource = sourceTable.columns.find(
    (column) =>
      column.dataType === "Text" &&
      column.name.replaceAll("_", "").toLowerCase() === canonicalName,
  );
  return matchingSource
    ? { kind: "source", field: matchingSource.name }
    : defaultValue("text", sourceTable);
}

function lookupArguments(
  lookup: string,
  sourceTable: TableDefinition,
): Record<string, ValueExpression> {
  if (lookup === "mtiOriginalPolicyExists@1") {
    return {
      systemId: defaultNamedArgument("systemId", sourceTable),
      productName: defaultNamedArgument("productName", sourceTable),
      loanNumber: defaultNamedArgument("loanNumber", sourceTable),
    };
  }
  return {};
}

function defaultComparison(sourceTable: TableDefinition): ComparisonExpression {
  const left = sourceTable.columns[0]
    ? { kind: "source" as const, field: sourceTable.columns[0].name }
    : literal("text");
  return {
    kind: "eq",
    left,
    right: literal(expressionValueType(left, sourceTable) ?? "text"),
  };
}

function preferredType(
  value: ValueExpression | null,
  targetType: ColumnDataType | undefined,
  expectedType: ExpressionValueType | undefined,
  sourceTable: TableDefinition,
) {
  return (
    expectedType ??
    (targetType ? expressionTypeForColumn(targetType) : null) ??
    expressionValueType(value, sourceTable) ??
    "text"
  );
}

function canNest(
  depth: number,
  programNodeCount: number,
  capabilities: MappingCapabilities,
  additionalNodes = 1,
) {
  return (
    depth + 1 < capabilities.transformation.limits.maxExpressionDepth &&
    programNodeCount + additionalNodes <=
      capabilities.transformation.limits.maxExpressionNodes
  );
}

const valueKindShape: Record<
  ValueExpression["kind"],
  { nodes: number; maxRelativeDepth: number }
> = {
  source: { nodes: 1, maxRelativeDepth: 0 },
  metadata: { nodes: 1, maxRelativeDepth: 0 },
  literal: { nodes: 1, maxRelativeDepth: 0 },
  trim: { nodes: 2, maxRelativeDepth: 1 },
  substring: { nodes: 2, maxRelativeDepth: 1 },
  convert: { nodes: 2, maxRelativeDepth: 1 },
  coalesce: { nodes: 3, maxRelativeDepth: 1 },
  defaultIfBlank: { nodes: 3, maxRelativeDepth: 1 },
  case: { nodes: 6, maxRelativeDepth: 2 },
  currentDate: { nodes: 1, maxRelativeDepth: 0 },
  call: { nodes: 2, maxRelativeDepth: 1 },
};

const booleanKindShape: Record<
  BooleanExpression["kind"],
  { nodes: number; maxRelativeDepth: number }
> = {
  eq: { nodes: 3, maxRelativeDepth: 1 },
  ne: { nodes: 3, maxRelativeDepth: 1 },
  gt: { nodes: 3, maxRelativeDepth: 1 },
  gte: { nodes: 3, maxRelativeDepth: 1 },
  lt: { nodes: 3, maxRelativeDepth: 1 },
  lte: { nodes: 3, maxRelativeDepth: 1 },
  isNull: { nodes: 2, maxRelativeDepth: 1 },
  isBlank: { nodes: 2, maxRelativeDepth: 1 },
  notBlank: { nodes: 2, maxRelativeDepth: 1 },
  like: { nodes: 3, maxRelativeDepth: 1 },
  in: { nodes: 3, maxRelativeDepth: 1 },
  notIn: { nodes: 3, maxRelativeDepth: 1 },
  and: { nodes: 7, maxRelativeDepth: 2 },
  or: { nodes: 7, maxRelativeDepth: 2 },
  not: { nodes: 4, maxRelativeDepth: 2 },
  exists: { nodes: 4, maxRelativeDepth: 1 },
  notExists: { nodes: 4, maxRelativeDepth: 1 },
};

export function ValueExpressionEditor({
  value,
  targetType,
  expectedType,
  allowNone = true,
  allowLiteralTypeChange = false,
  sourceTable,
  capabilities,
  diagnostics,
  programNodeCount,
  path,
  onChange,
  depth = 0,
  readOnly = false,
}: ValueExpressionEditorProps) {
  const type = preferredType(value, targetType, expectedType, sourceTable);
  const mode = value?.kind ?? "none";
  const currentNodes = value ? valueExpressionStats(value).nodes : 0;
  const rootMessages = path ? diagnosticsAtPath(diagnostics, path) : [];

  function canSelectKind(kind: ValueExpression["kind"]) {
    const shape = valueKindShape[kind];
    return (
      depth + shape.maxRelativeDepth <
        capabilities.transformation.limits.maxExpressionDepth &&
      programNodeCount - currentNodes + shape.nodes <=
        capabilities.transformation.limits.maxExpressionNodes
    );
  }

  function selectMode(kind: "none" | ValueExpression["kind"]) {
    if (kind === "none") {
      onChange(null);
      return;
    }

    const source = defaultValue("text", sourceTable);
    switch (kind) {
      case "source":
        onChange({ kind, field: sourceTable.columns[0]?.name ?? "" });
        break;
      case "metadata":
        onChange({
          kind,
          name: capabilities.transformation.metadataFields[0] ?? "fileName",
        });
        break;
      case "literal":
        onChange(literal(type));
        break;
      case "trim":
        onChange({ kind, value: source });
        break;
      case "substring":
        onChange({ kind, value: source, start: 1, length: 1 });
        break;
      case "convert": {
        const conversionType =
          type !== "text" &&
          capabilities.transformation.targetConversionTypes.includes(type)
            ? type
            : capabilities.transformation.targetConversionTypes[0] ?? "date";
        onChange({ kind, value: source, type: conversionType });
        break;
      }
      case "coalesce":
        onChange({
          kind,
          values: [
            defaultValue(type, sourceTable),
            defaultValue(type, sourceTable),
          ],
        });
        break;
      case "defaultIfBlank":
        onChange({ kind, value: source, fallback: literal("text") });
        break;
      case "case":
        onChange({
          kind,
          cases: [
            {
              when: defaultComparison(sourceTable),
              then: defaultValue(type, sourceTable),
            },
          ],
          else: defaultValue(type, sourceTable),
        });
        break;
      case "currentDate":
        onChange({ kind });
        break;
      case "call":
        onChange({
          kind,
          function: capabilities.transformation.functions[0] ?? "",
          arguments: [source],
        });
        break;
    }
  }

  const availableKinds = capabilities.transformation.valueExpressions;

  return (
    <div
      className={depth > 0 ? "border-l-2 border-slate-200 pl-3" : undefined}
      data-expression-path={path ?? undefined}
    >
      <select
        aria-label="Value expression type"
        className={inputClassName}
        disabled={readOnly}
        onChange={(event) =>
          selectMode(event.target.value as "none" | ValueExpression["kind"])
        }
        value={mode}
      >
        {allowNone ? <option value="none">Do not map</option> : null}
        {availableKinds.map((kind) => {
          return (
            <option
              disabled={
                (kind === "source" && sourceTable.columns.length === 0) ||
                (kind === "currentDate" && type !== "date") ||
                !canSelectKind(kind)
              }
              key={kind}
              value={kind}
            >
              {valueLabels[kind]}
            </option>
          );
        })}
      </select>

      {value?.kind === "source" ? (
        <div className="mt-2">
          <select
            aria-label="Source field"
            className={inputClassName}
            disabled={readOnly}
            onChange={(event) =>
              onChange({ ...value, field: event.target.value })
            }
            value={value.field}
          >
            {sourceTable.columns.map((column) => (
              <option key={column.name} value={column.name}>
                {column.name}
              </option>
            ))}
          </select>
          <Messages
            messages={
              path ? diagnosticsAtPath(diagnostics, `${path}.field`) : []
            }
          />
        </div>
      ) : null}

      {value?.kind === "metadata" ? (
        <div className="mt-2">
          <select
            aria-label="Metadata field"
            className={inputClassName}
            disabled={readOnly}
            onChange={(event) =>
              onChange({ ...value, name: event.target.value })
            }
            value={value.name}
          >
            {capabilities.transformation.metadataFields.map((name) => (
              <option key={name} value={name}>
                {name}
              </option>
            ))}
          </select>
          <Messages
            messages={
              path ? diagnosticsAtPath(diagnostics, `${path}.name`) : []
            }
          />
        </div>
      ) : null}

      {value?.kind === "literal" ? (
        <LiteralEditor
          allowTypeChange={allowLiteralTypeChange}
          diagnostics={diagnostics}
          onChange={onChange}
          path={path}
          readOnly={readOnly}
          value={value}
        />
      ) : null}

      {value?.kind === "trim" ? (
        <NestedValue
          capabilities={capabilities}
          depth={depth}
          diagnostics={diagnostics}
          expectedType="text"
          label="Text to trim"
          onChange={(next) => next && onChange({ ...value, value: next })}
          path={path}
          programNodeCount={programNodeCount}
          readOnly={readOnly}
          sourceTable={sourceTable}
          suffix=".value"
          value={value.value}
        />
      ) : null}

      {value?.kind === "substring" ? (
        <div className="mt-2 space-y-2">
          <NestedValue
            capabilities={capabilities}
            depth={depth}
            diagnostics={diagnostics}
            expectedType="text"
            label="Text value"
            onChange={(next) => next && onChange({ ...value, value: next })}
            path={path}
            programNodeCount={programNodeCount}
            readOnly={readOnly}
            sourceTable={sourceTable}
            suffix=".value"
            value={value.value}
          />
          <div className="grid gap-2 sm:grid-cols-2">
            <NumberField
              label="Start (1-based)"
              messages={
                path ? diagnosticsAtPath(diagnostics, `${path}.start`) : []
              }
              min={1}
              onChange={(start) => onChange({ ...value, start })}
              readOnly={readOnly}
              value={value.start}
            />
            <NumberField
              label="Length"
              messages={
                path ? diagnosticsAtPath(diagnostics, `${path}.length`) : []
              }
              min={0}
              onChange={(length) => onChange({ ...value, length })}
              readOnly={readOnly}
              value={value.length}
            />
          </div>
        </div>
      ) : null}

      {value?.kind === "convert" ? (
        <div className="mt-2 space-y-2">
          <NestedValue
            capabilities={capabilities}
            depth={depth}
            diagnostics={diagnostics}
            expectedType="text"
            label="Text value"
            onChange={(next) => next && onChange({ ...value, value: next })}
            path={path}
            programNodeCount={programNodeCount}
            readOnly={readOnly}
            sourceTable={sourceTable}
            suffix=".value"
            value={value.value}
          />
          <div className="grid gap-2 sm:grid-cols-2">
            <label className="text-xs font-semibold text-slate-600">
              Convert to
              <select
                className={`${inputClassName} mt-1`}
                disabled={readOnly}
                onChange={(event) =>
                  onChange({
                    ...value,
                    type: event.target.value as ExpressionValueType,
                    inputFormat: undefined,
                  })
                }
                value={value.type}
              >
                {capabilities.transformation.targetConversionTypes.map(
                  (conversionType) => (
                    <option key={conversionType} value={conversionType}>
                      {conversionType}
                    </option>
                  ),
                )}
              </select>
            </label>
            {value.type === "date" || value.type === "dateTime" ? (
              <label className="text-xs font-semibold text-slate-600">
                Input format
                <select
                  className={`${inputClassName} mt-1`}
                  disabled={readOnly}
                  onChange={(event) =>
                    onChange({
                      ...value,
                      inputFormat: event.target.value || undefined,
                    })
                  }
                  value={value.inputFormat ?? ""}
                >
                  <option value="">Default format</option>
                  {(value.type === "date"
                    ? capabilities.transformation.inputFormats.date
                    : capabilities.transformation.inputFormats.dateTime
                  ).map((format) => (
                    <option key={format} value={format}>
                      {format}
                    </option>
                  ))}
                </select>
              </label>
            ) : null}
          </div>
          <Messages
            messages={
              path
                ? [
                    ...diagnosticsAtPath(diagnostics, `${path}.type`),
                    ...diagnosticsAtPath(diagnostics, `${path}.inputFormat`),
                  ]
                : []
            }
          />
        </div>
      ) : null}

      {value?.kind === "coalesce" ? (
        <CollectionValues
          capabilities={capabilities}
          depth={depth}
          diagnostics={diagnostics}
          expectedType={type}
          label="Candidate"
          onChange={(values) => onChange({ ...value, values })}
          path={path}
          programNodeCount={programNodeCount}
          readOnly={readOnly}
          sourceTable={sourceTable}
          suffix=".values"
          values={value.values}
        />
      ) : null}

      {value?.kind === "defaultIfBlank" ? (
        <div className="mt-2 space-y-2">
          <NestedValue
            capabilities={capabilities}
            depth={depth}
            diagnostics={diagnostics}
            expectedType="text"
            label="Text value"
            onChange={(next) => next && onChange({ ...value, value: next })}
            path={path}
            programNodeCount={programNodeCount}
            readOnly={readOnly}
            sourceTable={sourceTable}
            suffix=".value"
            value={value.value}
          />
          <NestedValue
            capabilities={capabilities}
            depth={depth}
            diagnostics={diagnostics}
            expectedType="text"
            label="Fallback"
            onChange={(next) => next && onChange({ ...value, fallback: next })}
            path={path}
            programNodeCount={programNodeCount}
            readOnly={readOnly}
            sourceTable={sourceTable}
            suffix=".fallback"
            value={value.fallback}
          />
        </div>
      ) : null}

      {value?.kind === "case" ? (
        <CaseEditor
          capabilities={capabilities}
          depth={depth}
          diagnostics={diagnostics}
          expectedType={type}
          onChange={onChange}
          path={path}
          programNodeCount={programNodeCount}
          readOnly={readOnly}
          sourceTable={sourceTable}
          value={value}
        />
      ) : null}

      {value?.kind === "currentDate" ? (
        <p className="mt-2 text-xs text-slate-500">
          Typed Date from the evaluation instant in Asia/Bangkok.
        </p>
      ) : null}

      {value?.kind === "call" ? (
        <div className="mt-2 space-y-2 rounded-lg border border-indigo-100 bg-indigo-50/40 p-3">
          <label className="text-xs font-semibold text-slate-600">
            Logical transform
            <select
              aria-label="Named transform"
              className={`${inputClassName} mt-1`}
              disabled={readOnly}
              onChange={(event) =>
                onChange({
                  ...value,
                  function: event.target.value,
                  arguments: [defaultValue("text", sourceTable)],
                })
              }
              value={value.function}
            >
              {!capabilities.transformation.functions.includes(
                value.function,
              ) ? (
                <option value={value.function}>
                  {value.function} (unavailable)
                </option>
              ) : null}
              {capabilities.transformation.functions.map((functionName) => (
                <option key={functionName} value={functionName}>
                  {functionName}
                </option>
              ))}
            </select>
          </label>
          <Messages
            messages={
              path ? diagnosticsAtPath(diagnostics, `${path}.function`) : []
            }
          />
          {value.arguments.map((argument, index) => (
            <div className="mt-2" key={index}>
              <p className="mb-1 text-xs font-semibold text-slate-600">
                Argument {index + 1} · Text
              </p>
              <ValueExpressionEditor
                allowNone={false}
                capabilities={capabilities}
                depth={depth + 1}
                diagnostics={diagnostics}
                expectedType="text"
                onChange={(next) =>
                  next &&
                  onChange({
                    ...value,
                    arguments: value.arguments.map((item, argumentIndex) =>
                      argumentIndex === index ? next : item,
                    ),
                  })
                }
                path={path ? `${path}.arguments[${index}]` : null}
                programNodeCount={programNodeCount}
                readOnly={readOnly}
                sourceTable={sourceTable}
                value={argument}
              />
            </div>
          ))}
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-xs text-slate-500">
              {value.function === "formatRegNo@1"
                ? "Returns Text and requires exactly one Text argument."
                : "Signature is validated by the API."}
            </p>
            <button
              className="text-xs font-semibold text-indigo-700 disabled:opacity-40"
              disabled={readOnly}
              onClick={() =>
                onChange({
                  ...value,
                  arguments: [defaultValue("text", sourceTable)],
                })
              }
              type="button"
            >
              Reset arguments
            </button>
          </div>
          <Messages
            messages={
              path
                ? diagnosticsAtPath(
                    diagnostics,
                    `${path}.arguments`,
                    true,
                  )
                : []
            }
          />
        </div>
      ) : null}

      <Messages messages={rootMessages} />
    </div>
  );
}

function LiteralEditor({
  value,
  allowTypeChange,
  diagnostics,
  path,
  readOnly,
  onChange,
}: {
  value: Extract<ValueExpression, { kind: "literal" }>;
  allowTypeChange: boolean;
  diagnostics: MappingDraftDiagnostic[];
  path: string | null;
  readOnly: boolean;
  onChange: (value: ValueExpression) => void;
}) {
  return (
    <div className="mt-2 grid gap-2 sm:grid-cols-[10rem_minmax(0,1fr)]">
      <select
        aria-label="Constant type"
        className={inputClassName}
        disabled={readOnly || !allowTypeChange}
        onChange={(event) =>
          onChange(literal(event.target.value as ExpressionValueType))
        }
        value={value.type}
      >
        {expressionTypes.map((item) => (
          <option key={item} value={item}>
            {item}
          </option>
        ))}
      </select>
      {value.type === "boolean" ? (
        <select
          aria-label="Constant value"
          className={inputClassName}
          disabled={readOnly}
          onChange={(event) =>
            onChange({ ...value, value: event.target.value === "true" })
          }
          value={String(value.value)}
        >
          <option value="false">false</option>
          <option value="true">true</option>
        </select>
      ) : (
        <input
          aria-label="Constant value"
          className={inputClassName}
          disabled={readOnly}
          onChange={(event) =>
            onChange({
              ...value,
              value:
                value.type === "decimal" || value.type === "integer"
                  ? parseNumericLiteral(
                      event.target.value,
                      value.type === "integer",
                    )
                  : event.target.value,
            })
          }
          placeholder={
            value.type === "dateTime"
              ? "2026-10-14T10:00:00+07:00"
              : undefined
          }
          step={value.type === "integer" ? 1 : "any"}
          type={
            value.type === "decimal" || value.type === "integer"
              ? "number"
              : value.type === "date"
                ? "date"
                : "text"
          }
          value={value.value === null ? "" : String(value.value)}
        />
      )}
      <Messages
        messages={path ? diagnosticsAtPath(diagnostics, `${path}.value`) : []}
      />
    </div>
  );
}

function NumberField({
  label,
  value,
  min,
  readOnly,
  messages,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  readOnly: boolean;
  messages: MappingDraftDiagnostic[];
  onChange: (value: number) => void;
}) {
  return (
    <label className="text-xs font-semibold text-slate-600">
      {label}
      <input
        className={`${inputClassName} mt-1`}
        disabled={readOnly}
        min={min}
        onChange={(event) =>
          onChange(Math.max(min, Number(event.target.value) || min))
        }
        type="number"
        value={value}
      />
      <Messages messages={messages} />
    </label>
  );
}

interface NestedValueProps extends EditorContextProps {
  value: ValueExpression;
  expectedType: ExpressionValueType;
  label: string;
  suffix: string;
  path: string | null;
  onChange: (value: ValueExpression | null) => void;
}

function NestedValue({
  label,
  suffix,
  path,
  onChange,
  depth = 0,
  ...props
}: NestedValueProps) {
  return (
    <div className="mt-2">
      <p className="mb-1 text-xs font-semibold text-slate-600">{label}</p>
      <ValueExpressionEditor
        {...props}
        allowNone={false}
        depth={depth + 1}
        onChange={onChange}
        path={path ? `${path}${suffix}` : null}
      />
    </div>
  );
}

interface CollectionValuesProps extends EditorContextProps {
  values: ValueExpression[];
  expectedType: ExpressionValueType;
  label: string;
  suffix: string;
  path: string | null;
  onChange: (values: ValueExpression[]) => void;
}

function CollectionValues({
  values,
  expectedType,
  label,
  suffix,
  path,
  onChange,
  sourceTable,
  capabilities,
  diagnostics,
  programNodeCount,
  depth = 0,
  readOnly = false,
}: CollectionValuesProps) {
  return (
    <div className="mt-2 space-y-2">
      {values.map((item, index) => (
        <div className="rounded-lg border border-slate-200 bg-slate-50 p-2" key={index}>
          <div className="mb-1 flex items-center justify-between gap-2">
            <span className="text-xs font-semibold text-slate-600">
              {label} {index + 1}
            </span>
            <button
              className="text-xs font-semibold text-rose-700 disabled:opacity-40"
              disabled={readOnly || values.length === 1}
              onClick={() =>
                onChange(values.filter((_, itemIndex) => itemIndex !== index))
              }
              type="button"
            >
              Remove
            </button>
          </div>
          <ValueExpressionEditor
            allowNone={false}
            capabilities={capabilities}
            depth={depth + 1}
            diagnostics={diagnostics}
            expectedType={expectedType}
            onChange={(next) =>
              next &&
              onChange(
                values.map((current, itemIndex) =>
                  itemIndex === index ? next : current,
                ),
              )
            }
            path={path ? `${path}${suffix}[${index}]` : null}
            programNodeCount={programNodeCount}
            readOnly={readOnly}
            sourceTable={sourceTable}
            value={item}
          />
        </div>
      ))}
      <button
        className="text-xs font-semibold text-indigo-700 disabled:opacity-40"
        disabled={
          readOnly ||
          values.length >= capabilities.transformation.limits.maxCollectionItems ||
          !canNest(depth, programNodeCount, capabilities)
        }
        onClick={() =>
          onChange([...values, defaultValue(expectedType, sourceTable)])
        }
        type="button"
      >
        Add {label.toLowerCase()}
      </button>
      <Messages
        messages={
          path ? diagnosticsAtPath(diagnostics, `${path}${suffix}`) : []
        }
      />
    </div>
  );
}

interface CaseEditorProps extends EditorContextProps {
  value: Extract<ValueExpression, { kind: "case" }>;
  expectedType: ExpressionValueType;
  path: string | null;
  onChange: (value: ValueExpression) => void;
}

function CaseEditor({
  value,
  expectedType,
  path,
  onChange,
  sourceTable,
  capabilities,
  diagnostics,
  programNodeCount,
  depth = 0,
  readOnly = false,
}: CaseEditorProps) {
  return (
    <div className="mt-2 space-y-3">
      {value.cases.map((arm, index) => (
        <div className="rounded-lg border border-slate-200 bg-slate-50 p-3" key={index}>
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs font-semibold text-slate-600">
              WHEN {index + 1}
            </span>
            <button
              className="text-xs font-semibold text-rose-700 disabled:opacity-40"
              disabled={readOnly || value.cases.length === 1}
              onClick={() =>
                onChange({
                  ...value,
                  cases: value.cases.filter((_, armIndex) => armIndex !== index),
                })
              }
              type="button"
            >
              Remove arm
            </button>
          </div>
          <div className="mt-2">
            <BooleanExpressionEditor
              allowNone={false}
              capabilities={capabilities}
              depth={depth + 1}
              diagnostics={diagnostics}
              onChange={(when) =>
                when &&
                onChange({
                  ...value,
                  cases: value.cases.map((item, armIndex) =>
                    armIndex === index ? { ...item, when } : item,
                  ),
                })
              }
              path={path ? `${path}.cases[${index}].when` : ""}
              programNodeCount={programNodeCount}
              readOnly={readOnly}
              sourceTable={sourceTable}
              value={arm.when}
            />
          </div>
          <div className="mt-2">
            <p className="mb-1 text-xs font-semibold text-slate-600">THEN</p>
            <ValueExpressionEditor
              allowNone={false}
              capabilities={capabilities}
              depth={depth + 1}
              diagnostics={diagnostics}
              expectedType={expectedType}
              onChange={(then) =>
                then &&
                onChange({
                  ...value,
                  cases: value.cases.map((item, armIndex) =>
                    armIndex === index ? { ...item, then } : item,
                  ),
                })
              }
              path={path ? `${path}.cases[${index}].then` : null}
              programNodeCount={programNodeCount}
              readOnly={readOnly}
              sourceTable={sourceTable}
              value={arm.then}
            />
          </div>
        </div>
      ))}
      <button
        className="text-xs font-semibold text-indigo-700 disabled:opacity-40"
        disabled={
          readOnly ||
          value.cases.length >=
            capabilities.transformation.limits.maxCollectionItems ||
          !canNest(depth + 1, programNodeCount, capabilities, 4)
        }
        onClick={() =>
          onChange({
            ...value,
            cases: [
              ...value.cases,
              {
                when: defaultComparison(sourceTable),
                then: defaultValue(expectedType, sourceTable),
              },
            ],
          })
        }
        type="button"
      >
        Add CASE arm
      </button>
      <div>
        <p className="mb-1 text-xs font-semibold text-slate-600">ELSE</p>
        <ValueExpressionEditor
          allowNone={false}
          capabilities={capabilities}
          depth={depth + 1}
          diagnostics={diagnostics}
          expectedType={expectedType}
          onChange={(elseValue) =>
            elseValue && onChange({ ...value, else: elseValue })
          }
          path={path ? `${path}.else` : null}
          programNodeCount={programNodeCount}
          readOnly={readOnly}
          sourceTable={sourceTable}
          value={value.else}
        />
      </div>
      <Messages
        messages={path ? diagnosticsAtPath(diagnostics, `${path}.cases`) : []}
      />
    </div>
  );
}

export function BooleanExpressionEditor({
  value,
  allowNone = true,
  sourceTable,
  capabilities,
  diagnostics,
  programNodeCount,
  path,
  onChange,
  depth = 0,
  readOnly = false,
}: BooleanExpressionEditorProps) {
  const mode = value?.kind ?? "none";
  const currentNodes = value ? booleanExpressionStats(value).nodes : 0;
  const isLookup = value?.kind === "exists" || value?.kind === "notExists";
  const lookupExpression = isLookup ? (value as LookupExpression) : null;

  function canSelectKind(kind: BooleanExpression["kind"]) {
    const shape = booleanKindShape[kind];
    return (
      depth + shape.maxRelativeDepth <
        capabilities.transformation.limits.maxExpressionDepth &&
      programNodeCount - currentNodes + shape.nodes <=
        capabilities.transformation.limits.maxExpressionNodes
    );
  }

  function selectMode(kind: "none" | BooleanExpression["kind"]) {
    if (kind === "none") {
      onChange(null);
      return;
    }
    const comparison = defaultComparison(sourceTable);
    const textValue = defaultValue("text", sourceTable);
    switch (kind) {
      case "eq":
      case "ne":
      case "gt":
      case "gte":
      case "lt":
      case "lte":
        onChange({ kind, left: comparison.left, right: comparison.right });
        break;
      case "isNull":
      case "isBlank":
      case "notBlank":
        onChange({ kind, value: textValue });
        break;
      case "like":
        onChange({ kind, value: textValue, pattern: literal("text") });
        break;
      case "in":
      case "notIn":
        onChange({ kind, value: textValue, values: [literal("text")] });
        break;
      case "and":
      case "or":
        onChange({
          kind,
          operands: [
            defaultComparison(sourceTable),
            defaultComparison(sourceTable),
          ],
        });
        break;
      case "not":
        onChange({ kind, value: defaultComparison(sourceTable) });
        break;
      case "exists":
      case "notExists":
        onChange({
          kind,
          lookup: capabilities.transformation.lookups[0] ?? "",
          arguments: lookupArguments(
            capabilities.transformation.lookups[0] ?? "",
            sourceTable,
          ),
        });
        break;
    }
  }

  const operators = capabilities.transformation.booleanOperators;
  const renderValue = (
    expression: ValueExpression,
    suffix: string,
    update: (next: ValueExpression) => void,
    expected?: ExpressionValueType,
  ) => (
    <ValueExpressionEditor
      allowLiteralTypeChange
      allowNone={false}
      capabilities={capabilities}
      depth={depth + 1}
      diagnostics={diagnostics}
      expectedType={expected}
      onChange={(next) => next && update(next)}
      path={`${path}${suffix}`}
      programNodeCount={programNodeCount}
      readOnly={readOnly}
      sourceTable={sourceTable}
      value={expression}
    />
  );

  return (
    <div className={depth > 0 ? "border-l-2 border-indigo-100 pl-3" : undefined}>
      <select
        aria-label="Condition type"
        className={inputClassName}
        disabled={readOnly}
        onChange={(event) =>
          selectMode(event.target.value as "none" | BooleanExpression["kind"])
        }
        value={mode}
      >
        {allowNone ? <option value="none">No condition</option> : null}
        {operators.map((kind) => (
          <option
            disabled={!canSelectKind(kind)}
            key={kind}
            value={kind}
          >
            {booleanLabels[kind]}
          </option>
        ))}
      </select>

      {value && ["eq", "ne", "gt", "gte", "lt", "lte"].includes(value.kind) ? (
        <ComparisonEditor
          capabilities={capabilities}
          depth={depth}
          diagnostics={diagnostics}
          onChange={onChange}
          path={path}
          programNodeCount={programNodeCount}
          readOnly={readOnly}
          sourceTable={sourceTable}
          value={value as ComparisonExpression}
        />
      ) : null}

      {value && ["isNull", "isBlank", "notBlank"].includes(value.kind) ? (
        <div className="mt-2">
          {renderValue(
            (value as UnaryValueExpression).value,
            ".value",
            (next) => onChange({ ...value, value: next } as BooleanExpression),
            value.kind === "isNull" ? undefined : "text",
          )}
        </div>
      ) : null}

      {value?.kind === "like" ? (
        <div className="mt-2 grid gap-2 lg:grid-cols-2">
          {renderValue(value.value, ".value", (next) => onChange({ ...value, value: next }), "text")}
          {renderValue(value.pattern, ".pattern", (pattern) => onChange({ ...value, pattern }), "text")}
        </div>
      ) : null}

      {value?.kind === "in" || value?.kind === "notIn" ? (
        <div className="mt-2 space-y-2">
          {renderValue(value.value, ".value", (next) => onChange({ ...value, value: next }))}
          <CollectionValues
            capabilities={capabilities}
            depth={depth}
            diagnostics={diagnostics}
            expectedType={expressionValueType(value.value, sourceTable) ?? "text"}
            label="Allowed value"
            onChange={(values) => onChange({ ...value, values })}
            path={path}
            programNodeCount={programNodeCount}
            readOnly={readOnly}
            sourceTable={sourceTable}
            suffix=".values"
            values={value.values}
          />
        </div>
      ) : null}

      {value?.kind === "and" || value?.kind === "or" ? (
        <BooleanCollection
          capabilities={capabilities}
          depth={depth}
          diagnostics={diagnostics}
          onChange={onChange}
          path={path}
          programNodeCount={programNodeCount}
          readOnly={readOnly}
          sourceTable={sourceTable}
          value={value}
        />
      ) : null}

      {value?.kind === "not" ? (
        <div className="mt-2">
          <BooleanExpressionEditor
            allowNone={false}
            capabilities={capabilities}
            depth={depth + 1}
            diagnostics={diagnostics}
            onChange={(next) => next && onChange({ ...value, value: next })}
            path={`${path}.value`}
            programNodeCount={programNodeCount}
            readOnly={readOnly}
            sourceTable={sourceTable}
            value={value.value}
          />
        </div>
      ) : null}

      {lookupExpression ? (
        <div className="mt-2 space-y-2 rounded-lg border border-indigo-100 bg-indigo-50/40 p-3">
          <label className="text-xs font-semibold text-slate-600">
            Logical lookup
            <select
              aria-label="Named lookup"
              className={`${inputClassName} mt-1`}
              disabled={readOnly}
              onChange={(event) =>
                onChange({
                  ...lookupExpression,
                  lookup: event.target.value,
                  arguments: lookupArguments(event.target.value, sourceTable),
                })
              }
              value={lookupExpression.lookup}
            >
              {!capabilities.transformation.lookups.includes(
                lookupExpression.lookup,
              ) ? (
                <option value={lookupExpression.lookup}>
                  {lookupExpression.lookup} (unavailable)
                </option>
              ) : null}
              {capabilities.transformation.lookups.map((lookup) => (
                <option key={lookup} value={lookup}>
                  {lookup}
                </option>
              ))}
            </select>
          </label>
          <Messages
            messages={diagnosticsAtPath(diagnostics, `${path}.lookup`)}
          />
          {Object.entries(lookupExpression.arguments).map(([name, argument]) => (
            <div className="mt-2" key={name}>
              <p className="mb-1 text-xs font-semibold text-slate-600">
                {name} · Text
              </p>
              <ValueExpressionEditor
                allowNone={false}
                capabilities={capabilities}
                depth={depth + 1}
                diagnostics={diagnostics}
                expectedType="text"
                onChange={(next) =>
                  next &&
                  onChange({
                    ...lookupExpression,
                    arguments: {
                      ...lookupExpression.arguments,
                      [name]: next,
                    },
                  })
                }
                path={`${path}.arguments.${name}`}
                programNodeCount={programNodeCount}
                readOnly={readOnly}
                sourceTable={sourceTable}
                value={argument}
              />
            </div>
          ))}
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-xs text-slate-500">
              Live lookup dependency; runtime supplies row identity.
            </p>
            <button
              className="text-xs font-semibold text-indigo-700 disabled:opacity-40"
              disabled={readOnly}
              onClick={() =>
                onChange({
                  ...lookupExpression,
                  arguments: lookupArguments(
                    lookupExpression.lookup,
                    sourceTable,
                  ),
                })
              }
              type="button"
            >
              Reset arguments
            </button>
          </div>
          <Messages
            messages={diagnosticsAtPath(
              diagnostics,
              `${path}.arguments`,
              true,
            )}
          />
        </div>
      ) : null}

      <Messages messages={diagnosticsAtPath(diagnostics, path)} />
    </div>
  );
}

interface ComparisonEditorProps extends EditorContextProps {
  value: ComparisonExpression;
  path: string;
  onChange: (value: BooleanExpression) => void;
}

function ComparisonEditor({
  value,
  path,
  onChange,
  sourceTable,
  capabilities,
  diagnostics,
  programNodeCount,
  depth = 0,
  readOnly = false,
}: ComparisonEditorProps) {
  return (
    <div className="mt-2 grid gap-2 lg:grid-cols-2">
      <ValueExpressionEditor
        allowLiteralTypeChange
        allowNone={false}
        capabilities={capabilities}
        depth={depth + 1}
        diagnostics={diagnostics}
        expectedType={expressionValueType(value.right, sourceTable) ?? undefined}
        onChange={(left) => left && onChange({ ...value, left })}
        path={`${path}.left`}
        programNodeCount={programNodeCount}
        readOnly={readOnly}
        sourceTable={sourceTable}
        value={value.left}
      />
      <ValueExpressionEditor
        allowLiteralTypeChange
        allowNone={false}
        capabilities={capabilities}
        depth={depth + 1}
        diagnostics={diagnostics}
        expectedType={expressionValueType(value.left, sourceTable) ?? undefined}
        onChange={(right) => right && onChange({ ...value, right })}
        path={`${path}.right`}
        programNodeCount={programNodeCount}
        readOnly={readOnly}
        sourceTable={sourceTable}
        value={value.right}
      />
    </div>
  );
}

interface BooleanCollectionProps extends EditorContextProps {
  value: Extract<BooleanExpression, { kind: "and" | "or" }>;
  path: string;
  onChange: (value: BooleanExpression) => void;
}

function BooleanCollection({
  value,
  path,
  onChange,
  sourceTable,
  capabilities,
  diagnostics,
  programNodeCount,
  depth = 0,
  readOnly = false,
}: BooleanCollectionProps) {
  return (
    <div className="mt-2 space-y-2">
      {value.operands.map((operand, index) => (
        <div className="rounded-lg border border-indigo-100 bg-indigo-50/40 p-2" key={index}>
          <div className="mb-1 flex justify-end">
            <button
              className="text-xs font-semibold text-rose-700 disabled:opacity-40"
              disabled={readOnly || value.operands.length === 1}
              onClick={() =>
                onChange({
                  ...value,
                  operands: value.operands.filter(
                    (_, operandIndex) => operandIndex !== index,
                  ),
                })
              }
              type="button"
            >
              Remove condition
            </button>
          </div>
          <BooleanExpressionEditor
            allowNone={false}
            capabilities={capabilities}
            depth={depth + 1}
            diagnostics={diagnostics}
            onChange={(next) =>
              next &&
              onChange({
                ...value,
                operands: value.operands.map((current, operandIndex) =>
                  operandIndex === index ? next : current,
                ),
              })
            }
            path={`${path}.operands[${index}]`}
            programNodeCount={programNodeCount}
            readOnly={readOnly}
            sourceTable={sourceTable}
            value={operand}
          />
        </div>
      ))}
      <button
        className="text-xs font-semibold text-indigo-700 disabled:opacity-40"
        disabled={
          readOnly ||
          value.operands.length >=
            capabilities.transformation.limits.maxCollectionItems ||
          !canNest(depth + 1, programNodeCount, capabilities, 3)
        }
        onClick={() =>
          onChange({
            ...value,
            operands: [...value.operands, defaultComparison(sourceTable)],
          })
        }
        type="button"
      >
        Add condition
      </button>
      <Messages
        messages={diagnosticsAtPath(diagnostics, `${path}.operands`)}
      />
    </div>
  );
}
