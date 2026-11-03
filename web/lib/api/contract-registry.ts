import { apiRequest } from "./client";
import type {
  ContractAdminCapabilities,
  ContractAggregate,
  ContractValidation,
  ContractVersionDetails,
  ContractVersionLifecycle,
  ContractVersionSummary,
} from "./contracts";

const root = "/admin/contracts";

function headers(operatorId: string, token?: number): HeadersInit {
  return {
    "X-Operator-Id": operatorId,
    "X-Request-Id": crypto.randomUUID(),
    ...(token === undefined ? {} : { "If-Match": `"${token}"` }),
  };
}

export function listContracts(lifecycle?: ContractVersionLifecycle) {
  const query = lifecycle ? `?lifecycle=${lifecycle}` : "";
  return apiRequest<ContractVersionSummary[]>(`${root}${query}`);
}

export function getContract(contractKey: string) {
  return apiRequest<ContractAggregate>(
    `${root}/${encodeURIComponent(contractKey)}`,
  );
}

export function getContractCapabilities() {
  return apiRequest<ContractAdminCapabilities>(`${root}/permissions`);
}

export function createContract(
  contractKey: string,
  displayName: string,
  version: string,
  schema: string,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ContractVersionDetails>(root, {
    method: "POST",
    headers: headers(operatorId),
    body: { contractKey, displayName, version, schema, reason },
  });
}

export function updateContractDraft(
  id: number,
  token: number,
  schema: string,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ContractVersionDetails>(`${root}/versions/${id}`, {
    method: "PUT",
    headers: headers(operatorId, token),
    body: { schema, reason },
  });
}

export function validateContractDraft(id: number, operatorId: string) {
  return apiRequest<ContractValidation>(`${root}/versions/${id}/validate`, {
    method: "POST",
    headers: headers(operatorId),
  });
}

export function cloneContractVersion(
  id: number,
  version: string,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ContractVersionDetails>(`${root}/versions/${id}/clone`, {
    method: "POST",
    headers: headers(operatorId),
    body: { version, reason },
  });
}

export function publishContractVersion(
  version: ContractVersionDetails,
  acknowledgeBreakingChange: boolean,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ContractVersionDetails>(
    `${root}/versions/${version.contractVersionId}/publish`,
    {
      method: "POST",
      headers: headers(operatorId, version.concurrencyToken),
      body: { acknowledgeBreakingChange, reason },
    },
  );
}

export function retireContractVersion(
  version: ContractVersionDetails,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ContractVersionDetails>(
    `${root}/versions/${version.contractVersionId}/retire`,
    {
      method: "POST",
      headers: headers(operatorId, version.concurrencyToken),
      body: { reason },
    },
  );
}
