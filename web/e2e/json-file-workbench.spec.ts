import { expect, test, type Page, type Route } from "@playwright/test";

test("create switches delivery kinds and submits only JsonFile fields", async ({
  page,
}) => {
  let submitted: Record<string, unknown> | null = null;
  await mockCatalog(page, (body) => {
    submitted = body;
  });
  await page.goto("/configs/new");

  await page.getByRole("radio", { name: /Whole file · JSON file/ }).check();
  await expect(page.getByLabel("Normalized table")).toHaveCount(0);
  await expect(page.getByLabel("API destination profile")).toHaveCount(0);
  await page
    .getByLabel("File destination profile")
    .selectOption("operations-json");
  await page.getByLabel("Contract version").selectOption("proposal/1");
  await expect(page.getByText("UTF-8 · no BOM", { exact: true })).toBeVisible();
  await expect(page.getByText("Whole file", { exact: true })).toBeVisible();
  await expect(page.getByText(/server-secret|^[A-Z]:\\/i)).toHaveCount(0);

  await page.getByRole("radio", { name: /One record at a time/ }).check();
  await expect(page.getByLabel("File destination profile")).toHaveCount(0);
  await page.getByRole("radio", { name: /Whole file · JSON file/ }).check();
  await expect(page.getByLabel("File destination profile")).toHaveValue("");

  await page.getByLabel("Name", { exact: true }).fill("customer-json");
  await page.getByLabel("Input folder").fill("customer-input");
  await page.getByLabel("Source table").selectOption("7");
  await page
    .getByLabel("File destination profile")
    .selectOption("operations-json");
  await page.getByLabel("Contract version").selectOption("proposal/1");
  await page.getByLabel("Relative folder").fill("exports/customers");
  await page
    .getByLabel("File name template")
    .fill("{sourceBaseName}.{artifactRevision}.json");
  await page.getByRole("button", { name: "Create config" }).click();

  await expect.poll(() => submitted).not.toBeNull();
  expect(submitted).toMatchObject({
    name: "customer-json",
    normalizedTableId: null,
    destination: {
      kind: "jsonFile",
      fileDestinationProfileId: "operations-json",
      relativeFolder: "exports/customers",
      fileNameTemplate: "{sourceBaseName}.{artifactRevision}.json",
      formatting: "compact",
      contract: {
        id: "proposal",
        version: "1",
        reference: "proposal/1",
        schemaHash: "sha256:file-schema",
      },
    },
  });
  expect(JSON.stringify(submitted)).not.toContain("apiDestinationProfileId");
});

