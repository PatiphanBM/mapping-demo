"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ColumnEditor } from "@/components/tables/column-editor";
import { ApiError } from "@/lib/api/client";
import type {
  ColumnDefinition,
  TableAdminCapabilities,
  TableDefinition,
  TableReference,
  TableSchemaVerification,
  TableVerificationStatus,
} from "@/lib/api/contracts";
import {
  addAdminTableColumn,
  getAdminTable,
  getLatestTableVerification,
  getTableAdminCapabilities,
  getTableReferences,
  verifyAdminTable,
} from "@/lib/api/tables";

const emptyColumn = (): ColumnDefinition => ({
  name: "",
  dataType: "Text",
  isRequired: false,
  length: null,
  precision: null,
  scale: null,
});

export default function TableDetailClient({ tableId }: { tableId: number }) {
  const [table, setTable] = useState<TableDefinition | null>(null);
  const [verification, setVerification] =
    useState<TableSchemaVerification | null>(null);
  const [references, setReferences] = useState<TableReference[]>([]);
  const [capabilities, setCapabilities] =
    useState<TableAdminCapabilities | null>(null);
  const [loading, setLoading] = useState(true);
  const [verifying, setVerifying] = useState(false);
  const [adding, setAdding] = useState(false);
  const [showAdd, setShowAdd] = useState(false);
  const [column, setColumn] = useState<ColumnDefinition>(emptyColumn());
  const [operatorId, setOperatorId] = useState("local-table-admin");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  const dirty = showAdd && (column.name !== "" || reason !== "");
  useEffect(() => {
    const guard = (event: BeforeUnloadEvent) => {
      if (!dirty) return;
      event.preventDefault();
    };
    window.addEventListener("beforeunload", guard);
    return () => window.removeEventListener("beforeunload", guard);
  }, [dirty]);

  useEffect(() => {
    let active = true;
    async function loadInitial() {
      try {
        const [loadedTable, loadedVerification, loadedReferences, permissions] =
          await Promise.all([
            getAdminTable(tableId),
            getLatestTableVerification(tableId),
            getTableReferences(tableId),
            getTableAdminCapabilities(),
          ]);
        if (!active) return;
        setTable(loadedTable);
        setVerification(loadedVerification ?? null);
        setReferences(loadedReferences);
        setCapabilities(permissions);
        if (
          (loadedTable.verificationStatus ?? "Unknown") === "Unknown"
        ) {
          const result = await verifyAdminTable(
            loadedTable.id,
            "local-table-admin",
          );
          if (!active) return;
          setVerification(result);
          setTable({
            ...loadedTable,
            verificationStatus: result.status,
            verificationCheckedAt: result.checkedAt,
          });
        }
      } catch (caught) {
        if (active) setError(message(caught));
      } finally {
        if (active) setLoading(false);
      }
    }
    void loadInitial();
    return () => {
      active = false;
    };
  }, [tableId]);

  async function verify(value = table) {
    if (!value) return;
    setVerifying(true);
    setError(null);
    try {
      const result = await verifyAdminTable(value.id, operatorId.trim());
      setVerification(result);
      setTable({
        ...value,
        verificationStatus: result.status,
        verificationCheckedAt: result.checkedAt,
      });
    } catch (caught) {
      setError(message(caught));
    } finally {
      setVerifying(false);
    }
  }

  async function addColumn(event: React.FormEvent) {
    event.preventDefault();
    if (!column.name.trim() || !operatorId.trim() || !reason.trim()) {
      setError("Column name, operator ID and reason are required.");
      return;
    }
    setAdding(true);
    setError(null);
    try {
      await addAdminTableColumn(
        tableId,
        column,
        operatorId.trim(),
        reason.trim(),
      );
      const refreshed = await getAdminTable(tableId);
      setTable(refreshed);
      setColumn(emptyColumn());
      setReason("");
      setShowAdd(false);
      await verify(refreshed);
    } catch (caught) {
      setError(message(caught));
    } finally {
      setAdding(false);
    }
  }

  if (loading && !table) {
    return (
      <main className="mx-auto max-w-7xl px-6 py-8 lg:px-8">
        <p role="status">Loading table details…</p>
      </main>
    );
  }

  if (!table) {
    return (
      <main className="mx-auto max-w-7xl px-6 py-8 lg:px-8">
        <div role="alert" className="rounded-xl border border-rose-300 bg-rose-50 p-4 text-rose-800">
          {error ?? "Table not found."}
        </div>
        <Link className="mt-4 inline-block text-indigo-700" href="/admin/tables">
          Back to Table Catalog
        </Link>
      </main>
    );
  }

  const businessNames = new Set(table.columns.map((value) => value.name));
  const systemColumns = verification?.expected.columns.filter(
    (value) => !businessNames.has(value.name),
  ) ?? [];

  return (
    <main className="mx-auto max-w-7xl space-y-6 px-6 py-8 lg:px-8">
      <div>
        <Link className="text-sm font-semibold text-indigo-700 hover:underline" href="/admin/tables">
          ← Table Catalog
        </Link>
        <div className="mt-3 flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="font-mono text-xs text-slate-500">Table #{table.id}</p>
            <h1 className="text-3xl font-bold tracking-tight">{table.name}</h1>
            <p className="mt-1 text-slate-600">{table.kind} · expand-only managed table</p>
          </div>
          <div className="flex items-center gap-3">
            <StatusBadge status={verification?.status ?? table.verificationStatus ?? "Unknown"} />
            <button
              type="button"
              disabled={verifying}
              className="rounded-lg border border-indigo-300 px-4 py-2 font-semibold text-indigo-700 disabled:opacity-50"
              onClick={() => void verify()}
            >
              {verifying ? "Verifying…" : "Verify now"}
            </button>
            {capabilities?.canAddColumn ? (
              <button
                type="button"
                className="rounded-lg bg-indigo-600 px-4 py-2 font-semibold text-white"
                onClick={() => setShowAdd((value) => !value)}
              >
                Add column
              </button>
            ) : null}
          </div>
        </div>
      </div>

      {error ? (
        <div role="alert" className="rounded-xl border border-rose-300 bg-rose-50 p-4 text-rose-800">
          {error}
        </div>
      ) : null}
      {!capabilities?.mutationsEnabled && capabilities ? (
        <div role="status" className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-amber-900">
          Mutations are paused; schema verification remains read-only.
        </div>
      ) : null}

      {showAdd ? (
        <form
          aria-label="Add column"
          className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
          onSubmit={addColumn}
        >
          <div>
            <h2 className="text-xl font-semibold">Add one column</h2>
            <p className="text-sm text-slate-500">
              Existing columns, types and historical Config Versions are not changed.
            </p>
          </div>
          <ColumnEditor
            column={column}
            index={0}
            kind={table.kind}
            compact
            onChange={setColumn}
          />
          <div className="grid gap-4 md:grid-cols-2">
            <label className="text-sm font-medium text-slate-700">
              Operator ID
              <input
                className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2"
                value={operatorId}
                onChange={(event) => setOperatorId(event.target.value)}
              />
            </label>
            <label className="text-sm font-medium text-slate-700">
              Reason
              <input
                className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2"
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                placeholder="Approved additive schema change"
              />
            </label>
          </div>
          <button
            type="submit"
            disabled={adding}
            className="rounded-lg bg-indigo-600 px-4 py-2 font-semibold text-white disabled:opacity-50"
          >
            {adding ? "Adding…" : "Add column and verify"}
          </button>
        </form>
      ) : null}

      <section className="grid gap-6 lg:grid-cols-2">
        <Panel title="Business columns">
          <ColumnTable columns={table.columns.map((value) => ({
            name: value.name,
            dataType: displayType(value),
            detail: value.isRequired ? "Required mapping value" : "Optional mapping value",
          }))} />
        </Panel>
        <Panel title="Generated system columns">
          {systemColumns.length ? (
            <ColumnTable columns={systemColumns.map((value) => ({
              name: value.name,
              dataType: value.dataType,
              detail: `${value.isNullable ? "nullable" : "not null"}${value.isIdentity ? " · identity" : ""}`,
            }))} />
          ) : (
            <p className="text-sm text-slate-500">Verify to load expected system columns.</p>
          )}
        </Panel>
      </section>

      <Panel title="Physical schema verification">
        {verification ? (
          <div className="space-y-4">
            <div className="flex flex-wrap gap-x-6 gap-y-2 text-sm text-slate-600">
              <span>Checked {new Date(verification.checkedAt).toLocaleString()}</span>
              <span>{verification.durationMilliseconds.toFixed(1)} ms</span>
              <span>Primary key: {verification.expected.primaryKey.columns.join(", ")}</span>
              <span>
                Unique: {verification.expected.uniqueConstraints.map((value) => value.columns.join(" + ")).join("; ")}
              </span>
            </div>
            {verification.diagnostics.length === 0 ? (
              <p role="status" className="rounded-lg bg-emerald-50 p-4 text-emerald-800">
                Metadata and physical PostgreSQL schema match.
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead className="border-b border-slate-200 text-slate-500">
                    <tr><th className="p-2">Field</th><th className="p-2">Issue</th><th className="p-2">Expected</th><th className="p-2">Actual</th></tr>
                  </thead>
                  <tbody>
                    {verification.diagnostics.map((diagnostic, index) => (
                      <tr key={`${diagnostic.code}-${index}`} className="border-b border-slate-100">
                        <td className="p-2 font-mono text-xs">{diagnostic.field}</td>
                        <td className="p-2"><span className="font-semibold">{diagnostic.code}</span><br />{diagnostic.message}</td>
                        <td className="p-2">{diagnostic.expected ?? "—"}</td>
                        <td className="p-2">{diagnostic.actual ?? "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        ) : (
          <p className="text-sm text-slate-500">No verification result has been recorded.</p>
        )}
      </Panel>

      <Panel title="Configuration impact">
        {references.length === 0 ? (
          <p className="text-sm text-slate-500">No Mapping Config or Config Version references this table.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-200 text-slate-500">
                <tr><th className="p-2">Configuration</th><th className="p-2">Usage</th><th className="p-2">Version</th><th className="p-2">Pinned jobs</th></tr>
              </thead>
              <tbody>
                {references.map((reference, index) => (
                  <tr key={`${reference.configId}-${reference.configVersionId}-${index}`} className="border-b border-slate-100">
                    <td className="p-2"><Link className="text-indigo-700 hover:underline" href={`/configs/${reference.configId}`}>{reference.configName}</Link></td>
                    <td className="p-2">{reference.usage}</td>
                    <td className="p-2">{reference.versionNo ? `v${reference.versionNo}${reference.isActiveVersion ? " · active" : ""}` : "No versions"}</td>
                    <td className="p-2">{reference.pinnedJobCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <p className="text-sm text-slate-500">
        Drift detection is read-only. Repair Missing or Drifted objects through the approved operations runbook after impact analysis and backup.
      </p>
    </main>
  );
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="mb-4 text-lg font-semibold">{title}</h2>
      {children}
    </section>
  );
}

function ColumnTable({ columns }: { columns: Array<{ name: string; dataType: string; detail: string }> }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-slate-200 text-slate-500"><tr><th className="p-2">Name</th><th className="p-2">Type</th><th className="p-2">Rules</th></tr></thead>
        <tbody>{columns.map((column) => <tr key={column.name} className="border-b border-slate-100"><td className="p-2 font-mono text-xs">{column.name}</td><td className="p-2">{column.dataType}</td><td className="p-2">{column.detail}</td></tr>)}</tbody>
      </table>
    </div>
  );
}

function StatusBadge({ status }: { status: TableVerificationStatus }) {
  const colors = status === "Healthy" ? "bg-emerald-100 text-emerald-800" : status === "Drifted" || status === "Missing" ? "bg-rose-100 text-rose-800" : status === "VerificationFailed" ? "bg-amber-100 text-amber-900" : "bg-slate-100 text-slate-700";
  return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${colors}`}>{status}</span>;
}

function displayType(column: ColumnDefinition) {
  if (column.dataType === "Text" && column.length) return `Text(${column.length})`;
  if (column.dataType === "Decimal" && column.precision !== null && column.precision !== undefined) return `Decimal(${column.precision}${column.scale !== null && column.scale !== undefined ? `,${column.scale}` : ""})`;
  return column.dataType;
}

function message(error: unknown) {
  if (error instanceof ApiError) {
    const validation = Object.values(error.errors).flat().join(" ");
    return validation || error.detail || error.title;
  }
  return error instanceof Error ? error.message : "The request failed.";
}
