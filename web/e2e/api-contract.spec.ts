import { expect, test, type Page, type Route } from "@playwright/test";

const sourceTable = {
  id: 7,
  name: "source_contract",
  kind: "Source",
  columns: [
    { name: "full_name", dataType: "Text", isRequired: true },
    { name: "birth_date", dataType: "Text", isRequired: true },
    { name: "pa_number", dataType: "Text", isRequired: true },
    { name: "party_name_1", dataType: "Text", isRequired: false },
    { name: "party_name_2", dataType: "Text", isRequired: false },
  ],
};

const profiles = [
  {
    id: "pa-mtls-inform",
    displayName: "PA MTLS Inform",
    enabled: true,
    method: "POST",
    deliveryPathTemplate: "/informs/{contractVersion}",
    allowedHeaderNames: ["X-Correlation-Id", "Idempotency-Key"],
    supportedContracts: [{ id: "pa-inform", version: "1", reference: "pa-inform/1" }],
    payloadBindings: [{ targetPath: "outputKey", source: "OutputKey" }],
  },
  {
    id: "pa-mtls",
    displayName: "PA MTLS Proposal",
    enabled: true,
    method: "POST",
    deliveryPathTemplate: "/proposals/{contractVersion}",
    allowedHeaderNames: ["X-Correlation-Id", "Idempotency-Key"],
    supportedContracts: [{ id: "proposal", version: "1", reference: "proposal/1" }],
    payloadBindings: [{ targetPath: "envelope.fileJobId", source: "FileJobId" }],
  },
  {
    id: "claims-envelope",
    displayName: "Claims Envelope v2",
    enabled: true,
    method: "PUT",
    deliveryPathTemplate: "/cases/{contractVersion}",
    allowedHeaderNames: ["X-Request-Id", "Idempotency-Key"],
    supportedContracts: [{ id: "claims", version: "2", reference: "claims/2" }],
    payloadBindings: [{ targetPath: "meta.batch", source: "FileJobId" }],
  },
];

const capabilities = {
  input: { formats: ["delimited"], encodings: ["utf-8"], positionUnits: ["Utf16CodeUnit"] },
  transformation: {
    languageVersions: [1, 2],
    semanticsProfiles: [{ id: "sqlserver-legacy-v1", immutable: true, summary: "Legacy-compatible" }],
    targetConversionTypes: ["date", "time", "integer", "boolean"],
    inputFormats: { date: ["yyyy-MM-dd"], time: ["HH:mm:ss"], dateTime: ["yyyy-MM-ddTHH:mm:ss"] },
    valueExpressions: ["source", "literal"],
    booleanOperators: ["eq"],
    functions: [],
    lookups: [],
    metadataFields: ["fileName"],
    limits: { maxOutputs: 32, maxFieldsPerOutput: 256, maxCollectionItems: 256, maxExpressionDepth: 32, maxExpressionNodes: 2048 },
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
      profiles: [],
    },
  },
};

const goldenPayloads = {
  "pa-mtls": {
    envelope: { fileJobId: "preview-file-0" },
    proposal: { insured: { fullName: "Alice" } },
  },
  "claims-envelope": {
    meta: { batch: "preview-file-0" },
    case: { claimNo: "Alice" },
  },
  "pa-mtls-inform": {
    outputKey: "default",
    inform: { partnerCode: "MTLS", sourceReference: "PA001" },
  },
};

type ProfileId = keyof typeof goldenPayloads;