test("workbench previews a JSON array, clones unchanged, and switches by keyboard", async ({ page }) => {
  let cloned: Record<string, unknown> | null = null;
  let previewed: Record<string, unknown> | null = null;
  await mockJsonWorkbench(
    page,
    (body) => {
      cloned = body;
    },
    (body) => {
      previewed = body;
    },
  );
  await page.goto("/configs/11");

  const jsonRadio = page.getByRole("radio", { name: /Whole file · JSON file/ });
  await expect(jsonRadio).toBeChecked();
  await expect(page.getByLabel("File destination profile")).toHaveValue("operations-json");
  await expect(page.getByText("UTF-8 · no BOM", { exact: true }).first()).toBeVisible();
  await expect(page.getByText(/server-secret|^[A-Z]:\\/i)).toHaveCount(0);

  const contractRow = page.getByRole("row").filter({ hasText: "customer.name" });
  await contractRow.getByRole("combobox").selectOption("name");
  await page.getByLabel("name", { exact: true }).fill("Alice");
  await page.getByRole("button", { name: "Preview", exact: true }).click();
  await expect.poll(() => previewed).not.toBeNull();
  expect(previewed).toMatchObject({
    destination: {
      contract: { schemaHash: "sha256:file-schema" },
    },
  });
  await expect(page.getByLabel("Final JSON file preview")).toHaveText(
    JSON.stringify([{ customer: { name: "Alice" } }], null, 2),
  );
  await expect(page.getByText("Preview only · no file written", { exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Clone version" }).click();
  await expect.poll(() => cloned).not.toBeNull();
  expect(cloned).toMatchObject({
    destination: {
      kind: "jsonFile",
      fileDestinationProfileId: "operations-json",
      relativeFolder: "exports/customers",
      fileNameTemplate: "{sourceBaseName}.{artifactRevision}.json",
      formatting: "indented",
    },
  });

  await jsonRadio.focus();
  await jsonRadio.press("ArrowRight");
  await expect(page.getByRole("radio", { name: /One record at a time/ })).toBeChecked();
  await expect(page.getByLabel("File destination profile")).toHaveCount(0);
  await expect(page.getByLabel("API destination profile")).toBeVisible();

  await page.getByRole("radio", { name: /^Legacy Table/ }).check();
  await expect(page.getByLabel("Normalized table")).toBeVisible();
  await expect(page.getByLabel("Contract version")).toHaveCount(0);
});

test("workbench submits the resolved schema hash when creating a version", async ({
  page,
}) => {
  let created: Record<string, unknown> | null = null;
  await mockJsonWorkbench(
    page,
    (body) => {
      created = body;
    },
    () => undefined,
  );
  await page.goto("/configs/11");

  await page.getByRole("button", { name: "Create version" }).click();

  await expect.poll(() => created).not.toBeNull();
  expect(created).toMatchObject({
    destination: {
      contract: { schemaHash: "sha256:file-schema" },
    },
  });
});

test("workbench applies an input format to a contract date mapping", async ({
  page,
}) => {
  let created: Record<string, unknown> | null = null;
  await mockJsonWorkbench(
    page,
    (body) => {
      created = body;
    },
    () => undefined,
  );
  await page.goto("/configs/11");

  const dateRow = page.getByRole("row").filter({ hasText: "customer.birthDate" });
  await dateRow.getByRole("combobox").first().selectOption("birth_date");
  await page
    .getByLabel("Input format for customer.birthDate")
    .selectOption("yyyyMMdd");
  await page.getByRole("button", { name: "Create version" }).click();

  await expect.poll(() => created).not.toBeNull();
  expect(created).toMatchObject({
    transformationProgram: {
      outputs: [{
        fields: expect.arrayContaining([{
          targetPath: "customer.birthDate",
          inputFormat: "yyyyMMdd",
          value: { kind: "source", field: "birth_date" },
        }]),
      }],
    },
  });
});

test("workbench previews an Excel workbook and creates an XLS version", async ({
  page,
}) => {
  let created: Record<string, unknown> | null = null;
  let filePreviewBody = "";
  await mockJsonWorkbench(
    page,
    (body) => {
      created = body;
    },
    () => undefined,
    (body) => {
      filePreviewBody = body;
    },
  );
  await page.goto("/configs/11");

  await page.getByRole("button", { name: "Excel", exact: true }).click();
  await page.getByLabel("Workbook kind").selectOption("Xls");
  await page.getByLabel("Worksheet").fill("MTLSPA");
  await page.getByLabel("Header row").fill("1");
  await page.getByLabel("Data starts at").fill("2");
  await page.getByLabel("Choose workbook").setInputFiles({
    name: "MTLSPA.xls",
    mimeType: "application/vnd.ms-excel",
    buffer: Buffer.from("fake-xls"),
  });
  await page
    .getByRole("button", { name: "Preview workbook with API" })
    .click();

  await expect.poll(() => filePreviewBody).not.toBe("");
  expect(filePreviewBody).toContain('"kind":"excel"');
  expect(filePreviewBody).toContain('"workbookKind":"Xls"');
  expect(filePreviewBody).toContain('"sheet":"MTLSPA"');
  expect(filePreviewBody).toContain("MTLSPA.xls");

  await page.getByRole("button", { name: "Create version" }).click();
  await expect.poll(() => created).not.toBeNull();
  expect(created).toMatchObject({
    inputLayout: {
      kind: "excel",
      workbookKind: "Xls",
      sheet: "MTLSPA",
      headerRow: 1,
      dataStartRow: 2,
      blankRowPolicy: "Skip",
    },
    acceptedExtensions: [".xls"],
    destination: {
      contract: { schemaHash: "sha256:file-schema" },
    },
  });
});

test("activates a JSON version and rolls new files back to the prior Legacy version", async ({ page }) => {
  const activations: number[] = [];
  await mockActivationRollback(page, activations);
  await page.goto("/configs/11");

  const jsonVersion = page.locator("article").filter({ hasText: "Version 3" });
  await jsonVersion.getByRole("button", { name: "Activate" }).click();
  await expect(page.getByRole("status")).toHaveText("Version 3 is now active.");
  await expect(jsonVersion.getByText("Active", { exact: true })).toBeVisible();

  const legacyVersion = page.locator("article").filter({ hasText: "Version 2" });
  await legacyVersion.getByRole("button", { name: "Activate" }).click();
  await expect(page.getByRole("status")).toHaveText("Version 2 is now active.");
  await expect(legacyVersion.getByText("Active", { exact: true })).toBeVisible();
  expect(activations).toEqual([3, 2]);
});

async function mockCatalog(
  page: Page,
  onCreate: (body: Record<string, unknown>) => void,
) {
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");
    if (path === "/tables") {
      return json(route, [
        {
          id: 7,
          name: "source_customer",
          kind: "Source",
          columns: [{ name: "name", dataType: "Text", isRequired: true }],
        },
        {
          id: 8,
          name: "normalized_customer",
          kind: "Normalized",
          columns: [{ name: "name", dataType: "Text", isRequired: true }],
        },
      ]);
    }
    if (path === "/api-destination-profiles") {
      return json(route, [
        {
          id: "customer-api",
          displayName: "Customer API",
          enabled: true,
          method: "POST",
          deliveryPathTemplate: "/customers",
          environment: "Stub",
          allowedHeaderNames: [],
          supportedContracts: [
            { id: "proposal", version: "1", reference: "proposal/1" },
          ],
          payloadBindings: [],
        },
      ]);
    }
    if (path === "/mapping-capabilities") {
      return json(route, capabilities);
    }
    if (path === "/file-destination-profiles/operations-json/resolve") {
      return json(route, {
        contract: {
          id: "proposal",
          version: "1",
          reference: "proposal/1",
          schemaHash: "sha256:file-schema",
        },
        schemaHash: "sha256:file-schema",
        fetchedAt: "2026-10-26T00:00:00Z",
        paths: [],
      });
    }
    if (path === "/mapping-configs" && request.method() === "POST") {
      onCreate(request.postDataJSON() as Record<string, unknown>);
      return json(route, { id: 11 });
    }
    return route.fulfill({ status: 404, body: "not mocked" });
  });
}

async function mockJsonWorkbench(
  page: Page,
  onClone: (body: Record<string, unknown>) => void,
  onPreview: (body: Record<string, unknown>) => void,
  onFilePreview?: (body: string) => void,
) {
  const destination = {
    kind: "jsonFile",
    fileDestinationProfileId: "operations-json",
    contract: { id: "proposal", version: "1", reference: "proposal/1", schemaHash: "" },
    relativeFolder: "exports/customers",
    fileNameTemplate: "{sourceBaseName}.{artifactRevision}.json",
    formatting: "indented",
  };
  const inputLayout = {
    kind: "delimited",
    encoding: "utf-8",
    delimiter: ",",
    preambleLines: 0,
    headerPolicy: "SingleRow",
    fields: [
      { inputHeader: "name", sourceColumn: "name" },
      { inputHeader: "birth_date", sourceColumn: "birth_date" },
    ],
  };
  const transformationProgram = {
    languageVersion: 2,
    semanticsProfile: "sqlserver-legacy-v1",
    outputs: [{ outputKey: "main", fields: [], arrayGroups: [] }],
  };
  const version = {
    id: 102,
    versionNo: 2,
    isActive: true,
    inputLayout,
    transformationProgram,
    destination,
    acceptedExtensions: [".csv"],
    contractSnapshot: null,
  };

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");
    if (path === "/tables") {
      return json(route, [
        { id: 7, name: "source_customer", kind: "Source", columns: [
          { name: "name", dataType: "Text", isRequired: true },
          { name: "birth_date", dataType: "Text", isRequired: true },
        ] },
        { id: 8, name: "normalized_customer", kind: "Normalized", columns: [{ name: "name", dataType: "Text", isRequired: true }] },
      ]);
    }
    if (path === "/api-destination-profiles") {
      return json(route, [{
        id: "customer-api",
        displayName: "Customer API",
        enabled: true,
        method: "POST",
        deliveryPathTemplate: "/customers",
        environment: "Stub",
        allowedHeaderNames: [],
        supportedContracts: [{ id: "proposal", version: "1", reference: "proposal/1" }],
        payloadBindings: [],
      }]);
    }
    if (path === "/mapping-capabilities") return json(route, capabilities);
    if (path === "/mapping-configs/11" && request.method() === "GET") {
      return json(route, {
        id: 11,
        name: "customer-json",
        inputFolder: "customer-input",
        sourceTableId: 7,
        normalizedTableId: 8,
        activeVersionId: 102,
        destination,
        versions: [version],
      });
    }
    if (path === "/file-destination-profiles/operations-json/resolve") {
      return json(route, {
        contract: { ...destination.contract, schemaHash: "sha256:file-schema" },
        schemaHash: "sha256:file-schema",
        fetchedAt: "2026-10-26T00:00:00Z",
        paths: [
          { path: "customer", kind: "Object", isAssignable: false, isRequired: true, isNullable: false, scalarType: null },
          { path: "customer.name", kind: "Scalar", isAssignable: true, isRequired: true, isNullable: false, scalarType: "Text" },
          { path: "customer.birthDate", kind: "Scalar", isAssignable: true, isRequired: false, isNullable: false, scalarType: "Date" },
        ],
      });
    }
    if (path === "/mapping-configs/11/versions/preview") {
      onPreview(request.postDataJSON() as Record<string, unknown>);
      return json(route, {
        evaluationAt: "2026-10-26T00:00:00Z",
        records: [{
          rowKey: "preview-1",
          rawRecord: null,
          rowNumber: 1,
          physicalLineNumber: 1,
          action: "Import",
          recordRule: null,
          sourceValues: { name: "Alice" },
          transformation: { rowKey: "preview-1", status: "Done", outputs: [{ outputKey: "main", values: { customer: { name: "Alice" } } }], errors: [] },
        }],
        jsonFile: {
          profileId: "operations-json",
          contractReference: "proposal/1",
          schemaHash: "sha256:file-schema",
          formatting: "indented",
          relativeDisplayPath: "exports/customers/sample.1.json",
          finalJson: JSON.stringify([{ customer: { name: "Alice" } }], null, 2),
          objectCount: 1,
          estimatedUtf8Bytes: 58,
          diagnostics: [],
        },
      });
    }
    if (path === "/mapping-configs/11/versions/preview-file") {
      onFilePreview?.(request.postData() ?? "");
      return json(route, {
        evaluationAt: "2026-10-26T00:00:00Z",
        records: [],
        workbook: {
          sheets: ["MTLSPA"],
          selectedSheet: "MTLSPA",
          headers: ["name"],
        },
        jsonFile: {
          profileId: "operations-json",
          contractReference: "proposal/1",
          schemaHash: "sha256:file-schema",
          formatting: "indented",
          relativeDisplayPath: "exports/customers/MTLSPA.1.json",
          finalJson: "[]",
          objectCount: 0,
          estimatedUtf8Bytes: 2,
          diagnostics: [],
        },
      });
    }
    if (path === "/mapping-configs/11/versions" && request.method() === "POST") {
      onClone(request.postDataJSON() as Record<string, unknown>);
      return json(route, { ...version, id: 103, versionNo: 3, isActive: false });
    }
    return route.fulfill({ status: 404, body: "not mocked" });
  });
}

