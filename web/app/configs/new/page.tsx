"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { type FormEvent, useEffect, useMemo, useState } from "react";
import {
  ErrorPanel,
  FieldMessages,
  LoadingPanel,
} from "@/components/page-state";
import {
  DatabaseTargetSelector,
  EMPTY_DATABASE_TARGET_SELECTION,
  type DatabaseTargetSelection,
} from "@/components/mapping/database-target-selector";
import { ApiError } from "@/lib/api/client";
import type {
  ApiDestinationProfile,
  MappingCapabilities,
  TableDefinition,
  DatabaseDestinationProfile,
} from "@/lib/api/contracts";
import {
  createMappingConfig,
  getMappingCapabilities,
  listApiDestinationProfiles,
  listTables,
  resolveApiContract,
  resolveFileContract,
  listDatabaseDestinationProfiles,
} from "@/lib/api/mapping-configs";

type FormField =
  | "name"
  | "inputFolder"
  | "sourceTableId"
  | "normalizedTableId"
  | "apiDestinationProfileId"
  | "fileDestinationProfileId"
  | "contractReference"
  | "relativeFolder"
  | "fileNameTemplate"
  | "destination";
type FormErrors = Partial<Record<FormField, string[]>>;

const fieldClassName =
  "mt-2 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm text-slate-950 shadow-sm outline-none transition focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100";

