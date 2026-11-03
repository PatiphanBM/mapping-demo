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
  createdAt?: string | null;
  verificationStatus?: TableVerificationStatus;
  verificationCheckedAt?: string | null;
}

export interface CreateTableRequest {
  name: string;
  kind: TableKind;
  columns: ColumnDefinition[];
}

export type TableVerificationStatus =
  | "Unknown"
  | "Verifying"
  | "Healthy"
  | "Drifted"
  | "Missing"
  | "VerificationFailed";

export interface TableSchemaColumn {
  name: string;
  dataType: string;
  isNullable: boolean;
  isIdentity: boolean;
}

export interface TableSchemaConstraint {
  kind: string;
  columns: string[];
}

export interface TableSchemaSnapshot {
  columns: TableSchemaColumn[];
  primaryKey: TableSchemaConstraint;
  uniqueConstraints: TableSchemaConstraint[];
}

export interface TableSchemaDiagnostic {
  field: string;
  code: string;
  message: string;
  expected?: string | null;
  actual?: string | null;
}

export interface TableSchemaVerification {
  status: TableVerificationStatus;
  expected: TableSchemaSnapshot;
  actual: TableSchemaSnapshot | null;
  diagnostics: TableSchemaDiagnostic[];
  checkedAt: string;
  durationMilliseconds: number;
}

export interface TableReference {
  configId: number;
  configName: string;
  usage: "Source" | "Normalized";
  configVersionId: number | null;
  versionNo: number | null;
  isActiveVersion: boolean;
  pinnedJobCount: number;
}

export interface TableAdminCapabilities {
  canRead: boolean;
  canCreate: boolean;
  canAddColumn: boolean;
  mutationsEnabled: boolean;
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

export interface ExcelInputField {
  inputHeader: string;
  sourceColumn: string;
}

export interface ExcelInputLayout {
  kind: "excel";
  workbookKind: "AutoDetect" | "Xlsx" | "Xls";
  sheet: string;
  headerRow: number;
  dataStartRow: number;
  fields: ExcelInputField[];
  blankRowPolicy: "Skip" | "Error";
}

export type InputLayout =
  | DelimitedInputLayout
  | FixedWidthInputLayout
  | ExcelInputLayout;

export type ExpressionValueType =
  | "text"
  | "decimal"
  | "integer"
  | "date"
  | "time"
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
  | { kind: "add"; values: ValueExpression[] }
  | { kind: "concat"; values: ValueExpression[]; separator: string }
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
  normalizedColumn?: string | null;
  targetPath?: string | null;
  inputFormat?: string | null;
  value: ValueExpression;
}

export interface TransformationOutput {
  outputKey: string;
  emitWhen?: BooleanExpression | null;
  fields: FieldAssignment[];
  arrayGroups?: ArrayGroup[] | null;
}

export interface ArrayGroup {
  targetPath: string;
  sequencePath: string;
  emptyItemPolicy: "SkipWhenAllBusinessFieldsEmpty" | "Retain";
  sourceSlots: ArraySourceSlot[];
}

export interface ArraySourceSlot {
  sourceSlot: number;
  fields: ArrayItemFieldAssignment[];
}

export interface ArrayItemFieldAssignment {
  targetPath: string;
  inputFormat?: string | null;
  value: ValueExpression;
}

export interface TransformationProgram {
  languageVersion?: number | null;
  semanticsProfile: string;
  sourceWhen?: BooleanExpression | null;
  outputs: TransformationOutput[];
}

export interface ContractReference {
  id: string;
  version: string;
  reference: string;
  schemaHash: string;
}

export type RuntimeValueSource =
  | "ContractReference"
  | "FileJobId"
  | "FileName"
  | "SourceRowId"
  | "SourceRowNumber"
  | "ConfigVersionReference"
  | "OutputKey";

export interface PayloadBinding {
  targetPath: string;
  source: RuntimeValueSource;
}

export interface SupportedContract {
  id: string;
  version: string;
  reference: string;
}

export interface FileDestinationProfileSummary {
  id: string;
  enabled: boolean;
  supportedContracts: SupportedContract[];
  payloadBindings: PayloadBinding[];
}

