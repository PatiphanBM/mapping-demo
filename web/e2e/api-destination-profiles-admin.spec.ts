import { expect, test, type Page, type Route } from "@playwright/test";

test("admin creates, validates, activates and selects a safe profile", async ({ page }) => {
  const api = new ProfileAdminApi();
  await api.install(page);
  await page.goto("/admin/api-destination-profiles");

  await expect(page.getByRole("heading", { name: "API destination profiles" })).toBeVisible();
  await page.getByRole("button", { name: "New profile" }).click();
  await page.getByLabel("Operator ID").fill("operator-a");
  await page.getByLabel("Reason").fill("Approved sandbox partner onboarding");
  await page.getByLabel("Profile key").fill("partner-a");
  await page.getByLabel("Display name").fill("Partner A");
  await page.getByLabel("Environment", { exact: true }).selectOption("Sandbox");
  await page.getByLabel("Authentication scheme").selectOption("None");
  await page.getByLabel("Logical secret reference").fill("");
  await expect(page.getByLabel("Published Contract 1")).toHaveValue("proposal/1");
  await page.getByRole("button", { name: "Create draft" }).click();

  await expect(page.getByRole("heading", { name: "Partner A" })).toBeVisible();
  await expect(page.getByText("delivery.partner.example", { exact: false })).toBeVisible();
  await page.getByRole("button", { name: "Validate" }).click();
  await expect(page.getByRole("status")).toContainText("validation passed");

  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Activate" }).click();
  await expect(page.getByRole("button", { name: /Partner A/ })).toContainText("Active v1");
  await page.getByRole("button", { name: /Partner A/ }).click();
  await expect(page.getByText("v1 · Active", { exact: false })).toBeVisible();

  expect(api.lastCreateBody).toMatchObject({
    profileKey: "partner-a",
    draft: { authScheme: "None", authSecretReference: null },
  });
  expect(JSON.stringify(api.lastCreateBody)).not.toMatch(/clientSecret|pfxBase64|privateKey/i);
});

test("stale draft keeps edits and offers a refresh", async ({ page }) => {
  const api = new ProfileAdminApi(profile("stale-profile", "Stale Profile"));
  api.failNextSaveAsStale = true;
  await api.install(page);
  await page.goto("/admin/api-destination-profiles");

  await page.getByLabel("Operator ID").fill("operator-a");
  await page.getByLabel("Reason").fill("Approved endpoint correction");
  await page.getByLabel("Delivery base URL").fill("https://changed.partner.example");
  await page.getByRole("button", { name: "Save draft" }).click();

  await expect(page.getByRole("alert").filter({
    hasText: "changed by another operator",
  })).toBeVisible();
  await expect(page.getByLabel("Delivery base URL")).toHaveValue("https://changed.partner.example");
  await expect(page.getByRole("button", { name: "Refresh server version" })).toBeVisible();
});

class ProfileAdminApi {
  profiles: ReturnType<typeof profile>[];
  lastCreateBody: unknown = null;
  failNextSaveAsStale = false;

  constructor(initial?: ReturnType<typeof profile>) {
    this.profiles = initial ? [initial] : [];
  }

  async install(page: Page) {
    await page.route("**/api/admin/contracts**", async (route) => {
      return json(route, [{
        contractVersionId: 501,
        contractKey: "proposal",
        displayName: "Proposal",
        version: "1",
        reference: "proposal/1",
        lifecycle: "Published",
        schemaHash: "sha256:published",
        concurrencyToken: 2,
      }]);
    });
    await page.route("**/api/admin/api-destination-profiles**", async (route) => {
      const request = route.request();
      const path = new URL(request.url()).pathname.replace(/^\/api/, "");
      if (path === "/admin/api-destination-profiles" && request.method() === "GET") {
        return json(route, this.profiles);
      }
      if (path === "/admin/api-destination-profiles/permissions" && request.method() === "GET") {
        return json(route, {
          canReadEndpoints: true,
          canWriteDraft: true,
          canActivate: true,
          canActivateProduction: true,
          canDisable: true,
        });
      }
      if (path === "/admin/api-destination-profiles" && request.method() === "POST") {
        this.lastCreateBody = request.postDataJSON();
        const body = this.lastCreateBody as { profileKey: string; displayName: string; draft: Record<string, unknown> };
        const created = profile(body.profileKey, body.displayName, body.draft);
        this.profiles = [created];
        return json(route, created, 201);
      }
      const current = this.profiles[0];
      if (!current) return route.fulfill({ status: 404 });
      if (path.endsWith("/validate") && request.method() === "POST") {
        current.versions[0].validatedAt = "2026-10-30T00:00:00Z";
        current.versions[0].concurrencyToken += 1;
        return json(route, {
          isValid: true,
          contentHash: current.versions[0].contentHash,
          validatedAt: current.versions[0].validatedAt,
          secretStatus: "Unchecked",
          diagnostics: [],
        });
      }
      if (path.endsWith("/activate") && request.method() === "POST") {
        current.activeVersionNo = 1;
        current.concurrencyToken += 1;
        current.versions[0].lifecycle = "Active";
        return json(route, current);
      }
      if (/\/versions\/1$/.test(path) && request.method() === "PUT") {
        if (this.failNextSaveAsStale) {
          this.failNextSaveAsStale = false;
          return route.fulfill({
            status: 412,
            contentType: "application/problem+json",
            body: JSON.stringify({
              title: "Draft changed or is unavailable.",
              currentConcurrencyToken: 2,
            }),
          });
        }
        return json(route, current);
      }
      return route.fulfill({ status: 404, body: "not mocked" });
    });
  }
}

function profile(
  profileKey: string,
  displayName: string,
  override: Record<string, unknown> = {},
) {
  return {
    profileKey,
    displayName,
    concurrencyToken: 1,
    activeVersionNo: null as number | null,
    versions: [{
      profileVersionId: 101,
      versionNo: 1,
      lifecycle: "Draft",
      environment: "Sandbox",
      schemaBaseUrl: "https://contracts.partner.example",
      schemaPathTemplate: "/schemas/{contractReference}",
      deliveryBaseUrl: "https://delivery.partner.example",
      deliveryPathTemplate: "/proposals/{contractVersion}",
      method: "POST",
      authScheme: "Mtls",
      authSecretReference: "partner-mtls-reference",
      networkPolicyReference: "partner-egress-default",
      timeoutSeconds: 10,
      idempotencyHeaderName: "Idempotency-Key",
      correlationHeaderName: "X-Correlation-Id",
      allowedHeaderNames: ["Idempotency-Key", "X-Correlation-Id"],
      supportedContracts: [{ id: "proposal", version: "1", reference: "proposal/1" }],
      payloadBindings: [{ targetPath: "meta.fileJobId", source: "FileJobId" }],
      contentHash: "sha256:draft",
      concurrencyToken: 1,
      validatedAt: null as string | null,
      ...override,
    }],
  };
}

function json(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    contentType: "application/json",
    body: JSON.stringify(body),
  });
}
