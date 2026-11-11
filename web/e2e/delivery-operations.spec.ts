import { expect, test, type Page, type Route } from "@playwright/test";

test("mixed delivery states render without payloads and stale retry refreshes", async ({ page }) => {
  let stale = false;
  await mockDeliveryApi(page, () => stale, () => { stale = true; });
  await page.goto("/delivery");

  await expect(page.getByRole("heading", { name: "Delivery & JSON export" })).toBeVisible();
  await expect(page.getByText(/^Delivery running/)).toBeVisible();
  await expect(page.getByText("RetryableFailed", { exact: true })).toBeVisible();
  await expect(page.getByText("Delivered", { exact: true })).toBeVisible();
  await expect(page.getByRole("row").filter({ hasText: "proposal/1" })).toHaveCount(2);
  await expect(page.getByText("sha256:payload-a", { exact: true })).toBeVisible();
  await expect(page.getByText("Partner returned HTTP 503.", { exact: true })).toBeVisible();
  await expect(page.getByText(/client_secret|certificate|payload body/i)).toHaveCount(0);

  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("changed state");
  await expect(page.getByRole("button", { name: "Retry", exact: true })).toHaveCount(0);
});

test("JSON export history, retry and kill switches expose only safe metadata", async ({ page }) => {
  let retryRequest: { operator: string | null; body: unknown } | null = null;
  let controlRequest: { operator: string | null; body: unknown } | null = null;
  await mockJsonExportApi(page, (value) => { retryRequest = value; }, (value) => { controlRequest = value; });
  await page.goto("/delivery");

  await expect(page.getByText("New JSON activation disabled · finalizer paused", { exact: true })).toBeVisible();
  await expect(page.getByText("exports/customers/retry.json", { exact: true })).toBeVisible();
  await expect(page.getByText("sha256:staged", { exact: true })).toBeVisible();
  await expect(page.getByText("Hash / cleanup failures")).toBeVisible();
  await expect(page.getByText(/server-secret|client_secret|\"customer\"/i)).toHaveCount(0);
  await expect(page.getByRole("button", { name: /download/i })).toHaveCount(0);

  await page.getByLabel("Operator ID").fill("operator-json");
  await page.getByLabel("Action reason").fill("Storage recovered");
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Retry export" }).click();
  await expect.poll(() => retryRequest).not.toBeNull();
  expect(retryRequest).toEqual({
    operator: "operator-json",
    body: { reason: "Storage recovered" },
  });

  await page.getByLabel("Allow new JSON activation").check();
  await page.getByLabel("Pause JSON finalizer").uncheck();
  await page.getByRole("button", { name: "Apply controls" }).click();
  await expect.poll(() => controlRequest).not.toBeNull();
  expect(controlRequest).toMatchObject({
    operator: "operator-json",
    body: {
      jsonFileActivationEnabled: true,
      jsonFileFinalizerPaused: false,
      reason: "Storage recovered",
    },
  });
});

test("database drift and unknown outcome require audited duplicate-risk resolution", async ({ page }) => {
  let resolution: { operator: string | null; body: unknown } | null = null;
  await mockDatabaseOperationsApi(page, (value) => { resolution = value; });
  await page.goto("/delivery");

  await expect(page.getByRole("heading", { name: "Database delivery jobs" })).toBeVisible();
  await expect(page.getByText("UnknownOutcome", { exact: true })).toBeVisible();
  await expect(page.getByText("public.customer", { exact: true })).toBeVisible();
  await expect(page.getByText("BreakingChange", { exact: true })).toBeVisible();
  await expect(page.getByText(/password=|server-secret|insert into|mapped payload/i)).toHaveCount(0);

  await page.getByLabel("Operator ID").fill("operator-db");
  await page.getByLabel("Action reason").fill("Target owner approved duplicate risk");
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Retry (duplicate risk)" }).click();
  await expect.poll(() => resolution).not.toBeNull();
  expect(resolution).toMatchObject({
    operator: "operator-db",
    body: {
      reason: "Target owner approved duplicate risk",
      acceptDuplicateRisk: true,
    },
  });
});

async function mockDeliveryApi(
  page: Page,
  isStale: () => boolean,
  markStale: () => void,
) {
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(/^\/api/, "");
    if (path === "/file-jobs") {
      return json(route, [{
        id: 91,
        configId: 7,
        configVersionId: 12,
        fileName: "shadow.csv",
        importStatus: "Imported",
        archiveStatus: "Archived",
        totalRows: 1,
        normalizationStatus: "Completed",
        pendingRows: 0,
        doneRows: 1,
        filteredRows: 0,
        invalidRows: 0,
        failedRows: 0,
        lastError: null,
        createdAt: "2026-10-20T00:00:00Z",
        updatedAt: "2026-10-20T00:00:10Z",
        deliveryStatus: "CompletedWithErrors",
        pendingDeliveries: 0,
        deliveringDeliveries: 0,
        deliveredDeliveries: 1,
        retryableFailedDeliveries: 1,
        permanentFailedDeliveries: 0,
        endToEndStatus: "CompletedWithErrors",
      }]);
    }
    if (path === "/delivery-operations") {
      return json(route, {
        control: { deliveryPaused: false, apiContractActivationEnabled: true, jsonFileActivationEnabled: false, jsonFileFinalizerPaused: true, updatedAt: "2026-10-20T00:00:00Z", updatedBy: "operator-1", reason: "Shadow approved" },
        rollouts: [],
        groups: [],
        successRate: 0.5,
        retryableFailureRate: 0.5,
        permanentFailureRate: 0,
        p50LatencyMilliseconds: 100,
        p95LatencyMilliseconds: 200,
        p99LatencyMilliseconds: 250,
        oldestQueueAgeSeconds: 4,
        retryCount: 1,
        duplicateConflictCount: 0,
        schemaValidationFailureCount: 0,
        p95SourceToDeliveredSeconds: 2,
        alerts: [{ severity: "Warning", code: "RetryableFailureRate", message: "Retryable delivery failure rate is above 5%; pause delivery." }],
        jsonExports: { groups: [], alerts: [] },
      });
    }
    if (path === "/database-delivery-operations") {
      return json(route, emptyDatabaseOperations());
    }
    if (path === "/delivery-jobs" && url.searchParams.get("fileJobId") === "91") {
      return json(route, deliveryRows(!isStale()));
    }
    if (path === "/json-exports" && url.searchParams.get("fileJobId") === "91") {
      return json(route, []);
    }
    if (path === "/delivery-jobs/501/retry" && request.method() === "POST") {
      markStale();
      return route.fulfill({
        status: 409,
        contentType: "application/problem+json",
        body: JSON.stringify({ title: "Delivery job cannot be retried." }),
      });
    }
    return route.fulfill({ status: 404, body: "not mocked" });
  });
}