test("destination form switches to logical API profile and clears Legacy controls", async ({ page }) => {
  await mockApi(page, "pa-mtls");
  await page.goto("/configs/new");

  await page.getByRole("radio", { name: /One record at a time/ }).check();
  await page.getByLabel("API destination profile").selectOption("pa-mtls");
  await page.getByLabel("Contract version").selectOption("proposal/1");

  await expect(page.getByLabel("Normalized table")).toHaveCount(0);
  await expect(page.getByText("Logical IDs only.", { exact: false })).toBeVisible();
  await expect(page.getByText(/https:\/\//)).toHaveCount(0);
  await expect(page.getByText(/credential/i)).toHaveCount(1);
});

test("create resolves and submits the API contract schema hash", async ({ page }) => {
  let submitted: Record<string, unknown> | null = null;
  await mockApi(page, "pa-mtls", (body) => {
    submitted = body;
  });
  await page.goto("/configs/new");

  await page.getByLabel("Name", { exact: true }).fill("pa-contract");
  await page.getByLabel("Input folder").fill("pa-input");
  await page.getByLabel("Source table").selectOption("7");
  await page.getByRole("radio", { name: /One record at a time/ }).check();
  await page.getByLabel("API destination profile").selectOption("pa-mtls");
  await page.getByLabel("Contract version").selectOption("proposal/1");
  await page.getByRole("button", { name: "Create config" }).click();

  await expect.poll(() => submitted).not.toBeNull();
  expect(submitted).toMatchObject({
    destination: {
      kind: "apiContract",
      apiDestinationProfileId: "pa-mtls",
      contract: {
        id: "proposal",
        version: "1",
        reference: "proposal/1",
        schemaHash: "sha256:test-pa-mtls",
      },
    },
  });
});

for (const profileId of ["pa-mtls", "claims-envelope"] as const) {
  test(`${profileId} resolves paths and previews without sending`, async ({ page }) => {
    const requestedApiPaths: string[] = [];
    const partnerRequests: string[] = [];
    page.on("request", (request) => {
      const url = new URL(request.url());
      if (url.pathname.startsWith("/api/")) requestedApiPaths.push(url.pathname);
      if (url.hostname.includes("delivery.mapping-demo")) {
        partnerRequests.push(request.url());
      }
    });
    await mockApi(page, profileId);
    await page.goto("/configs/11");

    await expect(page.getByRole("heading", { name: profiles.find((item) => item.id === profileId)!.displayName })).toBeVisible();
    await expect(page.getByText(`sha256:test-${profileId}`, { exact: false }).first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Create version" })).toBeVisible();
    await expect(page.getByRole("button", { name: /send/i })).toHaveCount(0);
    await expect(page.getByRole("tab")).toHaveCount(5);

    const businessPath = profileId === "pa-mtls"
      ? "proposal.insured.fullName"
      : "case.claimNo";
    const row = page.getByRole("row").filter({ hasText: businessPath });
    await row.getByRole("combobox").selectOption("full_name");
    await page.getByRole("button", { name: "Preview" }).click();

    await page.getByRole("tab", { name: "Input" }).click();
    await expect(page.getByLabel("Input preview JSON")).toContainText('"kind": "sourceRows"');

    await page.getByRole("tab", { name: "Source" }).click();
    await expect(page.getByLabel("Source row preview JSON")).toContainText('"full_name": "Alice"');

    await page.getByRole("tab", { name: "Mapped" }).click();
    await expect(page.getByLabel("Mapped document preview JSON")).toHaveText(
      JSON.stringify([
        {
          rowKey: "preview-1",
          outputKey: "main",
          document: mappedDocument(profileId),
        },
      ], null, 2),
    );

    await page.getByRole("tab", { name: "Delivery" }).click();
    await expect(page.getByText("Endpoint profile")).toBeVisible();
    await expect(page.getByText(profileId, { exact: true })).toBeVisible();
    await expect(page.getByText(`${profiles.find((item) => item.id === profileId)!.method} ${profiles.find((item) => item.id === profileId)!.deliveryPathTemplate}`, { exact: true })).toBeVisible();
    await expect(page.getByText(`sha256:test-${profileId}`, { exact: true })).toBeVisible();
    await expect(page.getByText("preview-only:11:row:output", { exact: true })).toBeVisible();
    await expect(page.getByText("Preview only — no request sent", { exact: true })).toBeVisible();
    await expect(page.getByLabel("Delivery payload preview-1 main")).toHaveText(
      JSON.stringify(goldenPayloads[profileId], null, 2),
    );
    for (const header of profiles.find((item) => item.id === profileId)!.allowedHeaderNames) {
      await expect(page.getByText(header, { exact: true })).toBeVisible();
    }

    await page.getByRole("tab", { name: "Diagnostics" }).click();
    await expect(page.getByText("No diagnostics for this preview.")).toBeVisible();
    await expect(page.getByText(/secret|delivery\.example/i)).toHaveCount(0);
    expect(partnerRequests).toEqual([]);
    expect(requestedApiPaths.filter((path) => /delivery-jobs|outbox|\/send$/.test(path))).toEqual([]);
  });
}

test("version compare shows destination, contract, paths, groups and bindings", async ({ page }) => {
  await mockApi(page, "pa-mtls");
  await page.goto("/configs/11");

  await page.getByRole("button", { name: "Compare with v1" }).click();
  const comparison = page.getByRole("region", {
    name: "Compare version 2 with version 1",
  });

  await expect(comparison).toContainText("Legacy Table #44");
  await expect(comparison).toContainText("API Contract · pa-mtls");
  await expect(comparison).toContainText("proposal/1");
  await expect(comparison).toContainText("sha256:test-pa-mtls");
  await expect(comparison).toContainText("main: proposal.insured.fullName");
  await expect(comparison).toContainText("main: proposal.beneficiaries · seq=seq · slots=1,2");
  await expect(comparison).toContainText("envelope.fileJobId ← FileJobId");
});

test("PA MTLS starter replaces stale proposal mappings and uses the contract output key", async ({ page }) => {
  const submissions: Record<string, unknown>[] = [];
  await mockApi(page, "pa-mtls-inform", (body) => {
    submissions.push(body);
  });
  await page.goto("/configs/11");

  await page.getByLabel("API destination profile").selectOption("pa-mtls-inform");
  await page.getByLabel("Contract version").selectOption("pa-inform/1");
  await page.getByRole("button", { name: "Apply PA MTLS starter mapping" }).click();
  await page.getByRole("button", { name: "Create version" }).click();

  await expect.poll(() => submissions.length).toBe(1);
  const program = (submissions[0] as { transformationProgram: {
    outputs: Array<{ outputKey: string; fields: Array<{ targetPath: string; value: unknown }> }>;
  } }).transformationProgram;
  expect(program.outputs[0].outputKey).toBe("default");
  expect(program.outputs[0].fields).toEqual(expect.arrayContaining([
    { targetPath: "inform.partnerCode", value: { kind: "literal", type: "text", value: "MTLS" } },
    { targetPath: "inform.sourceReference", value: { kind: "source", field: "pa_number" } },
  ]));
  expect(program.outputs[0].fields.some((field) => field.targetPath.startsWith("proposal."))).toBe(false);
});

async function mockApi(
  page: Page,
  profileId: ProfileId,
  onCreate?: (body: Record<string, unknown>) => void,
) {
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(/^\/api/, "");
    const profile = profiles.find((item) => item.id === profileId)!;
    if (path === "/tables") return json(route, [sourceTable]);
    if (path === "/api-destination-profiles") return json(route, profiles);
    if (path === "/database-destinations/profiles") return json(route, []);
    if (path === "/mapping-capabilities") return json(route, capabilities);
    if (path === "/mapping-configs" && request.method() === "POST") {
      onCreate?.(request.postDataJSON() as Record<string, unknown>);
      return json(route, { id: 11 });
    }
    if (path === "/mapping-configs/11") {
      return json(route, {
        id: 11,
        name: `${profileId}-config`,
        inputFolder: `${profileId}-input`,
        sourceTableId: 7,
        normalizedTableId: null,
        activeVersionId: null,
        destination: destination(profile),
        versions: versions(profile),
      });
    }
    if (path.endsWith("/resolve")) return json(route, resolveResponse(profile));
    if (path === "/mapping-configs/11/versions/preview") {
      return json(route, {
        evaluationAt: "2026-10-20T00:00:00Z",
        records: [{
          rowKey: "preview-1",
          rawRecord: null,
          rowNumber: 1,
          physicalLineNumber: 1,
          action: "Import",
          recordRule: null,
          sourceValues: { full_name: "Alice" },
          transformation: { rowKey: "preview-1", status: "Done", outputs: [{ outputKey: "main", values: mappedDocument(profileId) }], errors: [] },
        }],
        contract: {
          profileId,
          contractReference: profile.supportedContracts[0].reference,
          schemaHash: `sha256:test-${profileId}`,
          method: profile.method,
          pathTemplate: profile.deliveryPathTemplate,
          allowedHeaderNames: profile.allowedHeaderNames,
          idempotencyKeyExample: "preview-only:11:row:output",
          sendsRequest: false,
          outputs: [{ rowKey: "preview-1", outputKey: "main", payloadJson: JSON.stringify(goldenPayloads[profileId]), payloadHash: `sha256:payload-${profileId}`, diagnostics: [] }],
        },
      });
    }
    if (path === "/mapping-configs/11/versions" && request.method() === "POST") {
      const body = request.postDataJSON() as Record<string, unknown>;
      onCreate?.(body);
      return json(route, { id: 103, configId: 11, versionNo: 3, ...body });
    }
    return route.fulfill({ status: 404, body: "not mocked" });
  });
}

function destination(profile: (typeof profiles)[number]) {
  const contract = profile.supportedContracts[0];
  return {
    kind: "apiContract",
    apiDestinationProfileId: profile.id,
    contract: { ...contract, schemaHash: "" },
  };
}

function resolveResponse(profile: (typeof profiles)[number]) {
  if (profile.id === "pa-mtls-inform") {
    return {
      contract: { ...profile.supportedContracts[0], schemaHash: "sha256:test-pa-mtls-inform" },
      schemaHash: "sha256:test-pa-mtls-inform",
      fetchedAt: "2026-10-20T00:00:00Z",
      paths: [
        { path: "outputKey", kind: "Scalar", isAssignable: true, isRequired: true, isNullable: false, scalarType: "Text", constraints: { pattern: "^default$" } },
        { path: "inform", kind: "Object", isAssignable: false, isRequired: true, isNullable: false, scalarType: null },
        { path: "inform.partnerCode", kind: "Scalar", isAssignable: true, isRequired: true, isNullable: false, scalarType: "Text" },
        { path: "inform.sourceReference", kind: "Scalar", isAssignable: true, isRequired: true, isNullable: false, scalarType: "Text" },
        { path: "inform.saleId", kind: "Scalar", isAssignable: true, isRequired: true, isNullable: true, scalarType: "Text" },
      ],
      requestSummary: { method: profile.method, pathTemplate: profile.deliveryPathTemplate, allowedHeaderNames: profile.allowedHeaderNames },
    };
  }
  const pa = profile.id === "pa-mtls";
  const businessRoot = pa ? "proposal" : "case";
  const businessPath = pa ? "proposal.insured.fullName" : "case.claimNo";
  const systemPath = pa ? "envelope.fileJobId" : "meta.batch";
  const arrayPath = pa ? "proposal.beneficiaries" : "case.parties";
  return {
    contract: { ...profile.supportedContracts[0], schemaHash: `sha256:test-${profile.id}` },
    schemaHash: `sha256:test-${profile.id}`,
    fetchedAt: "2026-10-20T00:00:00Z",
    paths: [
      { path: businessRoot, kind: "Object", isAssignable: false, isRequired: true, isNullable: false, scalarType: null },
      { path: businessPath, kind: "Scalar", isAssignable: true, isRequired: true, isNullable: false, scalarType: "Text", description: "Business identifier" },
      { path: systemPath, kind: "Scalar", isAssignable: true, isRequired: true, isNullable: false, scalarType: "Text" },
      { path: arrayPath, kind: "Array", isAssignable: false, isRequired: true, isNullable: false, scalarType: null },
      { path: `${arrayPath}[]`, kind: "Object", isAssignable: false, isRequired: true, isNullable: false, scalarType: null },
      { path: `${arrayPath}[].seq`, kind: "Scalar", isAssignable: true, isRequired: true, isNullable: false, scalarType: "Integer" },
      { path: `${arrayPath}[].fullName`, kind: "Scalar", isAssignable: true, isRequired: true, isNullable: false, scalarType: "Text" },
    ],
    requestSummary: { method: profile.method, pathTemplate: profile.deliveryPathTemplate, allowedHeaderNames: profile.allowedHeaderNames },
  };
}

function mappedDocument(profileId: ProfileId) {
  return profileId === "pa-mtls"
    ? { proposal: { insured: { fullName: "Alice" } } }
    : profileId === "pa-mtls-inform"
      ? { inform: { partnerCode: "MTLS", sourceReference: "PA001" } }
    : { case: { claimNo: "Alice" } };
}

function versions(profile: (typeof profiles)[number]) {
  if (profile.id === "pa-mtls-inform") {
    const proposalProfile = profiles.find((item) => item.id === "pa-mtls")!;
    return [{
      id: 102,
      versionNo: 2,
      isActive: false,
      inputLayout: inputLayout(),
      transformationProgram: {
        languageVersion: 2,
        semanticsProfile: "sqlserver-legacy-v1",
        outputs: [{
          outputKey: "main",
          fields: [{ targetPath: "proposal.insured.fullName", value: { kind: "source", field: "full_name" } }],
          arrayGroups: [],
        }],
      },
      destination: destination(proposalProfile),
      acceptedExtensions: [".csv"],
      contractSnapshot: null,
    }];
  }
  const pa = profile.id === "pa-mtls";
  const businessPath = pa ? "proposal.insured.fullName" : "case.claimNo";
  const arrayPath = pa ? "proposal.beneficiaries" : "case.parties";
  const sequencePath = "seq";
  return [
    {
      id: 102,
      versionNo: 2,
      isActive: true,
      inputLayout: inputLayout(),
      transformationProgram: {
        languageVersion: 2,
        semanticsProfile: "sqlserver-legacy-v1",
        outputs: [{
          outputKey: "main",
          fields: [{ targetPath: businessPath, value: { kind: "source", field: "full_name" } }],
          arrayGroups: [{
            targetPath: arrayPath,
            sequencePath,
            emptyItemPolicy: "SkipWhenAllBusinessFieldsEmpty",
            sourceSlots: [
              { sourceSlot: 1, fields: [{ targetPath: "fullName", value: { kind: "source", field: "party_name_1" } }] },
              { sourceSlot: 2, fields: [{ targetPath: "fullName", value: { kind: "source", field: "party_name_2" } }] },
            ],
          }],
        }],
      },
      destination: destination(profile),
      acceptedExtensions: [".csv"],
      contractSnapshot: {
        canonicalSchema: "{}",
        schemaHash: `sha256:test-${profile.id}`,
        fetchedAt: "2026-10-20T00:00:00Z",
        paths: resolveResponse(profile).paths,
        payloadBindings: profile.payloadBindings,
      },
    },
    {
      id: 101,
      versionNo: 1,
      isActive: false,
      inputLayout: inputLayout(),
      transformationProgram: {
        languageVersion: 1,
        semanticsProfile: "sqlserver-legacy-v1",
        outputs: [{
          outputKey: "default",
          fields: [{ normalizedColumn: "legacy_customer_name", value: { kind: "source", field: "full_name" } }],
          arrayGroups: [],
        }],
      },
      destination: { kind: "legacyTable", normalizedTableId: 44 },
      acceptedExtensions: [".csv"],
      contractSnapshot: null,
    },
  ];
}

function inputLayout() {
  return {
    kind: "delimited",
    encoding: "utf-8",
    delimiter: ",",
    preambleLines: 0,
    headerPolicy: "SingleRow",
    fields: sourceTable.columns.map((column) => ({
      inputHeader: column.name,
      sourceColumn: column.name,
    })),
  };
}

function json(route: Route, body: unknown) {
  return route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify(body),
  });
}
