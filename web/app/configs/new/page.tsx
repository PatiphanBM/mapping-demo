"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { type FormEvent, useEffect, useMemo, useState } from "react";
import {
  ErrorPanel,
  FieldMessages,
  LoadingPanel,
} from "@/components/page-state";
import { ApiError } from "@/lib/api/client";
import type { TableDefinition } from "@/lib/api/contracts";
import {
  createMappingConfig,
  listTables,
} from "@/lib/api/mapping-configs";

type FormField = "name" | "inputFolder" | "sourceTableId" | "normalizedTableId";
type FormErrors = Partial<Record<FormField, string[]>>;

const fieldClassName =
  "mt-2 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm text-slate-950 shadow-sm outline-none transition focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100";

export default function NewConfigPage() {
  const router = useRouter();
  const [tables, setTables] = useState<TableDefinition[]>([]);
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

  useEffect(() => {
    let cancelled = false;

    listTables()
      .then((rows) => {
        if (!cancelled) {
          setTables(rows);
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
    if (normalizedTableId.length === 0) {
      clientErrors.normalizedTableId = ["Choose a Normalized table."];
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
      const config = await createMappingConfig({
        name: name.trim(),
        inputFolder: inputFolder.trim(),
        sourceTableId: Number(sourceTableId),
        normalizedTableId: Number(normalizedTableId),
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
          Connect an input folder to one Source table and one Normalized table.
          Versions are created separately after this identity is saved.
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
  };
  return fields[normalized] ?? null;
}