async function mockJsonExportApi(
  page: Page,
  onRetry: (value: { operator: string | null; body: unknown }) => void,
  onControl: (value: { operator: string | null; body: unknown }) => void,
) {
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(/^\/api/, "");
    if (path === "/file-jobs") return json(route, [{
      id: 91, configId: 7, configVersionId: 12, fileName: "safe.csv",
      importStatus: "Imported", archiveStatus: "Archived", totalRows: 1,
      normalizationStatus: "Completed", pendingRows: 0, doneRows: 1,
      filteredRows: 0, invalidRows: 0, failedRows: 0, lastError: null,
      createdAt: "2026-10-26T00:00:00Z", updatedAt: "2026-10-26T00:00:10Z",
      deliveryStatus: null, pendingDeliveries: 0, deliveringDeliveries: 0,
      deliveredDeliveries: 0, retryableFailedDeliveries: 0,
      permanentFailedDeliveries: 0, endToEndStatus: "ExportFailed",
    }]);
    if (path === "/delivery-jobs") return json(route, []);
    if (path === "/json-exports") return json(route, [{
      id: 801, fileJobId: 91, configVersionId: 12,
      destinationProfileId: "operations-json", artifactRevision: 2,
      status: "RetryableFailed", relativeDisplayPath: "exports/customers/retry.json",
      objectCount: null, byteCount: null, sha256: "sha256:staged", attemptCount: 2,
      lastErrorRedacted: "Publication failed and will be retried.",
      createdAt: "2026-10-26T00:00:00Z", updatedAt: "2026-10-26T00:00:10Z",
      publishedAt: null, canRetry: true,
    }]);
    if (path === "/delivery-operations" && request.method() === "GET") {
      return json(route, {
        control: { deliveryPaused: false, apiContractActivationEnabled: true, jsonFileActivationEnabled: false, jsonFileFinalizerPaused: true, updatedAt: "2026-10-26T00:00:00Z", updatedBy: "operator-1", reason: "Gate E pending" },
        rollouts: [], groups: [], successRate: 0, retryableFailureRate: 0,
        permanentFailureRate: 0, p50LatencyMilliseconds: 0,
        p95LatencyMilliseconds: 0, p99LatencyMilliseconds: 0,
        oldestQueueAgeSeconds: 0, retryCount: 0, duplicateConflictCount: 0,
        schemaValidationFailureCount: 0, p95SourceToDeliveredSeconds: null,
        alerts: [],
        jsonExports: {
          groups: [{ configId: 7, configVersionId: 12, destinationProfileId: "operations-json", total: 2, pending: 0, finalizing: 0, published: 1, blocked: 0, retryableFailed: 1, objectCount: 2, byteCount: 123, retryCount: 1, hashConflictCount: 1, cleanupFailureCount: 1, oldestQueueAgeSeconds: 12, p95FinalizeLatencyMilliseconds: 250 }],
          alerts: [{ severity: "Critical", code: "JsonExportHashConflict", message: "JSON artifact hash conflicts are greater than zero." }],
        },
      });
    }
    if (path === "/database-delivery-operations" && request.method() === "GET") {
      return json(route, emptyDatabaseOperations());
    }
    if (path === "/json-exports/801/retry" && request.method() === "POST") {
      onRetry({ operator: request.headers()["x-operator-id"] ?? null, body: request.postDataJSON() });
      return json(route, { jsonExportJobId: 801 });
    }
    if (path === "/delivery-operations/control" && request.method() === "PUT") {
      onControl({ operator: request.headers()["x-operator-id"] ?? null, body: request.postDataJSON() });
      return json(route, {});
    }
    return route.fulfill({ status: 404, body: "not mocked" });
  });
}

