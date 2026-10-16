export type ValidationErrors = Record<string, string[]>;

export interface ApiProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: ValidationErrors;
}

export interface HealthResponse {
  status: string;
}

export type TableKind = "Source" | "Normalized";

export type ColumnDataType =
  | "Text"
  | "Date"
  | "Decimal"
  | "Boolean"
  | "Integer"
  | "DateTime"
  | "Guid";

export interface ColumnDefinition {
  name: string;
  dataType: ColumnDataType;
  isRequired: boolean;
  length?: number | null;
  precision?: number | null;
  scale?: number | null;
}

export interface TableDefinition {
  id: number;
  name: string;
  kind: TableKind;
  columns: ColumnDefinition[];
}

export interface CreateTableRequest {
  name: string;
  kind: TableKind;
  columns: ColumnDefinition[];
}

export interface DelimitedInputField {
  inputHeader: string;
  sourceColumn: string;
}

export interface DelimitedInputLayout {
  kind: "delimited";
  encoding: string;
  delimiter: string;
  preambleLines: number;
  headerPolicy: "None" | "SingleRow";
  fields: DelimitedInputField[];
}

export interface FixedWidthDiscriminator {
  start: number;
  length: number;
  value: string;
}

export interface FixedWidthInputField {
  sourceColumn: string;
  start: number;
  length: number;
}

export interface FixedWidthRecordRule {
  key: string;
  discriminator: FixedWidthDiscriminator;
  action: "Import" | "Ignore";
  expectedLength: number;
  fields: FixedWidthInputField[];
}

export interface InputRecordSequenceItem {
  recordRule: string;
  minOccurs: number;
  maxOccurs?: number | null;
}

export interface FixedWidthInputLayout {
  kind: "fixedWidth";
  encoding: string;
  positionUnit: string;
  recordRules: FixedWidthRecordRule[];
  recordSequence: InputRecordSequenceItem[];
  unmatchedRecordPolicy: "Error";
}

export type InputLayout = DelimitedInputLayout | FixedWidthInputLayout;

export type ExpressionValueType =
  | "text"
  | "decimal"
  | "integer"
  | "date"
  | "dateTime"
  | "boolean"
  | "guid";

export type JsonValue =
  | string
  | number
  | boolean
  | null
  | JsonValue[]
  | { [key: string]: JsonValue };

export type ValueExpression =
  | { kind: "source"; field: string }
  | { kind: "metadata"; name: string }
  | { kind: "literal"; type: ExpressionValueType; value: JsonValue }
  | { kind: "coalesce"; values: ValueExpression[] }
  | {
      kind: "defaultIfBlank";
      value: ValueExpression;
      fallback: ValueExpression;
    }
  | { kind: "trim"; value: ValueExpression }
  | { kind: "case"; cases: CaseArm[]; else: ValueExpression }
  | { kind: "substring"; value: ValueExpression; start: number; length: number }
  | {
      kind: "convert";
      value: ValueExpression;
      type: ExpressionValueType;
      inputFormat?: string | null;
    }
  | { kind: "currentDate" }
  | { kind: "call"; function: string; arguments: ValueExpression[] };

export interface CaseArm {
  when: BooleanExpression;
  then: ValueExpression;
}

type BinaryBooleanKind = "eq" | "ne" | "gt" | "gte" | "lt" | "lte";

type BinaryBooleanExpression = {
  [Kind in BinaryBooleanKind]: {
    kind: Kind;
    left: ValueExpression;
    right: ValueExpression;
  };
}[BinaryBooleanKind];

type LookupBooleanExpression = {
  kind: "exists" | "notExists";
  lookup: string;
  arguments: Record<string, ValueExpression>;
};

export type BooleanExpression =
  | BinaryBooleanExpression
  | { kind: "isNull" | "isBlank" | "notBlank"; value: ValueExpression }
  | { kind: "like"; value: ValueExpression; pattern: ValueExpression }
  | { kind: "in" | "notIn"; value: ValueExpression; values: ValueExpression[] }
  | { kind: "and" | "or"; operands: BooleanExpression[] }
  | { kind: "not"; value: BooleanExpression }
  | LookupBooleanExpression;

