import type {
  DelimitedInputLayout,
  ExcelInputLayout,
  FixedWidthInputLayout,
  InputLayout,
  MappingCapabilities,
  TableDefinition,
} from "@/lib/api/contracts";

function firstOrFallback(values: string[], fallback: string) {
  return values[0] ?? fallback;
}

export function defaultDelimitedLayout(
  sourceTable: TableDefinition,
  capabilities: MappingCapabilities,
): DelimitedInputLayout {
  return {
    kind: "delimited",
    encoding: firstOrFallback(capabilities.input.encodings, "utf-8"),
    delimiter: ",",
    preambleLines: 0,
    headerPolicy: "SingleRow",
    fields: sourceTable.columns.map((column) => ({
      inputHeader: column.name,
      sourceColumn: column.name,
    })),
  };
}

export function defaultFixedWidthLayout(
  sourceTable: TableDefinition,
  capabilities: MappingCapabilities,
): FixedWidthInputLayout {
  const fields = sourceTable.columns.map((column, index) => ({
    sourceColumn: column.name,
    start: index * 10 + 2,
    length: 10,
  }));

  return {
    kind: "fixedWidth",
    encoding: firstOrFallback(capabilities.input.encodings, "utf-8"),
    positionUnit: firstOrFallback(
      capabilities.input.positionUnits,
      "utf16CodeUnit",
    ),
    unmatchedRecordPolicy: "Error",
    recordRules: [
      {
        key: "detail",
        discriminator: { start: 1, length: 1, value: "D" },
        action: "Import",
        expectedLength: Math.max(1, fields.length * 10 + 1),
        fields,
      },
    ],
    recordSequence: [
      { recordRule: "detail", minOccurs: 1, maxOccurs: null },
    ],
  };
}

export function defaultExcelLayout(
  sourceTable: TableDefinition,
): ExcelInputLayout {
  return {
    kind: "excel",
    workbookKind: "AutoDetect",
    sheet: "Sheet1",
    headerRow: 1,
    dataStartRow: 2,
    fields: sourceTable.columns.map((column) => ({
      inputHeader: column.name,
      sourceColumn: column.name,
    })),
    blankRowPolicy: "Skip",
  };
}

export function acceptedExtensionsForLayout(layout: InputLayout): string[] {
  if (layout.kind === "delimited") return [".txt", ".csv"];
  if (layout.kind === "fixedWidth") return [".txt"];
  if (layout.workbookKind === "Xlsx") return [".xlsx"];
  if (layout.workbookKind === "Xls") return [".xls"];
  return [".xlsx", ".xls"];
}
