import { expect, test, type Page, type Route } from "@playwright/test";

test("admin creates validates and publishes an immutable Contract Version", async ({ page }) => {
  const api = new ContractAdminApi();
  await api.install(page);
  await page.goto("/admin/contracts");

  await expect(page.getByRole("heading", { name: "Contract Registry" })).toBeVisible();
  await page.getByRole("button", { name: "New Contract" }).click();
  await page.getByLabel("Operator ID").fill("operator-a");
  await page.getByLabel("Reason").fill("Approved proposal contract publication");
  await page.getByLabel("Contract key").fill("proposal");
  await page.getByLabel("Display name").fill("Proposal");
  await page.getByLabel("Version", { exact: true }).fill("1");
  await page.getByLabel("JSON Schema", { exact: true }).fill(schema);
  await page.getByRole("button", { name: "Create Draft" }).click();

  await expect(page.getByRole("heading", { name: "Proposal", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Validate" }).click();
  await expect(page.getByRole("status")).toContainText("Validation passed");
  await expect(page.getByRole("table")).toContainText("insuredName");

  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Publish" }).click();
  await expect(page.getByText("proposal/1 published.")).toBeVisible();
  await expect(page.getByText("Published · Initial")).toBeVisible();

  expect(api.createBody).toMatchObject({
    contractKey: "proposal",
    version: "1",
  });
  expect(JSON.stringify(api.createBody)).not.toMatch(/password|privateKey|accessToken/i);
});

test("malformed JSON is reported before a Draft mutation", async ({ page }) => {
  const api = new ContractAdminApi();
  await api.install(page);
  await page.goto("/admin/contracts");
  await page.getByRole("button", { name: "New Contract" }).click();
  await page.getByLabel("JSON Schema", { exact: true }).fill("{{");

  await expect(page.getByRole("alert").filter({ hasText: "Malformed JSON" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Create Draft" })).toBeDisabled();
  expect(api.createBody).toBeNull();
});

class ContractAdminApi {
  version: ReturnType<typeof contractVersion> | null = null;
  createBody: unknown = null;

  async install(page: Page) {
    await page.route("**/api/admin/contracts**", async (route) => {
      const request = route.request();
      const path = new URL(request.url()).pathname.replace(/^\/api/, "");
      if (path === "/admin/contracts/permissions") {
        return json(route, { canWriteDraft: true, canPublish: true });
      }
      if (path === "/admin/contracts" && request.method() === "GET") {
        return json(route, this.version ? [summary(this.version)] : []);
      }
      if (path === "/admin/contracts" && request.method() === "POST") {
        this.createBody = request.postDataJSON();
        const body = this.createBody as { contractKey: string; displayName: string; version: string; schema: string };
        this.version = contractVersion(body.contractKey, body.displayName, body.version, body.schema);
        return json(route, this.version, 201);
      }
      if (path === "/admin/contracts/proposal" && request.method() === "GET" && this.version) {
        return json(route, aggregate(this.version));
      }
      if (path.endsWith("/validate") && request.method() === "POST" && this.version) {
        return json(route, {
          isValid: true,
          canonicalSchema: schema,
          schemaHash: "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          paths: [{ path: "insuredName", kind: "Scalar", isAssignable: true, isRequired: false, isNullable: false, scalarType: "Text", constraints: { maxLength: 120 }, description: null }],
          compatibility: "Initial",
          compatibilityBaseVersionId: null,
          diagnostics: [],
        });
      }
      if (path.endsWith("/publish") && request.method() === "POST" && this.version) {
        this.version.lifecycle = "Published";
        this.version.canonicalSchema = schema;
        this.version.schemaHash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        this.version.compatibility = "Initial";
        this.version.concurrencyToken += 1;
        this.version.publishedAt = "2026-11-02T00:00:00Z";
        return json(route, this.version);
      }
      return route.fulfill({ status: 404, body: "not mocked" });
    });
  }
}

function contractVersion(contractKey: string, displayName: string, version: string, draftSchema: string) {
  return {
    contractVersionId: 101,
    contractId: 11,
    contractKey,
    displayName,
    version,
    reference: `${contractKey}/${version}`,
    lifecycle: "Draft",
    draftSchema,
    canonicalSchema: null as string | null,
    schemaHash: null as string | null,
    compatibility: null as string | null,
    compatibilityBaseVersionId: null,
    concurrencyToken: 1,
    createdAt: "2026-11-02T00:00:00Z",
    publishedAt: null as string | null,
    retiredAt: null,
  };
}

function summary(value: ReturnType<typeof contractVersion>) {
  return {
    contractVersionId: value.contractVersionId,
    contractKey: value.contractKey,
    displayName: value.displayName,
    version: value.version,
    reference: value.reference,
    lifecycle: value.lifecycle,
    schemaHash: value.schemaHash,
    concurrencyToken: value.concurrencyToken,
  };
}

function aggregate(value: ReturnType<typeof contractVersion>) {
  return {
    contractId: value.contractId,
    contractKey: value.contractKey,
    displayName: value.displayName,
    concurrencyToken: 1,
    versions: [value],
  };
}

function json(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    contentType: "application/json",
    body: JSON.stringify(body),
  });
}

const schema = JSON.stringify({
  type: "object",
  additionalProperties: false,
  properties: {
    insuredName: { type: "string", maxLength: 120 },
  },
}, null, 2);