export interface ContractSchemaResolveResponse {
  contract: ContractReference;
  schemaHash: string;
  fetchedAt: string;
  paths: ContractPathDescriptor[];
}

export interface ApiDestinationProfile {
  id: string;
  displayName: string;
  enabled: boolean;
  method: string;
  deliveryPathTemplate: string;
  environment: "Stub" | "Sandbox" | "Production";
  allowedHeaderNames: string[];
  supportedContracts: SupportedContract[];
  payloadBindings: PayloadBinding[];
}

export type ApiProfileLifecycle = "Draft" | "Active" | "Disabled";

export interface ApiDestinationProfileVersionAdmin {
  profileVersionId: number;
  versionNo: number;
  lifecycle: ApiProfileLifecycle;
  environment: "Stub" | "Sandbox" | "Production";
  schemaBaseUrl: string | null;
  schemaPathTemplate: string;
  deliveryBaseUrl: string | null;
  deliveryPathTemplate: string;
  method: "POST" | "PUT" | "PATCH";
  authScheme: "None" | "Mtls" | "OAuth2ClientCredentials";
  authSecretReference: string | null;
  networkPolicyReference: string;
  timeoutSeconds: number;
  idempotencyHeaderName: string;
  correlationHeaderName: string;
  allowedHeaderNames: string[];
  supportedContracts: SupportedContract[];
  payloadBindings: PayloadBinding[];
  contentHash: string;
  concurrencyToken: number;
  validatedAt: string | null;
}

export interface ApiDestinationProfileAdmin {
  profileKey: string;
  displayName: string;
  concurrencyToken: number;
  activeVersionNo: number | null;
  versions: ApiDestinationProfileVersionAdmin[];
}

export interface ApiDestinationProfileDraft {
  environment: "Stub" | "Sandbox" | "Production";
  schemaBaseUrl: string;
  schemaPathTemplate: string;
  deliveryBaseUrl: string;
  deliveryPathTemplate: string;
  method: "POST" | "PUT" | "PATCH";
  authScheme: "None" | "Mtls" | "OAuth2ClientCredentials";
  authSecretReference: string | null;
  networkPolicyReference: string;
  timeoutSeconds: number;
  idempotencyHeaderName: string;
  correlationHeaderName: string;
  allowedHeaderNames: string[];
  supportedContracts: SupportedContract[];
  payloadBindings: PayloadBinding[];
}

export interface ApiDestinationProfileValidation {
  isValid: boolean;
  contentHash: string;
  validatedAt: string;
  secretStatus: "Present" | "Missing" | "InvalidShape" | "Unchecked";
  diagnostics: Array<{ path: string; code: string; message: string }>;
}

export interface ApiDestinationProfileAdminCapabilities {
  canReadEndpoints: boolean;
  canWriteDraft: boolean;
  canActivate: boolean;
  canActivateProduction: boolean;
  canDisable: boolean;
}

export interface ContractPathDescriptor {
  path: string;
  kind: "Scalar" | "Object" | "Array";
  isAssignable: boolean;
  isRequired: boolean;
  isNullable: boolean;
  scalarType: "Text" | "Integer" | "Number" | "Boolean" | "Date" | "Time" | "DateTime" | "Guid" | null;
  constraints?: {
    length?: number | null;
    precision?: number | null;
    scale?: number | null;
    minimum?: number | null;
    maximum?: number | null;
    pattern?: string | null;
  } | null;
  description?: string | null;
}

export type ContractVersionLifecycle = "Draft" | "Published" | "Retired";
export type ContractCompatibility = "Initial" | "NonBreaking" | "Breaking";

export interface ContractVersionSummary {
  contractVersionId: number;
  contractKey: string;
  displayName: string;
  version: string;
  reference: string;
  lifecycle: ContractVersionLifecycle;
  schemaHash: string | null;
  concurrencyToken: number;
}

export interface ContractVersionDetails extends ContractVersionSummary {
  contractId: number;
  draftSchema: string;
  canonicalSchema: string | null;
  compatibility: ContractCompatibility | null;
  compatibilityBaseVersionId: number | null;
  createdAt: string;
  publishedAt: string | null;
  retiredAt: string | null;
}

export interface ContractAggregate {
  contractId: number;
  contractKey: string;
  displayName: string;
  concurrencyToken: number;
  versions: ContractVersionDetails[];
}

