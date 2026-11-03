"use client";

import { useState } from "react";
import type {
  ApiDestinationProfile,
  ContractResolveResponse,
  JsonValue,
  PreviewMappingConfigVersionResponse,
} from "@/lib/api/contracts";

type PreviewTab = "input" | "source" | "mapped" | "delivery" | "diagnostics";

interface Props {
  contract: ContractResolveResponse;
  preview: PreviewMappingConfigVersionResponse | null;
  profile: ApiDestinationProfile;
  sample: Record<string, string>;
}

const tabs: Array<{ id: PreviewTab; label: string }> = [
  { id: "input", label: "Input" },
  { id: "source", label: "Source" },
  { id: "mapped", label: "Mapped" },
  { id: "delivery", label: "Delivery" },
  { id: "diagnostics", label: "Diagnostics" },
];

function parsePayload(payloadJson: string | null): JsonValue | null {
  if (payloadJson === null) return null;

  try {
    return JSON.parse(payloadJson) as JsonValue;
  } catch {
    return payloadJson;
  }
}

export function ApiContractPreview({ contract, preview, profile, sample }: Props) {
  const [tab, setTab] = useState<PreviewTab>("mapped");
  const delivery = preview?.contract;
  const records = preview?.records ?? [];
  const mappedDocuments = records.flatMap((record) =>
    (record.transformation?.outputs ?? []).map((output) => ({
      rowKey: record.rowKey,
      outputKey: output.outputKey,
      document: output.values,
    })),
  );
  const diagnostics = [
    ...records.flatMap((record) =>
      (record.transformation?.errors ?? []).map((error) => ({
        stage: "transformation",
        rowKey: record.rowKey,
        outputKey: error.outputKey,
        path: error.expressionPath,
        code: null,
        message: error.reason,
      })),
    ),
    ...(delivery?.outputs.flatMap((output) =>
      output.diagnostics.map((diagnostic) => ({
        stage: "delivery",
        rowKey: output.rowKey,
        outputKey: output.outputKey,
        path: diagnostic.path,
        code: diagnostic.code,
        message: diagnostic.message,
      })),
    ) ?? []),
  ];

  return (
    <div className="rounded-xl border border-slate-200 p-4">
      <h3 className="font-semibold text-slate-900">Preview sample</h3>
      <div className="mt-3 grid gap-2 sm:grid-cols-3">
        {Object.keys(sample).map((field) => (
          <div className="rounded-lg bg-slate-50 px-3 py-2" key={field}>
            <p className="font-mono text-xs font-semibold text-slate-500">{field}</p>
            <p className="mt-1 break-words text-sm text-slate-900">
              {sample[field] === "" ? "(blank)" : sample[field]}
            </p>
          </div>
        ))}
      </div>

      <div className="mt-4 flex flex-wrap gap-2" role="tablist">
        {tabs.map((item) => (
          <button
            aria-controls={`contract-preview-${item.id}`}
            aria-selected={tab === item.id}
            className={`rounded-lg px-3 py-2 text-sm font-semibold ${
              tab === item.id
                ? "bg-slate-900 text-white"
                : "bg-slate-100 text-slate-700"
            }`}
            id={`contract-preview-tab-${item.id}`}
            key={item.id}
            onClick={() => setTab(item.id)}
            role="tab"
            type="button"
          >
            {item.label}
          </button>
        ))}
      </div>

      <div
        aria-labelledby={`contract-preview-tab-${tab}`}
        className="mt-3"
        id={`contract-preview-${tab}`}
        role="tabpanel"
      >
        {tab === "input" ? (
          <JsonBlock
            ariaLabel="Input preview JSON"
            value={{
              kind: "sourceRows",
              rows: [{ rowKey: "preview-1", values: sample }],
            }}
          />
        ) : null}

        {tab === "source" ? (
          <JsonBlock
            ariaLabel="Source row preview JSON"
            value={records.map((record) => ({
              rowKey: record.rowKey,
              rowNumber: record.rowNumber,
              physicalLineNumber: record.physicalLineNumber,
              action: record.action,
              values: record.sourceValues,
            }))}
          />
        ) : null}

        {tab === "mapped" ? (
          <JsonBlock ariaLabel="Mapped document preview JSON" value={mappedDocuments} />
        ) : null}

        {tab === "delivery" ? (
          <DeliveryPreview
            contract={contract}
            delivery={delivery}
            profile={profile}
          />
        ) : null}

        {tab === "diagnostics" ? (
          diagnostics.length === 0 ? (
            <p className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-900">
              No diagnostics for this preview.
            </p>
          ) : (
            <JsonBlock ariaLabel="Preview diagnostics JSON" value={diagnostics} />
          )
        ) : null}
      </div>
    </div>
  );
}