async function mockActivationRollback(page: Page, activations: number[]) {
  let activeVersionNo = 2;
  const inputLayout = {
    kind: "delimited",
    encoding: "utf-8",
    delimiter: ",",
    preambleLines: 0,
    headerPolicy: "SingleRow",
    fields: [{ inputHeader: "name", sourceColumn: "name" }],
  };
  const transformationProgram = {
    languageVersion: 2,
    semanticsProfile: "sqlserver-legacy-v1",
    outputs: [{ outputKey: "main", fields: [], arrayGroups: [] }],
  };
  const jsonDestination = {
    kind: "jsonFile",
    fileDestinationProfileId: "operations-json",
    contract: {
      id: "proposal",
      version: "1",
      reference: "proposal/1",
      schemaHash: "sha256:file-schema",
    },
    relativeFolder: "exports/customers",
    fileNameTemplate: "{sourceBaseName}.{artifactRevision}.json",
    formatting: "compact",
  };
  const legacyDestination = {
    kind: "legacyTable",
    normalizedTableId: 8,
  };

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(/^\/api/, "");
    if (path === "/tables") {
      return json(route, [
        { id: 7, name: "source_customer", kind: "Source", columns: [{ name: "name", dataType: "Text", isRequired: true }] },
        { id: 8, name: "normalized_customer", kind: "Normalized", columns: [{ name: "name", dataType: "Text", isRequired: true }] },
      ]);
    }
    if (path === "/api-destination-profiles") return json(route, []);
    if (path === "/mapping-capabilities") return json(route, capabilities);
    if (path === "/mapping-configs/11" && request.method() === "GET") {
      const versions = [
        {
          id: 103,
          versionNo: 3,
          isActive: activeVersionNo === 3,
          inputLayout,
          transformationProgram,
          destination: jsonDestination,
          acceptedExtensions: [".csv"],
          contractSnapshot: null,
        },
        {
          id: 102,
          versionNo: 2,
          isActive: activeVersionNo === 2,
          inputLayout,
          transformationProgram,
          destination: legacyDestination,
          acceptedExtensions: [".csv"],
          contractSnapshot: null,
        },
      ];
      return json(route, {
        id: 11,
        name: "customer-export",
        inputFolder: "customer-input",
        sourceTableId: 7,
        normalizedTableId: activeVersionNo === 2 ? 8 : null,
        activeVersionId: activeVersionNo === 3 ? 103 : 102,
        destination: activeVersionNo === 3 ? jsonDestination : legacyDestination,
        versions,
      });
    }
    if (path === "/file-destination-profiles/operations-json/resolve") {
      return json(route, {
        contract: jsonDestination.contract,
        schemaHash: "sha256:file-schema",
        fetchedAt: "2026-10-26T00:00:00Z",
        paths: [],
      });
    }
    const activation = path.match(
      /^\/mapping-configs\/11\/versions\/(\d+)\/activate$/,
    );
    if (activation && request.method() === "POST") {
      activeVersionNo = Number(activation[1]);
      activations.push(activeVersionNo);
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 404, body: "not mocked" });
  });
}