export interface ContractDiagnostic {
  path: string;
  code: string;
  message: string;
}

export interface ContractValidation {
  isValid: boolean;
  canonicalSchema: string | null;
  schemaHash: string | null;
  paths: ContractPathDescriptor[];
  compatibility: ContractCompatibility | null;
  compatibilityBaseVersionId: number | null;
  diagnostics: ContractDiagnostic[];
}

export interface ContractAdminCapabilities {
  canWriteDraft: boolean;
  canPublish: boolean;
}

export interface ContractResolveResponse extends ContractSchemaResolveResponse {
  requestSummary: {
    method: string;
    pathTemplate: string;
    allowedHeaderNames: string[];
  };
}

export interface ContractSnapshot {
  canonicalSchema: string;
  schemaHash: string;
  fetchedAt: string;
  paths: ContractPathDescriptor[];
  payloadBindings: PayloadBinding[];
}

export type DestinationDefinition =
  | { kind: "legacyTable"; normalizedTableId: number }
  | {
      kind: "apiContract";
      apiDestinationProfileId: string;
      contract: ContractReference;
    }
  | {
      kind: "jsonFile";
      fileDestinationProfileId: string;
      contract: ContractReference;
      relativeFolder: string;
      fileNameTemplate: string;
      formatting: "compact" | "indented";
    };

export interface MappingConfig {
  id: number;
  name: string;
  inputFolder: string;
  sourceTableId: number;
  normalizedTableId: number | null;
  activeVersionId: number | null;
  destination: DestinationDefinition | null;
  fileDestinationProfile?: FileDestinationProfileSummary | null;
}

export interface MappingConfigVersion {
  id: number;
  configId: number;
  versionNo: number;
  inputLayout: InputLayout;
  transformationProgram: TransformationProgram;
  destination: DestinationDefinition;
  acceptedExtensions: string[];
  contractSnapshot?: ContractSnapshot | null;
  fileDestinationProfile?: FileDestinationProfileSummary | null;
}

export interface MappingConfigVersionDetails {
  id: number;
  versionNo: number;
  isActive: boolean;
  inputLayout: InputLayout;
  transformationProgram: TransformationProgram;
  destination: DestinationDefinition;
  acceptedExtensions: string[];
  contractSnapshot?: ContractSnapshot | null;
  fileDestinationProfile?: FileDestinationProfileSummary | null;
}

export interface MappingConfigDetails extends MappingConfig {
  versions: MappingConfigVersionDetails[];
}

export interface CreateMappingConfigRequest {
  name: string;
  inputFolder: string;
  sourceTableId: number;
  normalizedTableId: number | null;
  destination: DestinationDefinition;
}

