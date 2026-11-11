import { apiRequest } from "./client";
import type {
  CreateMappingConfigRequest,
  ApiDestinationProfile,
  ContractSchemaResolveResponse,
  ContractReference,
  ContractResolveResponse,
  MappingCapabilities,
  MappingConfig,
  MappingConfigDetails,
  MappingConfigVersion,
  MappingConfigVersionDraft,
  PreviewMappingConfigVersionRequest,
  PreviewMappingConfigVersionResponse,
  TableDefinition,
  ValidateMappingConfigVersionResponse,
  DatabaseDestinationProfile,
  DatabaseTargetTableSummary,
  DatabaseTargetSchema,
} from "./contracts";

export function listDatabaseDestinationProfiles(): Promise<DatabaseDestinationProfile[]> {
  return apiRequest<DatabaseDestinationProfile[]>("/database-destinations/profiles", {
    cache: "no-store",
  });
}

export function listDatabaseTargetTables(
  profileId: string,
  schema: string,
): Promise<DatabaseTargetTableSummary[]> {
  return apiRequest<DatabaseTargetTableSummary[]>(
    `/database-destinations/profiles/${encodeURIComponent(profileId)}/schemas/${encodeURIComponent(schema)}/tables`,
    { cache: "no-store" },
  );
}

export function inspectDatabaseTargetTable(
  profileId: string,
  schema: string,
  table: string,
  refresh = false,
): Promise<DatabaseTargetSchema> {
  return apiRequest<DatabaseTargetSchema>(
    `/database-destinations/profiles/${encodeURIComponent(profileId)}/schemas/${encodeURIComponent(schema)}/tables/${encodeURIComponent(table)}?refresh=${refresh}`,
    { cache: "no-store" },
  );
}

export function getMappingCapabilities(): Promise<MappingCapabilities> {
  return apiRequest<MappingCapabilities>("/mapping-capabilities", {
    cache: "no-store",
  });
}

export function listApiDestinationProfiles(): Promise<ApiDestinationProfile[]> {
  return apiRequest<ApiDestinationProfile[]>("/api-destination-profiles", {
    cache: "no-store",
  });
}

export function resolveApiContract(
  profileId: string,
  contract: ContractReference,
): Promise<ContractResolveResponse> {
  return apiRequest<ContractResolveResponse>(
    `/api-destination-profiles/${encodeURIComponent(profileId)}/resolve`,
    { method: "POST", body: contract },
  );
}

export function resolveFileContract(
  profileId: string,
  contract: ContractReference,
): Promise<ContractSchemaResolveResponse> {
  return apiRequest<ContractSchemaResolveResponse>(
    `/file-destination-profiles/${encodeURIComponent(profileId)}/resolve`,
    { method: "POST", body: contract },
  );
}

export function listMappingConfigs(): Promise<MappingConfig[]> {
  return apiRequest<MappingConfig[]>("/mapping-configs", {
    cache: "no-store",
  });
}

export function getMappingConfig(id: number): Promise<MappingConfigDetails> {
  return apiRequest<MappingConfigDetails>(`/mapping-configs/${id}`, {
    cache: "no-store",
  });
}

export function createMappingConfig(
  request: CreateMappingConfigRequest,
): Promise<MappingConfig> {
  return apiRequest<MappingConfig>("/mapping-configs", {
    method: "POST",
    body: request,
  });
}

export function createMappingConfigVersion(
  configId: number,
  request: MappingConfigVersionDraft,
): Promise<MappingConfigVersion> {
  return apiRequest<MappingConfigVersion>(
    `/mapping-configs/${configId}/versions`,
    {
      method: "POST",
      body: request,
    },
  );
}

export function activateMappingConfigVersion(
  configId: number,
  versionNo: number,
): Promise<void> {
  return apiRequest<void>(
    `/mapping-configs/${configId}/versions/${versionNo}/activate`,
    { method: "POST" },
  );
}

export function previewMappingConfigVersion(
  configId: number,
  request: PreviewMappingConfigVersionRequest,
): Promise<PreviewMappingConfigVersionResponse> {
  return apiRequest<PreviewMappingConfigVersionResponse>(
    `/mapping-configs/${configId}/versions/preview`,
    {
      method: "POST",
      body: request,
    },
  );
}

export function previewMappingConfigFile(
  configId: number,
  file: File,
  request: MappingConfigVersionDraft & { evaluationAt: string },
): Promise<PreviewMappingConfigVersionResponse> {
  const form = new FormData();
  form.set("file", file);
  form.set("evaluationAt", request.evaluationAt);
  form.set("inputLayoutJson", JSON.stringify(request.inputLayout));
  form.set(
    "transformationProgramJson",
    JSON.stringify(request.transformationProgram),
  );
  form.set("destinationJson", JSON.stringify(request.destination));
  return apiRequest<PreviewMappingConfigVersionResponse>(
    `/mapping-configs/${configId}/versions/preview-file`,
    { method: "POST", body: form },
  );
}

export function validateMappingConfigVersion(
  configId: number,
  request: MappingConfigVersionDraft,
): Promise<ValidateMappingConfigVersionResponse> {
  return apiRequest<ValidateMappingConfigVersionResponse>(
    `/mapping-configs/${configId}/versions/validate`,
    {
      method: "POST",
      body: request,
    },
  );
}

export function listTables(): Promise<TableDefinition[]> {
  return apiRequest<TableDefinition[]>("/tables", { cache: "no-store" });
}
