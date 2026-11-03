import { apiRequest } from "./client";
import type {
  ApiDestinationProfileAdmin,
  ApiDestinationProfileAdminCapabilities,
  ApiDestinationProfileDraft,
  ApiDestinationProfileValidation,
} from "./contracts";

const root = "/admin/api-destination-profiles";

function auditHeaders(operatorId: string, token?: number): HeadersInit {
  return {
    "X-Operator-Id": operatorId,
    "X-Request-Id": crypto.randomUUID(),
    ...(token === undefined ? {} : { "If-Match": `"${token}"` }),
  };
}

export function listAdminProfiles() {
  return apiRequest<ApiDestinationProfileAdmin[]>(root);
}

export function getAdminProfileCapabilities() {
  return apiRequest<ApiDestinationProfileAdminCapabilities>(
    `${root}/permissions`,
  );
}

export function createAdminProfile(
  profileKey: string,
  displayName: string,
  draft: ApiDestinationProfileDraft,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ApiDestinationProfileAdmin>(root, {
    method: "POST",
    headers: auditHeaders(operatorId),
    body: { profileKey, displayName, draft, reason },
  });
}

export function updateAdminDraft(
  profileKey: string,
  versionNo: number,
  token: number,
  draft: ApiDestinationProfileDraft,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ApiDestinationProfileAdmin>(
    `${root}/${profileKey}/versions/${versionNo}`,
    {
      method: "PUT",
      headers: auditHeaders(operatorId, token),
      body: { draft, reason },
    },
  );
}

export function createAdminDraft(
  profile: ApiDestinationProfileAdmin,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ApiDestinationProfileAdmin>(
    `${root}/${profile.profileKey}/versions`,
    {
      method: "POST",
      headers: auditHeaders(operatorId, profile.concurrencyToken),
      body: { reason },
    },
  );
}

export function validateAdminDraft(
  profileKey: string,
  versionNo: number,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ApiDestinationProfileValidation>(
    `${root}/${profileKey}/versions/${versionNo}/validate`,
    {
      method: "POST",
      headers: auditHeaders(operatorId),
      body: { reason },
    },
  );
}

export function activateAdminVersion(
  profile: ApiDestinationProfileAdmin,
  versionNo: number,
  operatorId: string,
  reason: string,
  rollback = false,
) {
  return apiRequest<ApiDestinationProfileAdmin>(
    `${root}/${profile.profileKey}/versions/${versionNo}/${rollback ? "rollback" : "activate"}`,
    {
      method: "POST",
      headers: auditHeaders(operatorId, profile.concurrencyToken),
      body: { reason },
    },
  );
}

export function disableAdminProfile(
  profile: ApiDestinationProfileAdmin,
  operatorId: string,
  reason: string,
) {
  return apiRequest<ApiDestinationProfileAdmin>(
    `${root}/${profile.profileKey}/disable`,
    {
      method: "POST",
      headers: auditHeaders(operatorId, profile.concurrencyToken),
      body: { reason },
    },
  );
}