export interface MappingConfigVersionDraft {
  inputLayout: InputLayout;
  transformationProgram: TransformationProgram;
  destination: DestinationDefinition;
  acceptedExtensions?: string[];
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
      time: string[];
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
  destinations: {
    kinds: Array<DestinationDefinition["kind"]>;
    jsonFile: {
      formatting: Array<"compact" | "indented">;
      fileNameTokens: string[];
      maxPreviewBytes: number;
      maxPreviewRecords: number;
      maxObjectBytes: number;
      maxObjectsPerArtifact: number;
      maxArtifactBytes: number;
      profiles: FileDestinationProfileSummary[];
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
  workbook?: {
    sheets: string[];
    selectedSheet: string;
    headers: string[];
  } | null;
  contract?: {
    profileId: string;
    contractReference: string;
    schemaHash: string;
    method: string;
    pathTemplate: string;
    allowedHeaderNames: string[];
    idempotencyKeyExample: string;
    sendsRequest: boolean;
    outputs: Array<{
      rowKey: string;
      outputKey: string;
      payloadJson: string | null;
      payloadHash: string | null;
      diagnostics: Array<{ path: string; code: string; message: string }>;
    }>;
  } | null;
  jsonFile?: {
    profileId: string;
    contractReference: string;
    schemaHash: string;
    formatting: "compact" | "indented";
    relativeDisplayPath: string;
    finalJson: string;
    objectCount: number;
    estimatedUtf8Bytes: number;
    diagnostics: Array<{
      rowKey: string;
      outputKey: string | null;
      path: string;
      code: string;
      message: string;
    }>;
  } | null;
}

export interface FileJob {
  id: number;
  configId: number;
  configVersionId: number;
  fileName: string;
  importStatus: string;
  archiveStatus: string;
  totalRows: number | null;
  normalizationStatus: string;
  pendingRows: number;
  doneRows: number;
  filteredRows: number;
  invalidRows: number;
  failedRows: number;
  lastError: string | null;
  createdAt: string;
  updatedAt: string;
  deliveryStatus: string | null;
  pendingDeliveries: number;
  deliveringDeliveries: number;
  deliveredDeliveries: number;
  retryableFailedDeliveries: number;
  permanentFailedDeliveries: number;
  endToEndStatus: string;
  export?: {
    status: "Pending" | "Finalizing" | "RetryableFailed" | "Blocked" | "Published";
    artifactRevision: number;
    relativeDisplayPath: string;
    objectCount: number | null;
    byteCount: number | null;
    sha256: string | null;
    publishedAt: string | null;
    blockReason: string | null;
  } | null;
}

export interface DeliveryJob {
  id: number;
  fileJobId: number;
  rowJobId: number;
  sourceRowNumber: number;
  configVersionId: number;
  outputKey: string;
  operationKind: "Initial" | "Reprocess" | "Resend";
  destinationProfileId: string;
  contractReference: string;
  schemaHash: string;
  payloadHash: string;
  status: "Pending" | "Delivering" | "Delivered" | "RetryableFailed" | "PermanentFailed";
  attempts: number;
  nextAttemptAt: string | null;
  httpStatus: number | null;
  correlationId: string | null;
  lastErrorRedacted: string | null;
  createdAt: string;
  updatedAt: string;
  deliveredAt: string | null;
  canRetry: boolean;
}

export interface JsonExportHistory {
  id: number;
  fileJobId: number;
  configVersionId: number;
  destinationProfileId: string;
  artifactRevision: number;
  status: "Pending" | "Finalizing" | "RetryableFailed" | "Blocked" | "Published";
  relativeDisplayPath: string;
  objectCount: number | null;
  byteCount: number | null;
  sha256: string | null;
  attemptCount: number;
  lastErrorRedacted: string | null;
  createdAt: string;
  updatedAt: string;
  publishedAt: string | null;
  canRetry: boolean;
}

export interface DeliveryOperations {
  control: {
    deliveryPaused: boolean;
    apiContractActivationEnabled: boolean;
    jsonFileActivationEnabled: boolean;
    jsonFileFinalizerPaused: boolean;
    updatedAt: string;
    updatedBy: string;
    reason: string;
  };
  rollouts: Array<{
    configId: number;
    configName: string;
    stage: "Shadow" | "Canary" | "Expanded";
    enabled: boolean;
    approvedBy: string;
    reason: string;
    updatedAt: string;
  }>;
  groups: Array<{
    configVersionId: number;
    contractReference: string;
    total: number;
    pending: number;
    delivering: number;
    delivered: number;
    retryableFailed: number;
    permanentFailed: number;
  }>;
  successRate: number;
  retryableFailureRate: number;
  permanentFailureRate: number;
  p50LatencyMilliseconds: number;
  p95LatencyMilliseconds: number;
  p99LatencyMilliseconds: number;
  oldestQueueAgeSeconds: number;
  retryCount: number;
  duplicateConflictCount: number;
  schemaValidationFailureCount: number;
  p95SourceToDeliveredSeconds: number | null;
  alerts: Array<{ severity: "Critical" | "Warning"; code: string; message: string }>;
  jsonExports: {
    groups: Array<{
      configId: number;
      configVersionId: number;
      destinationProfileId: string;
      total: number;
      pending: number;
      finalizing: number;
      published: number;
      blocked: number;
      retryableFailed: number;
      objectCount: number;
      byteCount: number;
      retryCount: number;
      hashConflictCount: number;
      cleanupFailureCount: number;
      oldestQueueAgeSeconds: number;
      p95FinalizeLatencyMilliseconds: number;
    }>;
    alerts: Array<{ severity: "Critical" | "Warning"; code: string; message: string }>;
  };
}
