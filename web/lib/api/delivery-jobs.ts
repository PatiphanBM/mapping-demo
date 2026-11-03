import { apiRequest } from "./client";
import type { DeliveryJob, DeliveryOperations, FileJob, JsonExportHistory } from "./contracts";

export function listFileJobs(): Promise<FileJob[]> {
  return apiRequest<FileJob[]>("/file-jobs", { cache: "no-store" });
}

export function listDeliveryJobs(fileJobId: number): Promise<DeliveryJob[]> {
  return apiRequest<DeliveryJob[]>(`/delivery-jobs?fileJobId=${fileJobId}`, {
    cache: "no-store",
  });
}

export function retryDeliveryJob(deliveryJobId: number): Promise<void> {
  return apiRequest<void>(`/delivery-jobs/${deliveryJobId}/retry`, {
    method: "POST",
  });
}

export function getDeliveryOperations(): Promise<DeliveryOperations> {
  return apiRequest<DeliveryOperations>("/delivery-operations", {
    cache: "no-store",
  });
}

export function listJsonExports(fileJobId: number): Promise<JsonExportHistory[]> {
  return apiRequest<JsonExportHistory[]>(`/json-exports?fileJobId=${fileJobId}`, {
    cache: "no-store",
  });
}

export function retryJsonExport(
  exportId: number,
  operatorId: string,
  reason: string,
): Promise<void> {
  return apiRequest<void>(`/json-exports/${exportId}/retry`, {
    method: "POST",
    headers: { "X-Operator-Id": operatorId },
    body: { reason },
  });
}

export function updateDeliveryControl(
  control: Pick<DeliveryOperations["control"],
    | "deliveryPaused"
    | "apiContractActivationEnabled"
    | "jsonFileActivationEnabled"
    | "jsonFileFinalizerPaused">,
  operatorId: string,
  reason: string,
): Promise<DeliveryOperations["control"]> {
  return apiRequest<DeliveryOperations["control"]>("/delivery-operations/control", {
    method: "PUT",
    headers: { "X-Operator-Id": operatorId },
    body: { ...control, reason },
  });
}
