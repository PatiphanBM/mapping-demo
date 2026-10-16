import type {
  BooleanExpression,
  ColumnDataType,
  ExpressionValueType,
  JsonValue,
  MappingCapabilities,
  MappingDraftDiagnostic,
  TableDefinition,
  TransformationProgram,
  ValueExpression,
} from "./api/contracts";

export function expressionTypeForColumn(
  dataType: ColumnDataType,
): ExpressionValueType {
  return (dataType.charAt(0).toLowerCase() +
    dataType.slice(1)) as ExpressionValueType;
}

export function defaultLiteralValue(dataType: ColumnDataType): JsonValue {
  switch (dataType) {
    case "Text":
      return "";
    case "Decimal":
    case "Integer":
      return 0;
    case "Date":
      return "2026-01-01";
    case "DateTime":
      return "2026-01-01T00:00:00Z";
    case "Boolean":
      return false;
    case "Guid":
      return "00000000-0000-0000-0000-000000000000";
  }
}

export function createBasicTransformationProgram(
  sourceTable: TableDefinition,
  normalizedTable: TableDefinition,
  capabilities: MappingCapabilities,
): TransformationProgram {
  const sourceNames = new Map(
    sourceTable.columns.map((column) => [column.name.toLowerCase(), column.name]),
  );

  return {
    languageVersion: capabilities.transformation.languageVersions[0] ?? 1,
    semanticsProfile:
      capabilities.transformation.semanticsProfiles[0]?.id ??
      "sqlserver-legacy-v1",
    outputs: [
      {
        outputKey: "main",
        fields: normalizedTable.columns.flatMap((column) => {
          const sourceName = sourceNames.get(column.name.toLowerCase());
          return sourceName
            ? [
                {
                  normalizedColumn: column.name,
                  value: { kind: "source", field: sourceName } as const,
                },
              ]
            : [];
        }),
      },
    ],
  };
}

export function assignmentPathByColumn(
  program: TransformationProgram,
  outputIndex = 0,
): Map<string, string> {
  return new Map(
    (program.outputs[outputIndex]?.fields ?? []).map((field, fieldIndex) => [
      field.normalizedColumn.toLowerCase(),
      `transformationProgram.outputs[${outputIndex}].fields[${fieldIndex}]`,
    ]),
  );
}

export function diagnosticsAtPath(
  diagnostics: MappingDraftDiagnostic[],
  path: string,
  includeDescendants = false,
): MappingDraftDiagnostic[] {
  return diagnostics.filter(
    (diagnostic) =>
      diagnostic.path === path ||
      (includeDescendants &&
        (diagnostic.path.startsWith(`${path}.`) ||
          diagnostic.path.startsWith(`${path}[`))),
  );
}

export function diagnosticForMissingTarget(
  diagnostics: MappingDraftDiagnostic[],
  fieldsPath: string,
  targetColumn: string,
): MappingDraftDiagnostic[] {
  return diagnostics.filter(
    (diagnostic) =>
      diagnostic.path === fieldsPath &&
      diagnostic.code === "RequiredTargetMissing" &&
      diagnostic.message.includes(`'${targetColumn}'`),
  );
}

export function expressionValueType(
  expression: ValueExpression | null | undefined,
  sourceTable: TableDefinition,
): ExpressionValueType | null {
  if (!expression) return null;

  switch (expression.kind) {
    case "source": {
      const column = sourceTable.columns.find(
        (item) => item.name.toLowerCase() === expression.field.toLowerCase(),
      );
      return column ? expressionTypeForColumn(column.dataType) : null;
    }
    case "metadata":
      if (expression.name === "rowNumber" || expression.name === "physicalLineNumber") {
        return "integer";
      }
      return expression.name === "evaluationTime" ? "dateTime" : "text";
    case "literal":
    case "convert":
      return expression.type;
    case "trim":
    case "substring":
    case "call":
      return "text";
    case "currentDate":
      return "date";
    case "coalesce":
      return expressionValueType(expression.values[0], sourceTable);
    case "defaultIfBlank":
      return expressionValueType(expression.value, sourceTable);
    case "case":
      return expressionValueType(
        expression.cases[0]?.then ?? expression.else,
        sourceTable,
      );
  }
}

export interface ExpressionStats {
  nodes: number;
  maxDepth: number;
}