async function mockDatabaseOperationsApi(
  page: Page,
  onResolution: (value: { operator: string | null; body: unknown }) => void,
) {
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");
    if (path === "/file-jobs") return json(route, []);
    if (path === "/delivery-operations") return json(route, {
      control: { deliveryPaused: false, apiContractActivationEnabled: false, jsonFileActivationEnabled: false, jsonFileFinalizerPaused: true, databaseTableActivationEnabled: false, databaseDeliveryPaused: true, updatedAt: "2026-11-09T00:00:00Z", updatedBy: "operator", reason: "Gate I pending" },
      rollouts: [], groups: [], successRate: 0, retryableFailureRate: 0,
      permanentFailureRate: 0, p50LatencyMilliseconds: 0,
      p95LatencyMilliseconds: 0, p99LatencyMilliseconds: 0,
      oldestQueueAgeSeconds: 0, retryCount: 0, duplicateConflictCount: 0,
      schemaValidationFailureCount: 0, p95SourceToDeliveredSeconds: null,
      alerts: [], jsonExports: { groups: [], alerts: [] },
    });
    if (path === "/database-delivery-operations" && request.method() === "GET") return json(route, {
      metrics: { total: 1, pending: 0, delivering: 0, delivered: 0, alreadyDelivered: 0, retryableFailed: 0, permanentFailed: 0, deliveryConflict: 0, blockedSchemaDrift: 0, unknownOutcome: 1, oldestQueueAgeSeconds: 0 },
      jobs: [{ id: 901, fileJobId: 91, configVersionId: 12, sourceRowNumber: 1, outputKey: "main", destinationProfileId: "target-pg", provider: "PostgreSql", targetSchema: "public", targetTable: "customer", targetSchemaHash: "sha256:schema", mappedValuesHash: "sha256:values", status: "UnknownOutcome", attempts: 1, correlationId: null, lastErrorRedacted: "Delivery outcome cannot be proven without a usable unique key.", createdAt: "2026-11-09T00:00:00Z", updatedAt: "2026-11-09T00:00:01Z", hasExistingUniqueKey: false, canRetry: true, canMarkDelivered: true, canMarkFailed: true, retryRequiresDuplicateRiskAcceptance: true }],
      schemaChecks: [{ destinationProfileId: "target-pg", targetSchema: "public", targetTable: "customer", configuredSchemaHash: "sha256:schema", currentSchemaHash: "sha256:changed", compatibility: "BreakingChange", diagnosticsJson: "[]", checkedAt: "2026-11-09T00:00:02Z" }],
      alerts: [{ severity: "Critical", code: "DatabaseUnknownOutcome", message: "Automatic retry is disabled." }],
    });
    if (path === "/database-delivery-operations/901/retry" && request.method() === "POST") {
      onResolution({ operator: request.headers()["x-operator-id"] ?? null, body: request.postDataJSON() });
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 404, body: "not mocked" });
  });
}

function deliveryRows(canRetry: boolean) {
  const common = {
    fileJobId: 91,
    rowJobId: 301,
    sourceRowNumber: 1,
    configVersionId: 12,
    operationKind: "Initial",
    destinationProfileId: "pa-mtls",
    contractReference: "proposal/1",
    schemaHash: "sha256:schema",
    nextAttemptAt: null,
    correlationId: "stub-correlation",
    createdAt: "2026-10-20T00:00:00Z",
    updatedAt: "2026-10-20T00:00:01Z",
    deliveredAt: null,
  };
  return [
    {
      ...common,
      id: 501,
      outputKey: "pmx",
      payloadHash: "sha256:payload-a",
      status: canRetry ? "RetryableFailed" : "Delivering",
      attempts: 1,
      httpStatus: 503,
      lastErrorRedacted: "Partner returned HTTP 503.",
      canRetry,
    },
    {
      ...common,
      id: 502,
      outputKey: "ctp",
      payloadHash: "sha256:payload-b",
      status: "Delivered",
      attempts: 1,
      httpStatus: 202,
      lastErrorRedacted: null,
      deliveredAt: "2026-10-20T00:00:01Z",
      canRetry: false,
    },
  ];
}

function emptyDatabaseOperations() {
  return {
    metrics: {
      total: 0, pending: 0, delivering: 0, delivered: 0,
      alreadyDelivered: 0, retryableFailed: 0, permanentFailed: 0,
      deliveryConflict: 0, blockedSchemaDrift: 0, unknownOutcome: 0,
      oldestQueueAgeSeconds: 0,
    },
    jobs: [],
    schemaChecks: [],
    alerts: [],
  };
}

function json(route: Route, body: unknown) {
  return route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify(body),
  });
}
