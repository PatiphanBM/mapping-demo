import { apiRequest } from "./client";
import type {
  ColumnDefinition,
  CreateTableRequest,
  TableAdminCapabilities,
  TableDefinition,
  TableKind,
  TableReference,
  TableSchemaVerification,
  TableVerificationStatus,
} from "./contracts";

export interface TableFilters {
  search?: string;
  kind?: TableKind | "";
  status?: TableVerificationStatus | "";
}

function mutationHeaders(operatorId: string, reason?: string): HeadersInit {
  return {
    "X-Operator-Id": operatorId,
    "X-Request-Id": crypto.randomUUID(),
    ...(reason ? { "X-Change-Reason": reason } : {}),
  };
}

export function listAdminTables(filters: TableFilters = {}) {
  const query = new URLSearchParams({ limit: "100", offset: "0" });
  if (filters.search) query.set("search", filters.search);
  if (filters.kind) query.set("kind", filters.kind);
  if (filters.status) query.set("status", filters.status);
  return apiRequest<TableDefinition[]>(`/tables?${query}`);
}

export function getAdminTable(id: number) {
  return apiRequest<TableDefinition>(`/tables/${id}`);
}

export function getTableAdminCapabilities() {
  return apiRequest<TableAdminCapabilities>("/tables/permissions");
}

export function createAdminTable(
  request: CreateTableRequest,
  operatorId: string,
  reason: string,
) {
  return apiRequest<TableDefinition>("/tables", {
    method: "POST",
    headers: mutationHeaders(operatorId, reason),
    body: request,
  });
}

export function addAdminTableColumn(
  id: number,
  column: ColumnDefinition,
  operatorId: string,
  reason: string,
) {
  return apiRequest<void>(`/tables/${id}/columns`, {
    method: "POST",
    headers: mutationHeaders(operatorId, reason),
    body: column,
  });
}

export function verifyAdminTable(id: number, operatorId: string) {
  return apiRequest<TableSchemaVerification>(`/tables/${id}/verify`, {
    method: "POST",
    headers: mutationHeaders(operatorId),
  });
}

export function getLatestTableVerification(id: number) {
  return apiRequest<TableSchemaVerification | undefined>(
    `/tables/${id}/verification`,
  );
}

export function getTableReferences(id: number) {
  return apiRequest<TableReference[]>(`/tables/${id}/references`);
}
