"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { ErrorPanel, LoadingPanel } from "@/components/page-state";
import type {
  MappingConfigDetails,
  TableDefinition,
} from "@/lib/api/contracts";
import {
  getMappingConfig,
  listMappingConfigs,
  listTables,
} from "@/lib/api/mapping-configs";

export default function ConfigsPage() {
  const [configs, setConfigs] = useState<MappingConfigDetails[]>([]);
  const [tables, setTables] = useState<TableDefinition[]>([]);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(true);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const [configRows, tableRows] = await Promise.all([
          listMappingConfigs(),
          listTables(),
        ]);
        const details = await Promise.all(
          configRows.map((config) => getMappingConfig(config.id)),
        );

        if (!cancelled) {
          setConfigs(details);
          setTables(tableRows);
          setError(null);
        }
      } catch (loadError) {
        if (!cancelled) {
          setConfigs([]);
          setError(loadError);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [reloadKey]);

  const tableNames = useMemo(
    () => new Map(tables.map((table) => [table.id, table.name])),
    [tables],
  );
  const versionCount = configs.reduce(
    (total, config) => total + config.versions.length,
    0,
  );
  const outputCount = configs.reduce(
    (total, config) =>
      total +
      config.versions.reduce(
        (versionTotal, version) =>
          versionTotal + version.transformationProgram.outputs.length,
        0,
      ),
    0,
  );
  const ruleCount = configs.reduce(
    (total, config) =>
      total +
      config.versions.reduce(
        (versionTotal, version) =>
          versionTotal +
          (version.inputLayout.kind === "fixedWidth"
            ? version.inputLayout.recordRules.length
            : 0),
        0,
      ),
    0,
  );

  function retry() {
    setLoading(true);
    setReloadKey((current) => current + 1);
  }

  return (
    <main className="mx-auto w-full max-w-7xl px-6 py-10 lg:px-8">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm font-semibold text-indigo-600">Control plane</p>
          <h1 className="mt-1 text-3xl font-semibold tracking-tight text-slate-950">
            Mapping configurations
          </h1>
          <p className="mt-2 text-sm leading-6 text-slate-600">
            Review version history, input rules, outputs, and activation state.
          </p>
        </div>
        <Link
          className="rounded-xl bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-indigo-700"
          href="/configs/new"
        >
          Create configuration
        </Link>
      </div>

      {loading ? (
        <div className="mt-8">
          <LoadingPanel label="Loading configurations…" />
        </div>
      ) : error ? (
        <div className="mt-8">
          <ErrorPanel
            error={error}
            onRetry={retry}
            title="Could not load configurations"
          />
        </div>
      ) : (
        <>
          <section className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {[
              ["Configurations", configs.length],
              ["Versions", versionCount],
              ["Outputs", outputCount],
              ["Record rules", ruleCount],
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

          <section className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
            {configs.length === 0 ? (
              <div className="px-6 py-16 text-center">
                <h2 className="font-semibold text-slate-950">
                  No configurations yet
                </h2>
                <p className="mt-2 text-sm text-slate-600">
                  Create a configuration to connect Source and Normalized tables.
                </p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      <th className="px-6 py-3 font-semibold">Configuration</th>
                      <th className="px-6 py-3 font-semibold">Tables</th>
                      <th className="px-6 py-3 font-semibold">Versions</th>
                      <th className="px-6 py-3 font-semibold">Outputs</th>
                      <th className="px-6 py-3 font-semibold">Rules</th>
                      <th className="px-6 py-3 text-right font-semibold">
                        Status
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {configs.map((config) => {
                      const outputs = config.versions.reduce(
                        (total, version) =>
                          total + version.transformationProgram.outputs.length,
                        0,
                      );
                      const rules = config.versions.reduce(
                        (total, version) =>
                          total +
                          (version.inputLayout.kind === "fixedWidth"
                            ? version.inputLayout.recordRules.length
                            : 0),
                        0,
                      );

                      return (
                        <tr className="hover:bg-slate-50" key={config.id}>
                          <td className="px-6 py-4">
                            <Link
                              className="font-semibold text-indigo-700 hover:text-indigo-900 hover:underline"
                              href={`/configs/${config.id}`}
                            >
                              {config.name}
                            </Link>
                            <p className="mt-1 font-mono text-xs text-slate-500">
                              {config.inputFolder}
                            </p>
                          </td>
                          <td className="px-6 py-4 text-slate-600">
                            <p>
                              {tableNames.get(config.sourceTableId) ??
                                `#${config.sourceTableId}`}
                            </p>
                            <p className="mt-1 text-xs text-slate-400">
                              →{" "}
                              {tableNames.get(config.normalizedTableId) ??
                                `#${config.normalizedTableId}`}
                            </p>
                          </td>
                          <td className="px-6 py-4 font-medium text-slate-700">
                            {config.versions.length}
                          </td>
                          <td className="px-6 py-4 font-medium text-slate-700">
                            {outputs}
                          </td>
                          <td className="px-6 py-4 font-medium text-slate-700">
                            {rules}
                          </td>
                          <td className="px-6 py-4 text-right">
                            <span
                              className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${
                                config.activeVersionId
                                  ? "bg-emerald-100 text-emerald-800"
                                  : "bg-amber-100 text-amber-800"
                              }`}
                            >
                              {config.activeVersionId ? "Active" : "Draft only"}
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
    </main>
  );
}
