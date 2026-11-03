"use client";

import { useState } from "react";
import type {
  ContractSchemaResolveResponse,
  FileDestinationProfileSummary,
  JsonValue,
  PreviewMappingConfigVersionResponse,
} from "@/lib/api/contracts";

type PreviewTab = "mapped" | "file" | "diagnostics";

interface Props {
  contract: ContractSchemaResolveResponse;
  preview: PreviewMappingConfigVersionResponse | null;
  profile: FileDestinationProfileSummary;
}

function parseJson(value: string | undefined): JsonValue | null {
  if (!value) return null;
  try {
    return JSON.parse(value) as JsonValue;
  } catch {
    return value;
  }
}

export function JsonFilePreview({ contract, preview, profile }: Props) {
  const [tab, setTab] = useState<PreviewTab>("file");
  const artifact = preview?.jsonFile;
  const mapped = (preview?.records ?? []).flatMap((record) =>
    (record.transformation?.outputs ?? []).map((output) => ({
      rowKey: record.rowKey,
      outputKey: output.outputKey,
      document: output.values,
    })),
  );
  const diagnostics = [
    ...(preview?.records ?? []).flatMap((record) =>
      (record.transformation?.errors ?? []).map((error) => ({
        stage: "transformation",
        rowKey: record.rowKey,
        path: error.expressionPath,
        message: error.reason,
      })),
    ),
    ...(artifact?.diagnostics ?? []).map((diagnostic) => ({
      stage: "json-file",
      ...diagnostic,
    })),
  ];

  return (
    <div className="rounded-xl border border-slate-200 p-4">
      <div className="grid gap-3 rounded-lg bg-slate-50 p-4 sm:grid-cols-2 lg:grid-cols-4">
        <Summary label="Profile" value={artifact?.profileId ?? profile.id} />
        <Summary label="Payload root" value="Array" />
        <Summary label="Encoding" value="UTF-8 · no BOM" />
        <Summary label="Failure scope" value="Whole file" />
        <Summary label="Visible path" value={artifact?.relativeDisplayPath ?? "Available after preview"} />
        <Summary label="Objects" value={String(artifact?.objectCount ?? 0)} />
        <Summary label="Estimated bytes" value={String(artifact?.estimatedUtf8Bytes ?? 0)} />
        <Summary label="Side effect" value="Preview only · no file written" />
      </div>

      <div className="mt-4 flex flex-wrap gap-2" role="tablist" aria-label="JSON file preview">
        {([
          ["mapped", "Mapped objects"],
          ["file", "Final JSON file"],
          ["diagnostics", "Diagnostics"],
        ] as const).map(([id, label]) => (
          <button
            aria-selected={tab === id}
            className={`rounded-lg px-3 py-2 text-sm font-semibold ${tab === id ? "bg-slate-900 text-white" : "bg-slate-100 text-slate-700"}`}
            key={id}
            onClick={() => setTab(id)}
            role="tab"
            type="button"
          >
            {label}
          </button>
        ))}
      </div>

      <div className="mt-3" role="tabpanel">
        {tab === "mapped" ? <JsonBlock ariaLabel="Mapped JSON objects preview" value={mapped} /> : null}
        {tab === "file" ? (
          artifact ? (
            <JsonBlock ariaLabel="Final JSON file preview" value={parseJson(artifact.finalJson)} />
          ) : (
            <p className="rounded-lg border border-dashed border-slate-300 bg-slate-50 px-4 py-8 text-center text-sm text-slate-600">
              Run Preview to assemble the read-only JSON array for {contract.contract.reference}.
            </p>
          )
        ) : null}
        {tab === "diagnostics" ? (
          diagnostics.length === 0 ? (
            <p className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-900">No diagnostics for this preview.</p>
          ) : <JsonBlock ariaLabel="JSON file preview diagnostics" value={diagnostics} />
        ) : null}
      </div>
    </div>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return <div><p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</p><p className="mt-1 break-all font-mono text-xs font-semibold text-slate-800">{value}</p></div>;
}

function JsonBlock({ ariaLabel, value }: { ariaLabel: string; value: unknown }) {
  return <pre aria-label={ariaLabel} className="max-h-96 overflow-auto rounded-lg bg-slate-950 p-4 font-mono text-xs text-slate-100">{JSON.stringify(value, null, 2)}</pre>;
}