export interface FieldAssignment {
  normalizedColumn: string;
  inputFormat?: string | null;
  value: ValueExpression;
}

export interface TransformationOutput {
  outputKey: string;
  emitWhen?: BooleanExpression | null;
  fields: FieldAssignment[];
}

export interface TransformationProgram {
  languageVersion?: number | null;
  semanticsProfile: string;
  sourceWhen?: BooleanExpression | null;
  outputs: TransformationOutput[];
}

export interface MappingConfig {
  id: number;
  name: string;
  inputFolder: string;
  sourceTableId: number;
  normalizedTableId: number;
  activeVersionId: number | null;
}

export interface MappingConfigVersion {
  id: number;
  configId: number;
  versionNo: number;
  inputLayout: InputLayout;
  transformationProgram: TransformationProgram;
}

export interface MappingConfigVersionDetails {
  id: number;
  versionNo: number;
  isActive: boolean;
  inputLayout: InputLayout;
  transformationProgram: TransformationProgram;
}

export interface MappingConfigDetails extends MappingConfig {
  versions: MappingConfigVersionDetails[];
}

export interface CreateMappingConfigRequest {
  name: string;
  inputFolder: string;
  sourceTableId: number;
  normalizedTableId: number;
}

export interface MappingConfigVersionDraft {
  inputLayout: InputLayout;
  transformationProgram: TransformationProgram;
}

export interface MappingCapabilities {
  input: {
    formats: Array<InputLayout["kind"]>;
    encodings: string[];
    positionUnits: string[];
  };
  transformation: {
    languageVersions: number[];
    semanticsProfiles: Array<{
      id: string;
      immutable: boolean;
      summary: string;
    }>;
    targetConversionTypes: ExpressionValueType[];
    inputFormats: {
      date: string[];
      dateTime: string[];
    };
    valueExpressions: Array<ValueExpression["kind"]>;
    booleanOperators: Array<BooleanExpression["kind"]>;
    functions: string[];
    lookups: string[];
    metadataFields: string[];
    limits: {
      maxOutputs: number;
      maxFieldsPerOutput: number;
      maxCollectionItems: number;
      maxExpressionDepth: number;
      maxExpressionNodes: number;
    };
  };
}

export type MappingDraftDiagnosticSeverity = "Error" | "Warning";

export interface MappingDraftDiagnostic {
  path: string;
  code: string;
  message: string;
  severity: MappingDraftDiagnosticSeverity;
}

export interface ValidateMappingConfigVersionResponse {
  isValid: boolean;
  diagnostics: MappingDraftDiagnostic[];
}

export interface TextFilePreviewSample {
  kind: "textFile";
  fileName: string;
  content: string;
}

export interface SourceRowsPreviewSample {
  kind: "sourceRows";
  rows: PreviewSourceRow[];
}

export interface PreviewSourceRow {
  values: Record<string, string | null>;
  rowKey?: string | null;
  fileName?: string | null;
  rowNumber?: number | null;
  physicalLineNumber?: number | null;
  sourceRowId?: number | null;
}

export interface PreviewMappingConfigVersionRequest
  extends MappingConfigVersionDraft {
  evaluationAt: string;
  sample: TextFilePreviewSample | SourceRowsPreviewSample;
}

export type TransformationRowStatus = "Done" | "Filtered" | "Invalid";

export interface TransformationOutputResult {
  outputKey: string;
  values: Record<string, JsonValue>;
}

export interface TransformationRowError {
  outputKey: string | null;
  field: string | null;
  expressionPath: string;
  reason: string;
}

export interface TransformationRowResult {
  rowKey: string;
  status: TransformationRowStatus;
  outputs: TransformationOutputResult[];
  errors: TransformationRowError[];
}

export interface MappingPreviewRecord {
  rowKey: string;
  rawRecord: string | null;
  rowNumber: number | null;
  physicalLineNumber: number | null;
  action: "Import" | "Ignore";
  recordRule: string | null;
  sourceValues: Record<string, string | null>;
  transformation: TransformationRowResult | null;
}

export interface PreviewMappingConfigVersionResponse {
  evaluationAt: string;
  records: MappingPreviewRecord[];
}