export default function NewConfigPage() {
  const router = useRouter();
  const [tables, setTables] = useState<TableDefinition[]>([]);
  const [profiles, setProfiles] = useState<ApiDestinationProfile[]>([]);
  const [databaseProfiles, setDatabaseProfiles] = useState<DatabaseDestinationProfile[]>([]);
  const [capabilities, setCapabilities] =
    useState<MappingCapabilities | null>(null);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [submitError, setSubmitError] = useState<unknown>(null);
  const [fieldErrors, setFieldErrors] = useState<FormErrors>({});
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const [name, setName] = useState("");
  const [inputFolder, setInputFolder] = useState("");
  const [sourceTableId, setSourceTableId] = useState("");
  const [normalizedTableId, setNormalizedTableId] = useState("");
  const [destinationKind, setDestinationKind] = useState<
    "legacyTable" | "apiContract" | "jsonFile" | "databaseTable"
  >("legacyTable");
  const [profileId, setProfileId] = useState("");
  const [fileProfileId, setFileProfileId] = useState("");
  const [contractReference, setContractReference] = useState("");
  const [relativeFolder, setRelativeFolder] = useState("exports");
  const [fileNameTemplate, setFileNameTemplate] = useState(
    "{sourceBaseName}.{artifactRevision}.json",
  );
  const [formatting, setFormatting] = useState<"compact" | "indented">(
    "compact",
  );
  const [databaseTarget, setDatabaseTarget] = useState<DatabaseTargetSelection>(
    EMPTY_DATABASE_TARGET_SELECTION,
  );
  const showApiContractDestination =
    process.env.NEXT_PUBLIC_API_CONTRACT_DESTINATION_VISIBLE !== "false";

  useEffect(() => {
    let cancelled = false;

    Promise.all([
      listTables(),
      listApiDestinationProfiles(),
      getMappingCapabilities(),
      listDatabaseDestinationProfiles(),
    ])
      .then(([rows, profileRows, capabilityRows, databaseProfileRows]) => {
        if (!cancelled) {
          setTables(rows);
          setProfiles(profileRows);
          setCapabilities(capabilityRows);
          setDatabaseProfiles(databaseProfileRows);
          setLoadError(null);
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setLoadError(error);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [reloadKey]);

  const sourceTables = useMemo(
    () => tables.filter((table) => table.kind === "Source"),
    [tables],
  );
  const normalizedTables = useMemo(
    () => tables.filter((table) => table.kind === "Normalized"),
    [tables],
  );
  const selectedProfile = profiles.find((profile) => profile.id === profileId);
  const selectedContract = selectedProfile?.supportedContracts.find(
    (contract) => contract.reference === contractReference,
  );
  const fileProfiles = capabilities?.destinations.jsonFile.profiles ?? [];
  const selectedFileProfile = fileProfiles.find(
    (profile) => profile.id === fileProfileId,
  );
  const selectedFileContract = selectedFileProfile?.supportedContracts.find(
    (contract) => contract.reference === contractReference,
  );

  function selectDestination(
    kind: "legacyTable" | "apiContract" | "jsonFile" | "databaseTable",
  ) {
    setDestinationKind(kind);
    setFieldErrors({});
    setNormalizedTableId("");
    setProfileId("");
    setFileProfileId("");
    setContractReference("");
    setRelativeFolder("exports");
    setFileNameTemplate("{sourceBaseName}.{artifactRevision}.json");
    setFormatting("compact");
    setDatabaseTarget(EMPTY_DATABASE_TARGET_SELECTION);
  }

  function retry() {
    setLoading(true);
    setReloadKey((current) => current + 1);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const clientErrors: FormErrors = {};

    if (name.trim().length === 0) clientErrors.name = ["Name is required."];
    if (inputFolder.trim().length === 0) {
      clientErrors.inputFolder = ["Input folder is required."];
    }
    if (sourceTableId.length === 0) {
      clientErrors.sourceTableId = ["Choose a Source table."];
    }
    if (destinationKind === "legacyTable" && normalizedTableId.length === 0) {
      clientErrors.normalizedTableId = ["Choose a Normalized table."];
    }
    if (destinationKind === "apiContract" && profileId.length === 0) {
      clientErrors.apiDestinationProfileId = ["Choose an API destination profile."];
    }
    if (destinationKind === "apiContract" && !selectedContract) {
      clientErrors.contractReference = ["Choose a supported contract."];
    }
    if (destinationKind === "jsonFile" && fileProfileId.length === 0) {
      clientErrors.fileDestinationProfileId = [
        "Choose a File destination profile.",
      ];
    }
    if (destinationKind === "jsonFile" && !selectedFileContract) {
      clientErrors.contractReference = ["Choose a supported contract."];
    }
    if (destinationKind === "jsonFile" && relativeFolder.trim().length === 0) {
      clientErrors.relativeFolder = ["Relative folder is required."];
    }
    if (destinationKind === "databaseTable" && !databaseTarget.schemaSnapshot) {
      clientErrors.destination = ["Choose a database profile, schema and table."];
    }
    if (
      destinationKind === "jsonFile" &&
      fileNameTemplate.trim().length === 0
    ) {
      clientErrors.fileNameTemplate = ["File name template is required."];
    }

    if (Object.keys(clientErrors).length > 0) {
      setFieldErrors(clientErrors);
      setSubmitError(null);
      return;
    }

    setSubmitting(true);
    setSubmitError(null);
    setFieldErrors({});

    try {
      const resolvedContract = destinationKind === "apiContract"
        ? (await resolveApiContract(profileId, {
            ...selectedContract!,
            schemaHash: "",
          })).contract
        : destinationKind === "jsonFile"
          ? (await resolveFileContract(fileProfileId, {
              ...selectedFileContract!,
              schemaHash: "",
            })).contract
          : null;
      const destination = destinationKind === "legacyTable"
        ? {
            kind: "legacyTable" as const,
            normalizedTableId: Number(normalizedTableId),
          }
        : destinationKind === "apiContract"
          ? {
            kind: "apiContract" as const,
            apiDestinationProfileId: profileId,
            contract: resolvedContract!,
          }
          : destinationKind === "jsonFile" ? {
              kind: "jsonFile" as const,
              fileDestinationProfileId: fileProfileId,
              contract: resolvedContract!,
              relativeFolder: relativeFolder.trim(),
              fileNameTemplate: fileNameTemplate.trim(),
              formatting,
            } : {
              kind: "databaseTable" as const,
              profileId: databaseTarget.schemaSnapshot!.profileId,
              schema: databaseTarget.schemaSnapshot!.schema,
              table: databaseTarget.schemaSnapshot!.table,
              writeMode: "insert" as const,
              duplicatePolicy: databaseTarget.duplicateKeyColumns.length > 0
                ? { kind: "existingUniqueKey" as const, columns: databaseTarget.duplicateKeyColumns }
                : { kind: "manualUnknownOutcome" as const },
              schemaHash: databaseTarget.schemaSnapshot!.schemaHash,
              schemaSnapshot: databaseTarget.schemaSnapshot!,
            };
      const config = await createMappingConfig({
        name: name.trim(),
        inputFolder: inputFolder.trim(),
        sourceTableId: Number(sourceTableId),
        normalizedTableId:
          destinationKind === "legacyTable" ? Number(normalizedTableId) : null,
        destination,
      });
      router.push(`/configs/${config.id}`);
    } catch (error) {
      setSubmitError(error);
      setFieldErrors(toFormErrors(error));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-6 py-10 lg:px-8">
      <Link
        className="text-sm font-semibold text-indigo-700 hover:text-indigo-900"
        href="/configs"
      >
        ← Back to configurations
      </Link>
      <div className="mt-5">
        <p className="text-sm font-semibold text-indigo-600">Configuration</p>
        <h1 className="mt-1 text-3xl font-semibold tracking-tight text-slate-950">
          Create a mapping config
        </h1>
        <p className="mt-2 text-sm leading-6 text-slate-600">
          Connect an input folder and Source table to a versioned destination.
          Choose one immutable delivery destination for the first version.
        </p>
      </div>

      {loading ? (
        <div className="mt-8">
          <LoadingPanel label="Loading table catalog…" />
        </div>
      ) : loadError ? (
        <div className="mt-8">
          <ErrorPanel
            error={loadError}
            onRetry={retry}
            title="Could not load the table catalog"
          />
        </div>
      ) : (
        <form
          className="mt-8 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8"
          onSubmit={submit}
        >
          {submitError ? (
            <div className="mb-6">
              <ErrorPanel error={submitError} title="Could not create config" />
            </div>
          ) : null}

          <div className="grid gap-6 sm:grid-cols-2">
            <label className="block text-sm font-semibold text-slate-800">
              Name
              <input
                aria-invalid={(fieldErrors.name?.length ?? 0) > 0}
                className={fieldClassName}
                onChange={(event) => setName(event.target.value)}
                placeholder="orders-demo"
                value={name}
              />
              <FieldMessages messages={fieldErrors.name ?? []} />
            </label>
            <fieldset className="sm:col-span-2">
              <legend className="text-sm font-semibold text-slate-800">
                Destination
              </legend>
              <div className="mt-2 grid gap-3 lg:grid-cols-3">
                <label className={`rounded-xl border p-4 text-sm ${destinationKind === "legacyTable" ? "border-indigo-300 bg-indigo-50" : "border-slate-200 bg-white"}`}>
                  <input checked={destinationKind === "legacyTable"} name="destination-kind" onChange={() => selectDestination("legacyTable")} type="radio" />
                  <span className="ml-2 font-semibold text-indigo-950">
                    Legacy Table
                  </span>
                  <span className="mt-1 block text-xs text-indigo-700">
                    Existing normalized-table upsert and reconciliation.
                  </span>
                </label>
                {showApiContractDestination ? (
                  <label className={`rounded-xl border p-4 text-sm ${destinationKind === "apiContract" ? "border-indigo-300 bg-indigo-50" : "border-slate-200 bg-white"}`}>
                    <input checked={destinationKind === "apiContract"} name="destination-kind" onChange={() => selectDestination("apiContract")} type="radio" />
                    <span className="ml-2 font-semibold">One record at a time · API Contract</span>
                    <span className="mt-1 block text-xs">
                      Nested contract mapping with server-owned delivery policy.
                    </span>
                  </label>
                ) : null}
                <label className={`rounded-xl border p-4 text-sm ${destinationKind === "jsonFile" ? "border-indigo-300 bg-indigo-50" : "border-slate-200 bg-white"}`}>
                  <input checked={destinationKind === "jsonFile"} name="destination-kind" onChange={() => selectDestination("jsonFile")} type="radio" />
                  <span className="ml-2 font-semibold">Whole file · JSON file</span>
                  <span className="mt-1 block text-xs">
                    Assemble all valid mapped objects into one JSON array.
                  </span>
                </label>
                <label className={`rounded-xl border p-4 text-sm ${destinationKind === "databaseTable" ? "border-indigo-300 bg-indigo-50" : "border-slate-200 bg-white"}`}>
                  <input checked={destinationKind === "databaseTable"} name="destination-kind" onChange={() => selectDestination("databaseTable")} type="radio" />
                  <span className="ml-2 font-semibold">External database table</span>
                  <span className="mt-1 block text-xs">Inspect PostgreSQL or SQL Server fields dynamically.</span>
                </label>
              </div>
              <FieldMessages messages={fieldErrors.destination ?? []} />
            </fieldset>
            <label className="block text-sm font-semibold text-slate-800">
              Input folder
              <input
                aria-invalid={(fieldErrors.inputFolder?.length ?? 0) > 0}
                className={fieldClassName}
                onChange={(event) => setInputFolder(event.target.value)}
                placeholder="orders"
                value={inputFolder}
              />
              <FieldMessages messages={fieldErrors.inputFolder ?? []} />
            </label>
            {destinationKind === "apiContract" ? (
              <div className="grid gap-4 sm:col-span-2 sm:grid-cols-2">
                <label className="block text-sm font-semibold text-slate-800">
                  API destination profile
                  <select className={fieldClassName} onChange={(event) => {
                    setProfileId(event.target.value);
                    setContractReference("");
                  }} value={profileId}>
                    <option value="">Choose a profile</option>
                    {profiles.map((profile) => (
                      <option disabled={!profile.enabled} key={profile.id} value={profile.id}>
                        {profile.displayName}{profile.enabled ? "" : " (disabled)"}
                      </option>
                    ))}
                  </select>
                  <FieldMessages messages={fieldErrors.apiDestinationProfileId ?? []} />
                </label>
                <label className="block text-sm font-semibold text-slate-800">
                  Contract version
                  <select className={fieldClassName} disabled={!selectedProfile} onChange={(event) => setContractReference(event.target.value)} value={contractReference}>
                    <option value="">Choose a contract</option>
                    {(selectedProfile?.supportedContracts ?? []).map((contract) => (
                      <option key={contract.reference} value={contract.reference}>
                        {contract.id} · v{contract.version}
                      </option>
                    ))}
                  </select>
                  <FieldMessages messages={fieldErrors.contractReference ?? []} />
                  <p className="mt-2 text-xs font-normal text-slate-500">Logical IDs only. URLs, credentials and secret references stay on the server.</p>
                </label>
              </div>
            ) : null}
            {destinationKind === "jsonFile" ? (
              <div className="grid gap-4 sm:col-span-2 sm:grid-cols-2">
                <label className="block text-sm font-semibold text-slate-800">
                  File destination profile
                  <select
                    aria-invalid={(fieldErrors.fileDestinationProfileId?.length ?? 0) > 0}
                    className={fieldClassName}
                    onChange={(event) => {
                      setFileProfileId(event.target.value);
                      setContractReference("");
                    }}
                    value={fileProfileId}
                  >
                    <option value="">Choose a profile</option>
                    {fileProfiles.map((profile) => (
                      <option disabled={!profile.enabled} key={profile.id} value={profile.id}>
                        {profile.id}{profile.enabled ? "" : " (disabled)"}
                      </option>
                    ))}
                  </select>
                  <FieldMessages messages={fieldErrors.fileDestinationProfileId ?? []} />
                </label>
                <label className="block text-sm font-semibold text-slate-800">
                  Contract version
                  <select
                    className={fieldClassName}
                    disabled={!selectedFileProfile}
                    onChange={(event) => setContractReference(event.target.value)}
                    value={contractReference}
                  >
                    <option value="">Choose a contract</option>
                    {(selectedFileProfile?.supportedContracts ?? []).map((contract) => (
                      <option key={contract.reference} value={contract.reference}>
                        {contract.id} · v{contract.version}
                      </option>
                    ))}
                  </select>
                  <FieldMessages messages={fieldErrors.contractReference ?? []} />
                </label>
                <label className="block text-sm font-semibold text-slate-800">
                  Relative folder
                  <input className={fieldClassName} onChange={(event) => setRelativeFolder(event.target.value)} value={relativeFolder} />
                  <FieldMessages messages={fieldErrors.relativeFolder ?? []} />
                </label>
                <label className="block text-sm font-semibold text-slate-800">
                  File name template
                  <input className={fieldClassName} onChange={(event) => setFileNameTemplate(event.target.value)} value={fileNameTemplate} />
                  <FieldMessages messages={fieldErrors.fileNameTemplate ?? []} />
                </label>
                <label className="block text-sm font-semibold text-slate-800">
                  JSON formatting
                  <select className={fieldClassName} onChange={(event) => setFormatting(event.target.value as "compact" | "indented")} value={formatting}>
                    {(capabilities?.destinations.jsonFile.formatting ?? []).map((value) => (
                      <option key={value} value={value}>{value}</option>
                    ))}
                  </select>
                </label>
                <dl className="grid grid-cols-2 gap-3 rounded-xl border border-slate-200 bg-slate-50 p-4 text-xs">
                  <div><dt className="text-slate-500">Payload root</dt><dd className="mt-1 font-semibold text-slate-900">array</dd></div>
                  <div><dt className="text-slate-500">Encoding</dt><dd className="mt-1 font-semibold text-slate-900">UTF-8 · no BOM</dd></div>
                  <div><dt className="text-slate-500">Failure scope</dt><dd className="mt-1 font-semibold text-slate-900">Whole file</dd></div>
                  <div><dt className="text-slate-500">Visible root</dt><dd className="mt-1 break-all font-mono text-slate-900">{relativeFolder || "—"}</dd></div>
                </dl>
              </div>
            ) : null}
            {destinationKind === "databaseTable" ? (
              <DatabaseTargetSelector
                onChange={setDatabaseTarget}
                profiles={databaseProfiles}
                value={databaseTarget}
              />
            ) : null}
            <label className="block text-sm font-semibold text-slate-800">
              Source table
              <select
                aria-invalid={(fieldErrors.sourceTableId?.length ?? 0) > 0}
                className={fieldClassName}
                onChange={(event) => setSourceTableId(event.target.value)}
                value={sourceTableId}
              >
                <option value="">Choose a Source table</option>
                {sourceTables.map((table) => (
                  <option key={table.id} value={table.id}>
                    {table.name} ({table.columns.length} columns)
                  </option>
                ))}
              </select>
              <FieldMessages messages={fieldErrors.sourceTableId ?? []} />
            </label>
            {destinationKind === "legacyTable" ? (
              <label className="block text-sm font-semibold text-slate-800">
              Normalized table
              <select
                aria-invalid={
                  (fieldErrors.normalizedTableId?.length ?? 0) > 0
                }
                className={fieldClassName}
                onChange={(event) => setNormalizedTableId(event.target.value)}
                value={normalizedTableId}
              >
                <option value="">Choose a Normalized table</option>
                {normalizedTables.map((table) => (
                  <option key={table.id} value={table.id}>
                    {table.name} ({table.columns.length} columns)
                  </option>
                ))}
              </select>
              <FieldMessages messages={fieldErrors.normalizedTableId ?? []} />
              </label>
            ) : null}
          </div>

          <div className="mt-8 flex items-center justify-end gap-3 border-t border-slate-200 pt-6">
            <Link
              className="rounded-xl px-4 py-2.5 text-sm font-semibold text-slate-600 transition hover:bg-slate-100"
              href="/configs"
            >
              Cancel
            </Link>
            <button
              className="rounded-xl bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-indigo-700 disabled:cursor-not-allowed disabled:bg-indigo-300"
              disabled={submitting}
              type="submit"
            >
              {submitting ? "Creating…" : "Create config"}
            </button>
          </div>
        </form>
      )}
    </main>
  );
}

function toFormErrors(error: unknown): FormErrors {
  if (!(error instanceof ApiError)) {
    return {};
  }

  const result: FormErrors = {};
  for (const [path, messages] of Object.entries(error.errors)) {
    const field = toFormField(path);
    if (field !== null) {
      result[field] = [...(result[field] ?? []), ...messages];
    }
  }

  if (error.status === 409) {
    const message = error.detail ?? error.title;
    result.name = [...(result.name ?? []), message];
    result.inputFolder = [...(result.inputFolder ?? []), message];
  }

  return result;
}

function toFormField(path: string): FormField | null {
  const normalized = path
    .replace(/^\$\./, "")
    .replace(/^request\./i, "")
    .toLowerCase();
  const fields: Record<string, FormField> = {
    name: "name",
    inputfolder: "inputFolder",
    sourcetableid: "sourceTableId",
    normalizedtableid: "normalizedTableId",
    apidestinationprofileid: "apiDestinationProfileId",
    filedestinationprofileid: "fileDestinationProfileId",
    contractreference: "contractReference",
    relativefolder: "relativeFolder",
    filenametemplate: "fileNameTemplate",
  };
  return normalized.startsWith("destination")
    ? "destination"
    : (fields[normalized] ?? null);
}
