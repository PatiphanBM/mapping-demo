"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ColumnEditor } from "@/components/tables/column-editor";
import { ApiError } from "@/lib/api/client";
import type {
  ColumnDefinition,
  TableAdminCapabilities,
  TableDefinition,
  TableKind,
  TableVerificationStatus,
} from "@/lib/api/contracts";
import {
  createAdminTable,
  getTableAdminCapabilities,
  listAdminTables,
} from "@/lib/api/tables";

const statuses: Array<TableVerificationStatus | ""> = [
  "",
  "Unknown",
  "Verifying",
  "Healthy",
  "Drifted",
  "Missing",
  "VerificationFailed",
];

const emptyColumn = (): ColumnDefinition => ({
  name: "",
  dataType: "Text",
  isRequired: false,
  length: null,
  precision: null,
  scale: null,
});

export default function TablesAdminPage() {
  const router = useRouter();
  const [tables, setTables] = useState<TableDefinition[]>([]);
  const [capabilities, setCapabilities] =
    useState<TableAdminCapabilities | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [kindFilter, setKindFilter] = useState<TableKind | "">("");
  const [statusFilter, setStatusFilter] =
    useState<TableVerificationStatus | "">("");
  const [showCreate, setShowCreate] = useState(false);
  const [name, setName] = useState("");
  const [kind, setKind] = useState<TableKind>("Source");
  const [columns, setColumns] = useState<ColumnDefinition[]>([emptyColumn()]);
  const [operatorId, setOperatorId] = useState("local-table-admin");
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);

  const dirty = showCreate &&
    (name !== "" || reason !== "" || columns.some((column) => column.name));

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
    void Promise.all([listAdminTables(), getTableAdminCapabilities()])
      .then(([loadedTables, permissions]) => {
        if (!active) return;
        setTables(loadedTables);
        setCapabilities(permissions);
      })
      .catch((caught) => {
        if (active) setError(message(caught));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  async function loadTables() {
    setLoading(true);
    setError(null);
    try {
      setTables(
        await listAdminTables({
          search: search.trim(),
          kind: kindFilter,
          status: statusFilter,
        }),
      );
    } catch (caught) {
      setError(message(caught));
    } finally {
      setLoading(false);
    }
  }

  function changeKind(next: TableKind) {
    setKind(next);
    if (next === "Source") {
      setColumns((values) =>
        values.map((column) => ({
          ...column,
          dataType: "Text",
          isRequired: false,
          precision: null,
          scale: null,
        })),
      );
    }
  }

  async function createTable(event: React.FormEvent) {
    event.preventDefault();
    const validation = validate(name, kind, columns, operatorId, reason);
    if (validation) {
      setError(validation);
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const created = await createAdminTable(
        { name: name.trim(), kind, columns },
        operatorId.trim(),
        reason.trim(),
      );
      setShowCreate(false);
      router.push(`/admin/tables/${created.id}?verify=1`);
    } catch (caught) {
      setError(message(caught));
    } finally {
      setSaving(false);
    }
  }

  function updateColumn(index: number, column: ColumnDefinition) {
    setColumns((values) =>
      values.map((value, position) => (position === index ? column : value)),
    );
  }

  function moveColumn(index: number, offset: number) {
    setColumns((values) => {
      const next = [...values];
      [next[index], next[index + offset]] = [
        next[index + offset],
        next[index],
      ];
      return next;
    });
  }

  return (
    <main className="mx-auto max-w-7xl space-y-6 px-6 py-8 lg:px-8">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-wide text-indigo-600">
            Control plane
          </p>
          <h1 className="text-3xl font-bold tracking-tight">Table Catalog</h1>
          <p className="mt-2 max-w-3xl text-slate-600">
            Create expand-only Source and Normalized tables, then verify their
            physical PostgreSQL schema against authoritative metadata.
          </p>
        </div>
        {capabilities?.canCreate ? (
          <button
            type="button"
            className="rounded-lg bg-indigo-600 px-4 py-2 font-semibold text-white hover:bg-indigo-700"
            onClick={() => setShowCreate((value) => !value)}
          >
            {showCreate ? "Close form" : "New table"}
          </button>
        ) : null}
      </header>

      {!capabilities?.mutationsEnabled && capabilities ? (
        <div role="status" className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-amber-900">
          Table mutations are paused. Read and verification remain available.
        </div>
      ) : null}
      {error ? (
        <div role="alert" className="rounded-xl border border-rose-300 bg-rose-50 p-4 text-rose-800">
          {error}
        </div>
      ) : null}

      {showCreate ? (
        <form
          aria-label="Create table"
          className="space-y-5 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
          onSubmit={createTable}
        >
          <div>
            <h2 className="text-xl font-semibold">Create table</h2>
            <p className="text-sm text-slate-500">
              Metadata and DDL commit together. Physical drift is never repaired automatically.
            </p>
          </div>
          <div className="grid gap-4 md:grid-cols-2">
            <label className="text-sm font-medium text-slate-700">
              Table name
              <input
                className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="src_orders"
              />
            </label>
            <label className="text-sm font-medium text-slate-700">
              Table kind
              <select
                className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2"
                value={kind}
                onChange={(event) => changeKind(event.target.value as TableKind)}
              >
                <option>Source</option>
                <option>Normalized</option>
              </select>
            </label>
          </div>
          <div className="space-y-3">
            {columns.map((column, index) => (
              <ColumnEditor
                key={index}
                column={column}
                index={index}
                kind={kind}
                canRemove={columns.length > 1}
                canMoveUp={index > 0}
                canMoveDown={index < columns.length - 1}
                onChange={(value) => updateColumn(index, value)}
                onRemove={() =>
                  setColumns((values) =>
                    values.filter((_, position) => position !== index),
                  )
                }
                onMoveUp={() => moveColumn(index, -1)}
                onMoveDown={() => moveColumn(index, 1)}
              />
            ))}
            <button
              type="button"
              className="rounded-lg border border-indigo-300 px-3 py-2 text-sm font-semibold text-indigo-700"
              onClick={() => setColumns((values) => [...values, emptyColumn()])}
            >
              Add another column
            </button>
          </div>
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
                placeholder="Approved table onboarding"
              />
            </label>
          </div>
          <button
            type="submit"
            disabled={saving}
            className="rounded-lg bg-indigo-600 px-4 py-2 font-semibold text-white disabled:opacity-50"
          >
            {saving ? "Creating…" : "Create and verify"}
          </button>
        </form>
      ) : null}

      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="grid gap-3 md:grid-cols-[1fr_12rem_14rem_auto]">
          <label className="text-sm font-medium text-slate-700">
            Search
            <input
              className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") void loadTables();
              }}
            />
          </label>
          <label className="text-sm font-medium text-slate-700">
            Kind
            <select
              className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2"
              value={kindFilter}
              onChange={(event) => setKindFilter(event.target.value as TableKind | "")}
            >
              <option value="">All kinds</option>
              <option>Source</option>
              <option>Normalized</option>
            </select>
          </label>
          <label className="text-sm font-medium text-slate-700">
            Verification status
            <select
              className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2"
              value={statusFilter}
              onChange={(event) =>
                setStatusFilter(event.target.value as TableVerificationStatus | "")
              }
            >
              {statuses.map((status) => (
                <option key={status} value={status}>
                  {status || "All statuses"}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className="self-end rounded-lg border border-slate-300 px-4 py-2 font-semibold"
            onClick={() => void loadTables()}
          >
            Apply filters
          </button>
        </div>

        {loading ? (
          <p role="status" className="mt-6 text-slate-500">Loading tables…</p>
        ) : tables.length === 0 ? (
          <div className="mt-6 rounded-xl border border-dashed border-slate-300 p-8 text-center text-slate-500">
            No managed tables match these filters.
          </div>
        ) : (
          <div className="mt-6 overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-200 text-slate-500">
                <tr>
                  <th className="px-3 py-2">Name</th>
                  <th className="px-3 py-2">Kind</th>
                  <th className="px-3 py-2">Columns</th>
                  <th className="px-3 py-2">Verification</th>
                  <th className="px-3 py-2">Checked</th>
                </tr>
              </thead>
              <tbody>
                {tables.map((table) => (
                  <tr key={table.id} className="border-b border-slate-100">
                    <td className="px-3 py-3 font-semibold">
                      <Link className="text-indigo-700 hover:underline" href={`/admin/tables/${table.id}`}>
                        {table.name}
                      </Link>
                    </td>
                    <td className="px-3 py-3">{table.kind}</td>
                    <td className="px-3 py-3">{table.columns.length}</td>
                    <td className="px-3 py-3">
                      <StatusBadge status={table.verificationStatus ?? "Unknown"} />
                    </td>
                    <td className="px-3 py-3 text-slate-500">
                      {formatDate(table.verificationCheckedAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}

export function StatusBadge({ status }: { status: TableVerificationStatus }) {
  const colors = status === "Healthy"
    ? "bg-emerald-100 text-emerald-800"
    : status === "Drifted" || status === "Missing"
      ? "bg-rose-100 text-rose-800"
      : status === "VerificationFailed"
        ? "bg-amber-100 text-amber-900"
        : "bg-slate-100 text-slate-700";
  return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${colors}`}>{status}</span>;
}

function validate(
  name: string,
  kind: TableKind,
  columns: ColumnDefinition[],
  operatorId: string,
  reason: string,
) {
  const identifier = /^[a-z][a-z0-9_]{0,62}$/;
  if (!identifier.test(name)) return "Table name must be a lowercase PostgreSQL-safe identifier.";
  if (!operatorId.trim() || !reason.trim()) return "Operator ID and reason are required.";
  if (columns.length === 0 || columns.some((column) => !identifier.test(column.name))) {
    return "Every column needs a unique lowercase PostgreSQL-safe name.";
  }
  if (new Set(columns.map((column) => column.name)).size !== columns.length) {
    return "Column names must be unique.";
  }
  if (kind === "Source" && columns.some((column) => column.dataType !== "Text" || column.isRequired)) {
    return "Source columns must be optional Text values.";
  }
  for (const column of columns) {
    if (column.dataType === "Text" && column.length !== null && column.length !== undefined && column.length < 1) {
      return "Text length must be greater than zero.";
    }
    if (column.dataType === "Decimal" && column.scale !== null && column.scale !== undefined &&
        (column.precision === null || column.precision === undefined || column.scale > column.precision)) {
      return "Decimal scale requires precision and cannot exceed it.";
    }
  }
  return null;
}

function message(error: unknown) {
  if (error instanceof ApiError) {
    const validation = Object.values(error.errors).flat().join(" ");
    return validation || error.detail || error.title;
  }
  return error instanceof Error ? error.message : "The request failed.";
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : "Not verified";
}