const capabilities = {
  input: {
    formats: ["delimited", "fixedWidth", "excel"],
    encodings: ["utf-8"],
    positionUnits: ["Utf16CodeUnit"],
  },
  transformation: {
    languageVersions: [1, 2],
    semanticsProfiles: [
      { id: "sqlserver-legacy-v1", immutable: true, summary: "Legacy" },
    ],
    targetConversionTypes: ["text"],
    inputFormats: {
      date: ["yyyy-MM-dd", "dd/MM/yyyy", "yyyyMMdd"],
      time: ["HH:mm:ss", "HH:mm"],
      dateTime: ["yyyy-MM-ddTHH:mm:ss"],
    },
    valueExpressions: ["source"],
    booleanOperators: ["eq"],
    functions: [],
    lookups: [],
    metadataFields: ["fileName"],
    limits: {
      maxOutputs: 32,
      maxFieldsPerOutput: 256,
      maxCollectionItems: 256,
      maxExpressionDepth: 32,
      maxExpressionNodes: 2048,
    },
  },
  destinations: {
    kinds: ["legacyTable", "apiContract", "jsonFile"],
    jsonFile: {
      formatting: ["compact", "indented"],
      fileNameTokens: ["{sourceBaseName}", "{artifactRevision}"],
      maxPreviewBytes: 1048576,
      maxPreviewRecords: 50,
      maxObjectBytes: 1048576,
      maxObjectsPerArtifact: 320000,
      maxArtifactBytes: 268435456,
      profiles: [
        {
          id: "operations-json",
          enabled: true,
          supportedContracts: [
            { id: "proposal", version: "1", reference: "proposal/1" },
          ],
          payloadBindings: [
            { targetPath: "fileJobId", source: "FileJobId" },
          ],
        },
      ],
    },
  },
};

function json(route: Route, body: unknown) {
  return route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify(body),
  });
}
