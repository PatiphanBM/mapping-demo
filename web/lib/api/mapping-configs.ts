import { apiRequest } from "./client";
import type {
  CreateMappingConfigRequest,
  MappingCapabilities,
  MappingConfig,
  MappingConfigDetails,
  MappingConfigVersion,
  MappingConfigVersionDraft,
  PreviewMappingConfigVersionRequest,
  PreviewMappingConfigVersionResponse,
  TableDefinition,
  ValidateMappingConfigVersionResponse,
} from "./contracts";

export function getMappingCapabilities(): Promise<MappingCapabilities> {
  return apiRequest<MappingCapabilities>("/mapping-capabilities", {
    cache: "no-store",
  });
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
