"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { VersionCompare } from "@/components/contracts/version-compare";
import { MappingWorkbench } from "@/components/mapping/mapping-workbench";
import { ErrorPanel, LoadingPanel } from "@/components/page-state";
import type {
  MappingCapabilities,
  ApiDestinationProfile,
  MappingConfigDetails,
  MappingConfigVersionDetails,
  TableDefinition,
} from "@/lib/api/contracts";
import {
  activateMappingConfigVersion,
  createMappingConfigVersion,
  getMappingCapabilities,
  getMappingConfig,
  listApiDestinationProfiles,
  listTables,
} from "@/lib/api/mapping-configs";

export function ConfigDetails() {
  const params = useParams<{ id: string }>();
  const configId = Number(params.id);
  const hasValidConfigId = Number.isSafeInteger(configId) && configId > 0;
  const [config, setConfig] = useState<MappingConfigDetails | null>(null);
  const [tables, setTables] = useState<TableDefinition[]>([]);
  const [capabilities, setCapabilities] =
    useState<MappingCapabilities | null>(null);
  const [apiProfiles, setApiProfiles] = useState<ApiDestinationProfile[]>([]);
  const [editorVersionId, setEditorVersionId] = useState<number | null>(null);
  const [compareVersionId, setCompareVersionId] = useState<number | null>(null);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [actionError, setActionError] = useState<unknown>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;

    if (!hasValidConfigId) {
      return;
    }

    Promise.all([
      getMappingConfig(configId),
      listTables(),
      getMappingCapabilities(),
      listApiDestinationProfiles(),
    ])
      .then(([details, tableRows, capabilityRows, profileRows]) => {
        if (!cancelled) {
          setConfig(details);
          setTables(tableRows);
          setCapabilities(capabilityRows);
          setApiProfiles(profileRows);
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
  }, [configId, hasValidConfigId, reloadKey]);

  const tableNames = useMemo(
    () => new Map(tables.map((table) => [table.id, table.name])),
    [tables],
  );

  function reload() {
    setLoading(config === null);
    setReloadKey((current) => current + 1);
  }

  async function cloneVersion(version: MappingConfigVersionDetails) {
    const actionKey = `clone:${version.id}`;
    setBusyAction(actionKey);
    setActionError(null);
    setNotice(null);

    try {
      const created = await createMappingConfigVersion(configId, {
        inputLayout: version.inputLayout,
        transformationProgram: version.transformationProgram,
        destination: version.destination,
        acceptedExtensions: version.acceptedExtensions,
      });
      setNotice(
        `Version ${version.versionNo} was cloned as version ${created.versionNo}.`,
      );
      setReloadKey((current) => current + 1);
    } catch (error) {
      setActionError(error);
    } finally {
      setBusyAction(null);
    }
  }

  async function activateVersion(version: MappingConfigVersionDetails) {
    const actionKey = `activate:${version.id}`;
    setBusyAction(actionKey);
    setActionError(null);
    setNotice(null);

    try {
      await activateMappingConfigVersion(configId, version.versionNo);
      setNotice(`Version ${version.versionNo} is now active.`);
      setReloadKey((current) => current + 1);
    } catch (error) {
      setActionError(error);
    } finally {
      setBusyAction(null);
    }
  }

  if (!hasValidConfigId) {
    return (
      <main className="mx-auto w-full max-w-7xl px-6 py-10 lg:px-8">
        <ErrorPanel
          error={new Error("The configuration id is invalid.")}
          title="Could not load configuration"
        />
      </main>
    );
  }

  if (loading && config === null) {
    return (
      <main className="mx-auto w-full max-w-7xl px-6 py-10 lg:px-8">
        <LoadingPanel label="Loading configuration…" />
      </main>
    );
  }

  if (loadError || config === null) {
    return (
      <main className="mx-auto w-full max-w-7xl px-6 py-10 lg:px-8">
        <Link
          className="mb-5 inline-block text-sm font-semibold text-indigo-700 hover:text-indigo-900"
          href="/configs"
        >
          ← Back to configurations
        </Link>
        <ErrorPanel
          error={loadError ?? new Error("Configuration was not found.")}
          onRetry={reload}
          title="Could not load configuration"
        />
      </main>
    );
  }

  const versions = [...config.versions].sort(
    (left, right) => right.versionNo - left.versionNo,
  );
  const sourceTable = tables.find((table) => table.id === config.sourceTableId);
  const editorVersion =
    versions.find((version) => version.id === editorVersionId) ?? versions[0];
  const outputCount = versions.reduce(
    (total, version) =>
      total + version.transformationProgram.outputs.length,
    0,
  );
  const ruleCount = versions.reduce(
    (total, version) =>
      total +
      (version.inputLayout.kind === "fixedWidth"
        ? version.inputLayout.recordRules.length
        : 0),
    0,
  );

  return (
    <main className="mx-auto w-full max-w-7xl px-6 py-10 lg:px-8">
      <Link
        className="text-sm font-semibold text-indigo-700 hover:text-indigo-900"
        href="/configs"
      >
        ← Back to configurations
      </Link>

      <div className="mt-5 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-3xl font-semibold tracking-tight text-slate-950">
              {config.name}
            </h1>
            <span
              className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
                config.activeVersionId
                  ? "bg-emerald-100 text-emerald-800"
                  : "bg-amber-100 text-amber-800"
              }`}
            >
              {config.activeVersionId ? "Active" : "No active version"}
            </span>
          </div>
          <p className="mt-2 font-mono text-sm text-slate-500">
            input/{config.inputFolder}
          </p>
        </div>
        <button
          className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 transition hover:bg-slate-50"
          onClick={reload}
          type="button"
        >
          Refresh
        </button>
      </div>

      <section className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[
          ["Versions", versions.length],
          ["Outputs", outputCount],
          ["Record rules", ruleCount],
          [
            "Active version",
            versions.find((version) => version.isActive)?.versionNo ?? "—",
          ],
        ].map(([label, value]) => (
          <div
            className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
            key={label}
          >
            <p className="text-sm font-medium text-slate-500">{label}</p>
            <p className="mt-2 text-3xl font-semibold tracking-tight text-slate-950">
              {value}
            </p>
          </div>
        ))}
      </section>

      <section className="mt-6 grid gap-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:grid-cols-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
            Source table
          </p>
          <p className="mt-2 font-semibold text-slate-900">
            {tableNames.get(config.sourceTableId) ?? `#${config.sourceTableId}`}
          </p>
        </div>
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
            Destination
          </p>
          <p className="mt-2 font-semibold text-slate-900">
            {config.destination?.kind === "apiContract"
              ? `API Contract · ${config.destination.contract.reference}`
              : config.destination?.kind === "jsonFile"
                ? `JSON File · ${config.destination.contract.reference}`
                : config.normalizedTableId === null
                  ? "Legacy Table · not selected"
                  : `Legacy Table · ${tableNames.get(config.normalizedTableId) ?? `#${config.normalizedTableId}`}`}
          </p>
        </div>
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
            Config id
          </p>
          <p className="mt-2 font-mono text-sm font-semibold text-slate-900">
            {config.id}
          </p>
        </div>
      </section>

      {notice ? (
        <div
          className="mt-6 rounded-2xl border border-emerald-200 bg-emerald-50 px-5 py-4 text-sm font-medium text-emerald-900"
          role="status"
        >
          {notice}
        </div>
      ) : null}
      {actionError ? (
        <div className="mt-6">
          <ErrorPanel error={actionError} title="Version action failed" />
        </div>
      ) : null}

      {sourceTable && capabilities ? (
        <MappingWorkbench
          apiProfiles={apiProfiles}
          capabilities={capabilities}
          configDestination={config.destination}
          configId={config.id}
          key={editorVersion?.id ?? `new:${config.id}`}
          normalizedTableId={config.normalizedTableId}
          onVersionCreated={(created) => {
            setEditorVersionId(created.id);
            setNotice(`Version ${created.versionNo} was created from the mapping workbench.`);
            setActionError(null);
            setReloadKey((current) => current + 1);
          }}
          sourceTable={sourceTable}
          tables={tables}
          version={editorVersion}
        />
      ) : (
        <div className="mt-8">
          <ErrorPanel
            error={new Error(
              "The source table or mapping capabilities could not be loaded.",
            )}
            title="Could not open Input Layout editor"
          />
        </div>
      )}

      <div className="mt-8">
        <h2 className="text-xl font-semibold tracking-tight text-slate-950">
          Version history
        </h2>
        <p className="mt-1 text-sm text-slate-600">
          Open an immutable version as a draft, then create a new version after
          editing. Clone is the unchanged shortcut.
        </p>
      </div>

      <section className="mt-4 space-y-4">
        {versions.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-14 text-center">
            <h3 className="font-semibold text-slate-950">No versions yet</h3>
            <p className="mt-2 text-sm text-slate-600">
              Use the draft editor above to prepare the first version.
            </p>
          </div>
        ) : (
          versions.map((version, index) => {
            const previous = versions[index + 1];
            const inputSummary =
              version.inputLayout.kind === "fixedWidth"
                ? `${version.inputLayout.recordRules.length} record rules`
                : `${version.inputLayout.fields.length} delimited fields`;
            const assignmentCount =
              version.transformationProgram.outputs.reduce(
                (total, output) => total + output.fields.length,
                0,
              );
            return (
              <article
                className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
                key={version.id}
              >
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <h3 className="text-lg font-semibold text-slate-950">
                        Version {version.versionNo}
                      </h3>
                      {version.isActive ? (
                        <span className="rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-semibold text-emerald-800">
                          Active
                        </span>
                      ) : null}
                    </div>
                    <p className="mt-1 text-sm text-slate-500">
                      {version.destination.kind === "legacyTable"
                        ? `Legacy Table #${version.destination.normalizedTableId}`
                        : version.destination.kind === "apiContract"
                          ? `API Contract · ${version.destination.contract.reference}`
                          : `JSON File · ${version.destination.contract.reference}`}
                      {" · "}
                      {version.inputLayout.kind === "fixedWidth"
                        ? "Fixed width"
                        : "Delimited"}
                      {" · "}
                      {inputSummary}
                    </p>
                    {version.contractSnapshot ? (
                      <p className="mt-2 font-mono text-xs text-slate-500">
                        {version.contractSnapshot.schemaHash} · {version.contractSnapshot.paths.filter((path) => path.isAssignable).length} assignable paths · {version.contractSnapshot.payloadBindings.length} system bindings
                      </p>
                    ) : null}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <button
                      className={`rounded-lg border px-3 py-2 text-sm font-semibold transition ${
                        editorVersion?.id === version.id
                          ? "border-indigo-300 bg-indigo-50 text-indigo-800"
                          : "border-slate-300 text-slate-700 hover:bg-slate-50"
                      }`}
                      onClick={() => {
                        setEditorVersionId(version.id);
                        requestAnimationFrame(() =>
                          document
                            .getElementById("input-layout-editor")
                            ?.scrollIntoView({ behavior: "smooth" }),
                        );
                      }}
                      type="button"
                    >
                      {editorVersion?.id === version.id
                        ? "Open in editor"
                        : "Edit layout"}
                    </button>
                    {previous ? (
                      <button
                        aria-expanded={compareVersionId === version.id}
                        className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-50"
                        onClick={() =>
                          setCompareVersionId((current) =>
                            current === version.id ? null : version.id,
                          )
                        }
                        type="button"
                      >
                        {compareVersionId === version.id
                          ? "Hide comparison"
                          : `Compare with v${previous.versionNo}`}
                      </button>
                    ) : null}
                    <button
                      className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                      disabled={busyAction !== null}
                      onClick={() => cloneVersion(version)}
                      type="button"
                    >
                      {busyAction === `clone:${version.id}`
                        ? "Cloning…"
                        : "Clone version"}
                    </button>
                    {!version.isActive ? (
                      <button
                        className="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-indigo-700 disabled:cursor-not-allowed disabled:bg-indigo-300"
                        disabled={busyAction !== null}
                        onClick={() => activateVersion(version)}
                        type="button"
                      >
                        {busyAction === `activate:${version.id}`
                          ? "Activating…"
                          : "Activate"}
                      </button>
                    ) : null}
                  </div>
                </div>

                <div className="mt-5 grid gap-4 border-t border-slate-100 pt-5 sm:grid-cols-3">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                      Outputs
                    </p>
                    <p className="mt-1 text-lg font-semibold text-slate-900">
                      {version.transformationProgram.outputs.length}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                      Field assignments
                    </p>
                    <p className="mt-1 text-lg font-semibold text-slate-900">
                      {assignmentCount}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                      Output keys
                    </p>
                    <p className="mt-1 truncate font-mono text-sm font-semibold text-slate-900">
                      {version.transformationProgram.outputs
                        .map((output) => output.outputKey)
                        .join(", ") || "—"}
                    </p>
                  </div>
                </div>
                {previous && compareVersionId === version.id ? (
                  <VersionCompare current={version} previous={previous} />
                ) : null}
              </article>
            );
          })
        )}
      </section>
    </main>
  );
}