interface DeliveryPreviewProps {
  contract: ContractResolveResponse;
  delivery: PreviewMappingConfigVersionResponse["contract"];
  profile: ApiDestinationProfile;
}

function DeliveryPreview({ contract, delivery, profile }: DeliveryPreviewProps) {
  const method = delivery?.method ?? contract.requestSummary.method;
  const path = delivery?.pathTemplate ?? contract.requestSummary.pathTemplate;
  const headerNames =
    delivery?.allowedHeaderNames ?? contract.requestSummary.allowedHeaderNames;

  return (
    <div className="space-y-4">
      <div className="grid gap-3 rounded-lg border border-slate-200 bg-slate-50 p-4 sm:grid-cols-2 lg:grid-cols-3">
        <Summary label="Endpoint profile" value={delivery?.profileId ?? profile.id} />
        <Summary label="Request" value={`${method} ${path}`} />
        <Summary
          label="Contract"
          value={delivery?.contractReference ?? contract.contract.reference}
        />
        <Summary label="Schema hash" value={delivery?.schemaHash ?? contract.schemaHash} />
        <Summary
          label="Idempotency example"
          value={delivery?.idempotencyKeyExample ?? "Available after preview"}
        />
        <Summary
          label="Network side effect"
          value={delivery?.sendsRequest ? "Request would be sent" : "Preview only — no request sent"}
        />
      </div>

      <div className="rounded-lg border border-slate-200 p-4">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
          Safe request headers
        </p>
        <div className="mt-2 flex flex-wrap gap-2">
          {headerNames.map((header) => (
            <span
              className="rounded bg-slate-100 px-2 py-1 font-mono text-xs text-slate-700"
              key={header}
            >
              {header}
            </span>
          ))}
        </div>
        <p className="mt-2 text-xs text-slate-500">
          Header names are server-owned metadata. Credentials and secret values are never shown.
        </p>
      </div>

      {delivery?.outputs.length ? (
        delivery.outputs.map((output) => (
          <article
            className="overflow-hidden rounded-lg border border-indigo-100"
            key={`${output.rowKey}:${output.outputKey}`}
          >
            <div className="flex flex-wrap justify-between gap-2 bg-indigo-50 px-4 py-3 text-xs text-indigo-900">
              <strong>
                {output.rowKey} · {output.outputKey}
              </strong>
              <span className="font-mono">{output.payloadHash ?? "No payload hash"}</span>
            </div>
            <JsonBlock
              ariaLabel={`Delivery payload ${output.rowKey} ${output.outputKey}`}
              value={parsePayload(output.payloadJson)}
            />
          </article>
        ))
      ) : (
        <p className="rounded-lg border border-dashed border-slate-300 bg-slate-50 px-4 py-8 text-center text-sm text-slate-600">
          Run Preview to assemble the read-only delivery payload.
        </p>
      )}
    </div>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
        {label}
      </p>
      <p className="mt-1 break-all font-mono text-xs font-semibold text-slate-800">
        {value}
      </p>
    </div>
  );
}

function JsonBlock({ ariaLabel, value }: { ariaLabel: string; value: unknown }) {
  return (
    <pre
      aria-label={ariaLabel}
      className="max-h-96 overflow-auto rounded-lg bg-slate-950 p-4 font-mono text-xs text-slate-100"
    >
      {JSON.stringify(value, null, 2)}
    </pre>
  );
}
