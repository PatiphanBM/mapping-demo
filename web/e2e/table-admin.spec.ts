import { expect, test, type Page, type Route } from "@playwright/test";

test("admin creates a Source table, redirects, verifies, and adds a column", async ({ page }) => {
  const api = new TableAdminApi();
  await api.install(page);
  await page.goto("/admin/tables");

  await expect(page.getByRole("heading", { name: "Table Catalog" })).toBeVisible();
  await page.getByRole("button", { name: "New table" }).click();
  await page.getByLabel("Table name").fill("src_payments");
  await page.getByLabel("Column 1 name").fill("payment_ref");
  await expect(page.getByLabel("Column 1 data type")).toBeDisabled();
  await page.getByLabel("Reason").fill("Approved Source table onboarding");
  await page.getByRole("button", { name: "Create and verify" }).click();

  await expect(page).toHaveURL(/\/admin\/tables\/41/, { timeout: 15_000 });
  await expect(page.getByRole("heading", { name: "src_payments" })).toBeVisible();
  await expect(page.getByText("Metadata and physical PostgreSQL schema match.")).toBeVisible();
  await expect(page.locator("span").filter({ hasText: /^Healthy$/ })).toBeVisible();
  expect(api.createBody).toMatchObject({
    name: "src_payments",
    kind: "Source",
  });
  expect(api.createReason).toBe("Approved Source table onboarding");

  await page.getByRole("button", { name: "Add column" }).click();
  await page.getByLabel("Column 1 name").fill("amount_raw");
  await page.getByLabel("Reason").fill("Approved additive source field");
  await page.getByRole("button", { name: "Add column and verify" }).click();

  await expect(page.getByRole("cell", { name: "amount_raw" })).toBeVisible();
  await expect(page.getByText("Metadata and physical PostgreSQL schema match.")).toBeVisible();
  expect(api.addReason).toBe("Approved additive source field");
  expect(api.table.columns).toHaveLength(2);
});

test("detail shows safe field drift and hides mutations when the kill switch is off", async ({ page }) => {
  const api = new TableAdminApi();
  api.mutationsEnabled = false;
  api.table.verificationStatus = "Drifted";
  api.latest = verification("Drifted", [
    {
      field: "columns.payment_ref.dataType",
      code: "ColumnTypeMismatch",
      message: "The physical column type differs from metadata.",
      expected: "character varying(50)",
      actual: "integer",
    },
  ]);
  await api.install(page);
  await page.goto("/admin/tables/41");

  await expect(page.getByText("ColumnTypeMismatch")).toBeVisible();
  await expect(page.getByText("character varying(50)")).toBeVisible();
  await expect(page.getByText("Mutations are paused")).toBeVisible();
  await expect(page.getByRole("button", { name: "Add column" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Verify now" })).toBeVisible();
  await expect(page.getByRole("button", { name: /drop|rename|alter/i })).toHaveCount(0);
  await expect(page.getByText(/connection string|select \*/i)).toHaveCount(0);
});

class TableAdminApi {
  mutationsEnabled = true;
  createBody: unknown = null;
  createReason: string | null = null;
  addReason: string | null = null;
  latest: ReturnType<typeof verification> | null = null;
  table = tableDefinition();

  async install(page: Page) {
    await page.route("**/api/tables**", async (route) => {
      const request = route.request();
      const url = new URL(request.url());
      const path = url.pathname.replace(/^\/api/, "");
      const method = request.method();

      if (path === "/tables/permissions") {
        return json(route, {
          canRead: true,
          canCreate: this.mutationsEnabled,
          canAddColumn: this.mutationsEnabled,
          mutationsEnabled: this.mutationsEnabled,
        });
      }
      if (path === "/tables" && method === "GET") {
        return json(route, this.table.name === "src_payments" ? [this.table] : []);
      }
      if (path === "/tables" && method === "POST") {
        this.createBody = request.postDataJSON();
        this.createReason = await request.headerValue("x-change-reason");
        const body = this.createBody as {
          name: string;
          kind: "Source" | "Normalized";
          columns: ReturnType<typeof businessColumn>[];
        };
        this.table = {
          ...this.table,
          name: body.name,
          kind: body.kind,
          columns: body.columns,
          verificationStatus: "Unknown",
        };
        this.latest = null;
        return json(route, this.table, 201);
      }
      if (path === "/tables/41" && method === "GET") {
        return json(route, this.table);
      }
      if (path === "/tables/41/verification" && method === "GET") {
        return this.latest ? json(route, this.latest) : route.fulfill({ status: 204 });
      }
      if (path === "/tables/41/references" && method === "GET") {
        return json(route, [
          {
            configId: 7,
            configName: "payments-import",
            usage: "Source",
            configVersionId: 13,
            versionNo: 2,
            isActiveVersion: true,
            pinnedJobCount: 4,
          },
        ]);
      }
      if (path === "/tables/41/verify" && method === "POST") {
        this.latest = verification("Healthy", []);
        this.table.verificationStatus = "Healthy";
        this.table.verificationCheckedAt = this.latest.checkedAt;
        return json(route, this.latest);
      }
      if (path === "/tables/41/columns" && method === "POST") {
        this.addReason = await request.headerValue("x-change-reason");
        const column = request.postDataJSON() as ReturnType<typeof businessColumn>;
        if (this.table.columns.some((value) => value.name === column.name)) {
          return json(route, { title: "Column name already exists.", status: 409 }, 409);
        }
        this.table.columns.push(column);
        this.table.verificationStatus = "Unknown";
        this.latest = null;
        return route.fulfill({ status: 204 });
      }
      return route.fulfill({ status: 404, body: "not mocked" });
    });
  }
}

function tableDefinition(): {
  id: number;
  name: string;
  kind: "Source" | "Normalized";
  columns: ReturnType<typeof businessColumn>[];
  createdAt: string;
  verificationStatus: string;
  verificationCheckedAt: string | null;
} {
  return {
    id: 41,
    name: "src_existing",
    kind: "Source" as const,
    columns: [businessColumn("payment_ref")],
    createdAt: "2026-11-03T00:00:00Z",
    verificationStatus: "Unknown",
    verificationCheckedAt: null as string | null,
  };
}

function businessColumn(name = "payment_ref") {
  return {
    name,
    dataType: "Text" as const,
    isRequired: false,
    length: 50,
    precision: null,
    scale: null,
  };
}

function verification(status: "Healthy" | "Drifted", diagnostics: unknown[]) {
  return {
    status,
    expected: {
      columns: [
        { name: "id", dataType: "bigint", isNullable: false, isIdentity: true },
        { name: "file_job_id", dataType: "bigint", isNullable: false, isIdentity: false },
        { name: "row_number", dataType: "integer", isNullable: false, isIdentity: false },
        { name: "physical_line_number", dataType: "integer", isNullable: false, isIdentity: false },
        { name: "imported_at", dataType: "timestamp with time zone", isNullable: false, isIdentity: false },
        { name: "payment_ref", dataType: "character varying(50)", isNullable: true, isIdentity: false },
      ],
      primaryKey: { kind: "PrimaryKey", columns: ["id"] },
      uniqueConstraints: [{ kind: "Unique", columns: ["file_job_id", "row_number"] }],
    },
    actual: null,
    diagnostics,
    checkedAt: "2026-11-03T01:00:00Z",
    durationMilliseconds: 4.2,
  };
}

function json(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    contentType: "application/json",
    body: JSON.stringify(body),
  });
}
