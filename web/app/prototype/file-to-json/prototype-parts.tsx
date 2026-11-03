"use client";

import type { Dispatch, SetStateAction } from "react";
import {
  type DraftState,
  inputClassName,
  mappingRows,
  previewJson,
} from "./prototype-data";

export function PrototypeNotice({ variant }: { variant: string }) {
  return (
    <div className="border-b border-amber-200 bg-amber-50 px-6 py-2 text-center text-xs font-medium text-amber-900">
      Prototype only · Variant {variant} · ข้อมูลจำลอง ไม่เรียก API และไม่บันทึก
    </div>
  );
}

export function Field({
  label,
  value,
  hint,
  onChange,
}: {
  label: string;
  value: string | number;
  hint?: string;
  onChange?: (value: string) => void;
}) {
  return (
    <label className="block text-sm font-semibold text-slate-700">
      {label}
      <input
        className={inputClassName}
        onChange={(event) => onChange?.(event.target.value)}
        readOnly={!onChange}
        value={value}
      />
      {hint ? (
        <span className="mt-1.5 block text-xs font-normal leading-5 text-slate-500">
          {hint}
        </span>
      ) : null}
    </label>
  );
}

export function ExtensionPicker({
  draft,
  setDraft,
}: {
  draft: DraftState;
  setDraft: Dispatch<SetStateAction<DraftState>>;
}) {
  return (
    <div>
      <p className="text-sm font-semibold text-slate-700">File extensions</p>
      <div className="mt-2 flex flex-wrap gap-2">
        {[".txt", ".csv", ".xlsx", ".xls"].map((extension) => {
          const active = draft.extensions.includes(extension);
          return (
            <button
              className={`rounded-full border px-3 py-1.5 text-sm font-semibold transition ${
                active
                  ? "border-indigo-600 bg-indigo-600 text-white"
                  : "border-slate-300 bg-white text-slate-600 hover:border-indigo-300"
              }`}
              key={extension}
              onClick={() =>
                setDraft((current) => ({
                  ...current,
                  extensions: active
                    ? current.extensions.filter((item) => item !== extension)
                    : [...current.extensions, extension],
                }))
              }
              type="button"
            >
              {active ? "✓ " : "+ "}
              {extension}
            </button>
          );
        })}
      </div>
    </div>
  );
}

export function MappingTable() {
  return (
    <div className="p-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold text-slate-950">Output branch · main</h3>
          <p className="mt-1 text-xs text-slate-500">
            ใช้ Transformation Program และ operator ชุดเดิม
          </p>
        </div>
        <button
          className="rounded-lg bg-indigo-600 px-3 py-2 text-xs font-semibold text-white"
          type="button"
        >
          + Add output
        </button>
      </div>
      <div className="mt-5 overflow-hidden rounded-xl border border-slate-200">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-3">JSON target field</th>
              <th className="px-4 py-3">Value / expression</th>
              <th className="px-4 py-3">Preview</th>
              <th className="px-4 py-3">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {mappingRows.map((row) => (
              <tr key={row.target}>
                <td className="px-4 py-4">
                  <p className="font-mono text-xs font-semibold text-slate-900">
                    {row.target}
                  </p>
                  <p className="mt-1 text-xs text-slate-400">{row.type}</p>
                </td>
                <td className="px-4 py-4">
                  <div className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 font-mono text-xs text-indigo-700">
                    {row.expression}
                  </div>
                </td>
                <td className="px-4 py-4 font-mono text-xs text-slate-600">
                  {row.sample}
                </td>
                <td className="px-4 py-4">
                  <span className="rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-700">
                    Valid
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export function PreviewPanel() {
  return (
    <div className="grid gap-6 p-6 lg:grid-cols-[0.8fr_1.2fr]">
      <div>
        <p className="text-sm font-semibold text-slate-900">Preview summary</p>
        <div className="mt-4 grid grid-cols-2 gap-3">
          {[
            ["Source rows", "10"],
            ["JSON objects", "9"],
            ["Filtered", "1"],
            ["Invalid", "0"],
          ].map(([label, value]) => (
            <div className="rounded-xl bg-slate-50 p-3" key={label}>
              <p className="text-xs text-slate-500">{label}</p>
              <p className="mt-1 text-2xl font-semibold text-slate-950">
                {value}
              </p>
            </div>
          ))}
        </div>
        <div className="mt-4 rounded-xl border border-emerald-200 bg-emerald-50 p-4">
          <p className="text-sm font-semibold text-emerald-900">
            ✓ Ready to create version
          </p>
        </div>
      </div>
      <JsonPreview />
    </div>
  );
}

export function JsonPreview() {
  return (
    <div className="rounded-2xl bg-slate-950 p-5 text-slate-200">
      <div className="flex items-center justify-between">
        <p className="text-sm font-semibold text-white">Final JSON preview</p>
        <span className="text-xs font-semibold text-emerald-300">9 objects</span>
      </div>
      <pre className="mt-4 overflow-x-auto text-xs leading-6 text-slate-300">
        {previewJson}
      </pre>
    </div>
  );
}

export function StateSnapshot({
  draft,
  variant,
}: {
  draft: DraftState;
  variant: string;
}) {
  const state = {
    prototypeVariant: variant,
    destination:
      draft.deliveryMode === "fileBatch" ? "jsonFile" : "apiContract",
    deliveryUnit:
      draft.deliveryMode === "fileBatch" ? "wholeFile" : "mappedOutput",
    draft,
    transformation: {
      sourceWhen: "notBlank(source.customer_no)",
      outputKey: "main",
      assignments: mappingRows.length,
    },
    preview: { sourceRows: 10, outputs: 9, filtered: 1, invalid: 0 },
  };

  return (
    <details className="mx-auto mb-32 mt-8 w-[calc(100%-3rem)] max-w-7xl rounded-2xl border border-dashed border-slate-300 bg-white px-5 py-4">
      <summary className="cursor-pointer text-sm font-semibold text-slate-700">
        Prototype state · เปิดดู state ที่หน้าจอกำลังแสดง
      </summary>
      <pre className="mt-4 overflow-x-auto rounded-xl bg-slate-950 p-4 text-xs leading-6 text-emerald-300">
        {JSON.stringify(state, null, 2)}
      </pre>
    </details>
  );
}