export function valueExpressionStats(value: ValueExpression): ExpressionStats {
  return valueStats(value, 0);
}

export function booleanExpressionStats(
  expression: BooleanExpression,
): ExpressionStats {
  return booleanStats(expression, 0);
}

function combineStats(rootDepth: number, children: ExpressionStats[]): ExpressionStats {
  return {
    nodes: 1 + children.reduce((total, child) => total + child.nodes, 0),
    maxDepth: Math.max(rootDepth, ...children.map((child) => child.maxDepth)),
  };
}

function valueStats(value: ValueExpression, depth: number): ExpressionStats {
  switch (value.kind) {
    case "coalesce":
      return combineStats(
        depth,
        value.values.map((item) => valueStats(item, depth + 1)),
      );
    case "defaultIfBlank":
      return combineStats(depth, [
        valueStats(value.value, depth + 1),
        valueStats(value.fallback, depth + 1),
      ]);
    case "trim":
    case "substring":
    case "convert":
      return combineStats(depth, [valueStats(value.value, depth + 1)]);
    case "case":
      return combineStats(depth, [
        ...value.cases.flatMap((arm) => [
          booleanStats(arm.when, depth + 1),
          valueStats(arm.then, depth + 1),
        ]),
        valueStats(value.else, depth + 1),
      ]);
    case "call":
      return combineStats(
        depth,
        value.arguments.map((item) => valueStats(item, depth + 1)),
      );
    default:
      return { nodes: 1, maxDepth: depth };
  }
}

function booleanStats(
  expression: BooleanExpression,
  depth: number,
): ExpressionStats {
  switch (expression.kind) {
    case "eq":
    case "ne":
    case "gt":
    case "gte":
    case "lt":
    case "lte":
      return combineStats(depth, [
        valueStats(expression.left, depth + 1),
        valueStats(expression.right, depth + 1),
      ]);
    case "isNull":
    case "isBlank":
    case "notBlank":
      return combineStats(depth, [valueStats(expression.value, depth + 1)]);
    case "like":
      return combineStats(depth, [
        valueStats(expression.value, depth + 1),
        valueStats(expression.pattern, depth + 1),
      ]);
    case "in":
    case "notIn":
      return combineStats(depth, [
        valueStats(expression.value, depth + 1),
        ...expression.values.map((item) => valueStats(item, depth + 1)),
      ]);
    case "and":
    case "or":
      return combineStats(
        depth,
        expression.operands.map((item) => booleanStats(item, depth + 1)),
      );
    case "not":
      return combineStats(depth, [booleanStats(expression.value, depth + 1)]);
    case "exists":
    case "notExists":
      return combineStats(
        depth,
        Object.values(expression.arguments).map((item) =>
          valueStats(item, depth + 1),
        ),
      );
  }
}

export function transformationExpressionStats(
  program: TransformationProgram,
): ExpressionStats {
  const roots: ExpressionStats[] = [];
  if (program.sourceWhen) roots.push(booleanStats(program.sourceWhen, 0));
  for (const output of program.outputs) {
    if (output.emitWhen) roots.push(booleanStats(output.emitWhen, 0));
    roots.push(...output.fields.map((field) => valueStats(field.value, 0)));
  }

  return {
    nodes: roots.reduce((total, item) => total + item.nodes, 0),
    maxDepth: roots.length === 0
      ? 0
      : Math.max(...roots.map((item) => item.maxDepth)),
  };
}

export interface TransformationDependencies {
  functions: string[];
  lookups: string[];
}

export function transformationDependencies(
  program: TransformationProgram,
): TransformationDependencies {
  const functions = new Set<string>();
  const lookups = new Set<string>();

  function visit(node: unknown) {
    if (!node || typeof node !== "object") return;
    if (Array.isArray(node)) {
      node.forEach(visit);
      return;
    }

    const record = node as Record<string, unknown>;
    if (record.kind === "call" && typeof record.function === "string") {
      functions.add(record.function);
    }
    if (
      (record.kind === "exists" || record.kind === "notExists") &&
      typeof record.lookup === "string"
    ) {
      lookups.add(record.lookup);
    }
    Object.values(record).forEach(visit);
  }

  visit(program);
  return { functions: [...functions], lookups: [...lookups] };
}
