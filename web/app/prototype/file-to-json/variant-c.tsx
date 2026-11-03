"use client";

import { useState } from "react";
import {
  excelColumns,
  mappingRows,
  previewJson,
  previewRecordJson,
  type VariantProps,
} from "./prototype-data";
import { PrototypeNotice } from "./prototype-parts";

export function VariantC({ draft, setDraft }: VariantProps) {
  const [editingTarget, setEditingTarget] = useState<string | null>(null);
  const editingRow = mappingRows.find((row) => row.target === editingTarget);
  const isFileBatch = draft.deliveryMode === "fileBatch";

  return (
    <>
      <PrototypeNotice variant="C · Mapping workbench" />
      <main className="min-h-[calc(100vh-104px)] bg-white">
        <div className="border-b border-slate-300 bg-slate-100 px-5 py-3">
          <div className="mx-auto flex max-w-[1600px] flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <span className="rounded bg-slate-900 px-2 py-1 font-mono text-xs font-bold text-white">
                FILE→JSON
              </span>
              <div>
                <h1 className="text-sm font-bold text-slate-950">
                  {draft.configName}
                </h1>
                <p className="text-[11px] text-slate-500">
                  Dense workbench · ดู input, mapping และ JSON พร้อมกัน
                </p>
              </div>
            </div>
            <div className="flex gap-1.5">
              {["Validate", "Preview", "Create version"].map((label, index) => (
                <button
                  className={`rounded px-3 py-1.5 text-xs font-semibold ${
                    index === 2
                      ? "bg-blue-700 text-white"
                      : "border border-slate-300 bg-white text-slate-700"
                  }`}
                  key={label}
                  type="button"
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="mx-auto grid max-w-[1600px] lg:grid-cols-[270px_minmax(650px,1fr)_390px]">
          <aside className="border-r border-slate-300 bg-slate-50 p-4">
            <p className="text-[10px] font-bold uppercase tracking-widest text-slate-400">
              Configuration
            </p>
            <div className="mt-3 space-y-3">
              <CompactField label="Input folder" value={draft.inputFolder} />
              <CompactField label="Source Table" value={draft.sourceTable} />
              <CompactField label="Target Schema" value={draft.targetSchema} />
            </div>

            <div className="mt-6 flex items-center justify-between border-b border-slate-300 pb-2">
              <p className="text-[10px] font-bold uppercase tracking-widest text-slate-400">
                Excel layout
              </p>
              <span className="text-[10px] font-semibold text-emerald-700">VALID</span>
            </div>
            <div className="mt-3 grid grid-cols-2 gap-2">
              <CompactField label="Sheet" value={draft.sheetName} />
              <CompactField label="Header row" value={String(draft.headerRow)} />
              <CompactField label="Data row" value={String(draft.dataStartRow)} />
              <CompactField label="Extensions" value="xlsx, xls" />
            </div>

            <div className="mt-6 border-b border-slate-300 pb-2">
              <p className="text-[10px] font-bold uppercase tracking-widest text-slate-400">
                Input → Source
              </p>
            </div>
            <div className="mt-2 divide-y divide-slate-200 border-y border-slate-200 bg-white">
              {excelColumns.map((column) => (
                <div className="px-2 py-2" key={column[0]}>
                  <div className="flex items-center gap-2 text-[11px]">
                    <span className="w-4 font-mono text-slate-400">{column[0]}</span>
                    <span className="min-w-0 flex-1 truncate text-slate-700">
                      {column[1]}
                    </span>
                  </div>
                  <p className="ml-6 mt-0.5 truncate font-mono text-[10px] text-blue-700">
                    → {column[2]}
                  </p>
                </div>
              ))}
            </div>
          </aside>

          <section className="min-w-0">
            <div className="flex items-center justify-between border-b border-slate-300 bg-white px-4 py-2.5">
              <div className="flex items-center gap-2">
                <button
                  className="border-b-2 border-blue-700 px-2 py-1 text-xs font-bold text-blue-800"
                  type="button"
                >
                  main
                </button>
                <button
                  className="px-2 py-1 text-xs font-semibold text-slate-500"
                  type="button"
                >
                  + branch
                </button>
              </div>
              <p className="font-mono text-[10px] text-slate-400">
                sourceWhen: notBlank(customer_no)
              </p>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full min-w-[820px] border-collapse text-left text-xs">
                <thead className="bg-slate-100 text-[10px] uppercase tracking-wide text-slate-500">
                  <tr>
                    <th className="w-10 border-b border-r border-slate-300 px-2 py-2 text-center">#</th>
                    <th className="border-b border-r border-slate-300 px-3 py-2">JSON target field</th>
                    <th className="border-b border-r border-slate-300 px-3 py-2">Type</th>
                    <th className="border-b border-r border-slate-300 px-3 py-2">Operator / expression</th>
                    <th className="border-b border-r border-slate-300 px-3 py-2">Sample output</th>
                    <th className="border-b border-slate-300 px-3 py-2">Check</th>
                  </tr>
                </thead>
                <tbody>
                  {mappingRows.map((row, index) => (
                    <tr className="hover:bg-blue-50/60" key={row.target}>
                      <td className="border-b border-r border-slate-200 bg-slate-50 px-2 py-3 text-center font-mono text-slate-400">
                        {index + 1}
                      </td>
                      <td className="border-b border-r border-slate-200 px-3 py-3 font-mono font-semibold text-slate-900">
                        {row.target}
                      </td>
                      <td className="border-b border-r border-slate-200 px-3 py-3 text-slate-500">
                        {row.type}
                      </td>
                      <td className="border-b border-r border-slate-200 p-1.5">
                        <button
                          aria-label={`Edit expression for ${row.target}`}
                          className="flex w-full items-center justify-between gap-3 rounded border border-transparent bg-transparent px-2 py-1.5 text-left font-mono text-[11px] text-blue-800 outline-none hover:border-blue-300 hover:bg-blue-50 focus:border-blue-500 focus:bg-white"
                          onClick={() => setEditingTarget(row.target)}
                          type="button"
                        >
                          <span className="truncate">
                            {draft.mappingExpressions[row.target] ?? row.expression}
                          </span>
                          <span className="shrink-0 font-sans text-[10px] font-semibold text-blue-600">
                            Edit
                          </span>
                        </button>
                      </td>
                      <td className="border-b border-r border-slate-200 px-3 py-3 font-mono text-[11px] text-slate-600">
                        {row.sample}
                      </td>
                      <td className="border-b border-slate-200 px-3 py-3 text-emerald-700">
                        {isProposedExpression(
                          draft.mappingExpressions[row.target] ?? row.expression,
                        )
                          ? "◌ Proposed UI"
                          : draft.mappingExpressions[row.target] !== row.expression
                            ? "✓ Edited · Valid"
                            : "✓ Valid"}
                      </td>
                    </tr>
                  ))}
                  {Array.from({ length: 7 }, (_, index) => (
                    <tr key={`empty:${index}`}>
                      <td className="border-b border-r border-slate-200 bg-slate-50 px-2 py-4 text-center font-mono text-slate-300">
                        {index + 5}
                      </td>
                      <td className="border-b border-r border-slate-200" />
                      <td className="border-b border-r border-slate-200" />
                      <td className="border-b border-r border-slate-200" />
                      <td className="border-b border-r border-slate-200" />
                      <td className="border-b border-slate-200" />
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <aside className="border-l border-slate-300 bg-[#0d1424] text-slate-300">
            <div className="flex items-center justify-between border-b border-slate-700 px-4 py-3">
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-slate-500">
                  Live result
                </p>
                <p className="mt-1 text-xs font-semibold text-white">
                  {isFileBatch ? "Final JSON · whole file" : "Request JSON · record 1"}
                </p>
              </div>
              <span className="rounded bg-emerald-400/10 px-2 py-1 text-[10px] font-bold text-emerald-300">
                {isFileBatch ? "1 FILE · 9 OBJECTS" : "9 REQUESTS"}
              </span>
            </div>
            <pre className="max-h-[520px] overflow-auto p-4 text-[11px] leading-5 text-sky-200">
              {isFileBatch ? previewJson : previewRecordJson}
            </pre>
            <div className="border-t border-slate-700 p-4">
              <p className="text-[10px] font-bold uppercase tracking-widest text-slate-500">
                Delivery mode
              </p>
              <p className="mt-1 text-[10px] leading-4 text-slate-500">
                หนึ่ง Config Version เลือกหนึ่งแบบ
              </p>
              <div className="mt-3 grid gap-2">
                <button
                  aria-pressed={isFileBatch}
                  className={`rounded border p-3 text-left transition ${
                    isFileBatch
                      ? "border-blue-400 bg-blue-400/10 text-white"
                      : "border-slate-700 bg-slate-900/40 text-slate-400 hover:border-slate-500"
                  }`}
                  onClick={() =>
                    setDraft((current) => ({
                      ...current,
                      deliveryMode: "fileBatch",
                    }))
                  }
                  type="button"
                >
                  <span className="flex items-center gap-2 text-xs font-semibold">
                    <span aria-hidden="true">{isFileBatch ? "●" : "○"}</span>
                    Whole file · Batch
                  </span>
                  <span className="mt-1 block pl-5 text-[10px] leading-4 text-slate-400">
                    รวมทุกรายการเป็น JSON array หนึ่งไฟล์
                  </span>
                </button>
                <button
                  aria-pressed={!isFileBatch}
                  className={`rounded border p-3 text-left transition ${
                    !isFileBatch
                      ? "border-blue-400 bg-blue-400/10 text-white"
                      : "border-slate-700 bg-slate-900/40 text-slate-400 hover:border-slate-500"
                  }`}
                  onClick={() =>
                    setDraft((current) => ({
                      ...current,
                      deliveryMode: "perRecord",
                    }))
                  }
                  type="button"
                >
                  <span className="flex items-center gap-2 text-xs font-semibold">
                    <span aria-hidden="true">{!isFileBatch ? "●" : "○"}</span>
                    One record at a time
                  </span>
                  <span className="mt-1 block pl-5 text-[10px] leading-4 text-slate-400">
                    ส่ง JSON object แยกหนึ่ง request ต่อหนึ่งราย
                  </span>
                </button>
              </div>
              <dl className="mt-3 space-y-2 text-[11px]">
                {(isFileBatch
                  ? [
                      ["Target", draft.outputFolder],
                      ["Payload root", "array"],
                      ["Delivery count", "1 file"],
                      ["Failure scope", "whole file"],
                    ]
                  : [
                      ["Target", "customer-api/v1"],
                      ["Payload root", "object"],
                      ["Delivery count", "9 requests"],
                      ["Retry scope", "per record"],
                    ]
                ).map(([label, value]) => (
                  <div className="flex justify-between gap-3" key={label}>
                    <dt className="text-slate-500">{label}</dt>
                    <dd className="font-mono text-white">{value}</dd>
                  </div>
                ))}
              </dl>
            </div>
          </aside>
        </div>
      </main>

      {editingRow ? (
        <ExpressionDialog
          expression={
            draft.mappingExpressions[editingRow.target] ?? editingRow.expression
          }
          key={editingRow.target}
          onApply={(expression) => {
            setDraft((current) => ({
              ...current,
              mappingExpressions: {
                ...current.mappingExpressions,
                [editingRow.target]: expression,
              },
            }));
            setEditingTarget(null);
          }}
          onClose={() => setEditingTarget(null)}
          row={editingRow}
        />
      ) : null}
    </>
  );
}

type MappingRow = (typeof mappingRows)[number];
type ExpressionKind =
  | "source"
  | "trim"
  | "convert"
  | "case"
  | "constant"
  | "add"
  | "concat";

function ExpressionDialog({
  row,
  expression,
  onApply,
  onClose,
}: {
  row: MappingRow;
  expression: string;
  onApply: (expression: string) => void;
  onClose: () => void;
}) {
  const fields = excelColumns.map((column) => column[2]);
  const initialField = fields.find((field) => expression.includes(field)) ?? fields[0];
  const [kind, setKind] = useState<ExpressionKind>(() => expressionKind(expression));
  const [sourceField, setSourceField] = useState(initialField);
  const [conversionType, setConversionType] = useState(
    expression.match(/,\s*(\w+)\)/)?.[1] ?? "date",
  );
  const [compareValue, setCompareValue] = useState("ACTIVE");
  const [thenValue, setThenValue] = useState("true");
  const [elseValue, setElseValue] = useState("false");
  const [constantValue, setConstantValue] = useState("");
  const [operands, setOperands] = useState<string[]>(() =>
    expression.startsWith("add(")
      ? ["net_amount", "tax_amount"]
      : ["customer_no", "full_name"],
  );
  const [separator, setSeparator] = useState(" ");

  const nextExpression = buildExpression({
    kind,
    sourceField,
    conversionType,
    compareValue,
    thenValue,
    elseValue,
    constantValue,
    operands,
    separator,
  });

  function selectKind(nextKind: ExpressionKind) {
    setKind(nextKind);
    if (nextKind === "add") setOperands(["net_amount", "tax_amount"]);
    if (nextKind === "concat") setOperands(["customer_no", "full_name"]);
  }

  return (
    <div
      aria-labelledby="expression-dialog-title"
      aria-modal="true"
      className="fixed inset-0 z-[60] grid place-items-center bg-slate-950/55 p-4"
      role="dialog"
    >
      <div className="max-h-[calc(100vh-2rem)] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white shadow-2xl">
        <div className="flex items-start justify-between gap-4 border-b border-slate-200 px-6 py-5">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-blue-700">
              Expression builder
            </p>
            <h2
              className="mt-1 text-xl font-semibold text-slate-950"
              id="expression-dialog-title"
            >
              Map {row.target}
            </h2>
            <p className="mt-1 text-sm text-slate-500">
              {row.type} · เลือกวิธีสร้างค่าโดยไม่ต้องพิมพ์ expression เอง
            </p>
          </div>
          <button
            aria-label="Close expression builder"
            className="rounded-lg px-3 py-1.5 text-xl text-slate-400 hover:bg-slate-100 hover:text-slate-700"
            onClick={onClose}
            type="button"
          >
            ×
          </button>
        </div>

        <div className="space-y-5 px-6 py-5">
          <label className="block text-sm font-semibold text-slate-700">
            Value type
            <select
              className={dialogInputClassName}
              onChange={(event) =>
                selectKind(event.target.value as ExpressionKind)
              }
              value={kind}
            >
              <option value="source">Direct source field</option>
              <option value="trim">Trim whitespace</option>
              <option value="convert">Convert type</option>
              <option value="add">Add numeric values</option>
              <option value="concat">Concatenate text</option>
              <option value="case">CASE condition</option>
              <option value="constant">Constant value</option>
            </select>
          </label>

          {!(["constant", "add", "concat"] as ExpressionKind[]).includes(
            kind,
          ) ? (
            <label className="block text-sm font-semibold text-slate-700">
              Source field
              <select
                className={dialogInputClassName}
                onChange={(event) => setSourceField(event.target.value)}
                value={sourceField}
              >
                {fields.map((field) => (
                  <option key={field} value={field}>
                    {field}
                  </option>
                ))}
              </select>
            </label>
          ) : null}

          {kind === "add" || kind === "concat" ? (
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="text-sm font-semibold text-slate-800">
                    {kind === "add" ? "Values to add" : "Text parts"}
                  </p>
                  <p className="mt-1 text-xs text-slate-500">
                    ระบบคำนวณจากบนลงล่าง และต้องมีอย่างน้อย 2 operands
                  </p>
                </div>
                <span className="rounded-full bg-amber-100 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-amber-800">
                  Proposed
                </span>
              </div>

              <div className="mt-4 space-y-2">
                {operands.map((operand, index) => (
                  <div
                    className="grid grid-cols-[28px_1fr_auto] items-center gap-2"
                    key={`${index}:${operand}`}
                  >
                    <span className="text-center font-mono text-xs text-slate-400">
                      {index + 1}
                    </span>
                    <select
                      aria-label={`Operand ${index + 1}`}
                      className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-950"
                      onChange={(event) =>
                        setOperands((current) =>
                          current.map((item, itemIndex) =>
                            itemIndex === index ? event.target.value : item,
                          ),
                        )
                      }
                      value={operand}
                    >
                      {fields.map((field) => (
                        <option key={field} value={field}>
                          source.{field}
                        </option>
                      ))}
                    </select>
                    <button
                      className="rounded-lg px-2 py-2 text-xs font-semibold text-rose-700 disabled:text-slate-300"
                      disabled={operands.length <= 2}
                      onClick={() =>
                        setOperands((current) =>
                          current.filter((_, itemIndex) => itemIndex !== index),
                        )
                      }
                      type="button"
                    >
                      Remove
                    </button>
                  </div>
                ))}
              </div>

              <button
                className="mt-3 text-xs font-semibold text-blue-700"
                onClick={() =>
                  setOperands((current) => [
                    ...current,
                    fields.find((field) => !current.includes(field)) ?? fields[0],
                  ])
                }
                type="button"
              >
                + Add operand
              </button>

              {kind === "concat" ? (
                <div className="mt-4">
                  <DialogField
                    label="Separator"
                    onChange={setSeparator}
                    value={separator}
                  />
                  <p className="mt-1 text-xs text-slate-500">
                    เว้นว่างเมื่อต้องการต่อข้อความโดยไม่มีตัวคั่น
                  </p>
                </div>
              ) : null}
            </div>
          ) : null}

          {kind === "convert" ? (
            <label className="block text-sm font-semibold text-slate-700">
              Convert to
              <select
                className={dialogInputClassName}
                onChange={(event) => setConversionType(event.target.value)}
                value={conversionType}
              >
                {['date', 'decimal', 'integer', 'boolean', 'text'].map((type) => (
                  <option key={type} value={type}>
                    {type}
                  </option>
                ))}
              </select>
            </label>
          ) : null}

          {kind === "case" ? (
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-sm font-semibold text-slate-800">Condition</p>
              <div className="mt-3 grid gap-3 sm:grid-cols-3">
                <ReadOnlyField label="Field" value={sourceField} />
                <ReadOnlyField label="Operator" value="Equals" />
                <DialogField
                  label="Value"
                  onChange={setCompareValue}
                  value={compareValue}
                />
              </div>
              <div className="mt-3 grid gap-3 sm:grid-cols-2">
                <DialogField
                  label="Then value"
                  onChange={setThenValue}
                  value={thenValue}
                />
                <DialogField
                  label="Else value"
                  onChange={setElseValue}
                  value={elseValue}
                />
              </div>
            </div>
          ) : null}

          {kind === "constant" ? (
            <DialogField
              label="Constant value"
              onChange={setConstantValue}
              value={constantValue}
            />
          ) : null}

          <div className="rounded-xl bg-slate-950 p-4">
            <div className="flex items-center justify-between gap-3">
              <p className="text-xs font-semibold uppercase tracking-wider text-slate-400">
                Expression preview
              </p>
              <span
                className={`text-xs font-semibold ${
                  kind === "add" || kind === "concat"
                    ? "text-amber-300"
                    : "text-emerald-300"
                }`}
              >
                {kind === "add" || kind === "concat"
                  ? "Proposed · backend pending"
                  : "Valid"}
              </span>
            </div>
            <code className="mt-3 block break-all text-sm text-sky-300">
              {nextExpression}
            </code>
          </div>
        </div>

        <div className="flex justify-end gap-2 border-t border-slate-200 bg-slate-50 px-6 py-4">
          <button
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700"
            onClick={onClose}
            type="button"
          >
            Cancel
          </button>
          <button
            className="rounded-lg bg-blue-700 px-4 py-2 text-sm font-semibold text-white"
            onClick={() => onApply(nextExpression)}
            type="button"
          >
            Apply expression
          </button>
        </div>
      </div>
    </div>
  );
}

const dialogInputClassName =
  "mt-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm text-slate-950 outline-none focus:border-blue-500 focus:ring-4 focus:ring-blue-100";

function DialogField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="block text-sm font-semibold text-slate-700">
      {label}
      <input
        className={dialogInputClassName}
        onChange={(event) => onChange(event.target.value)}
        value={value}
      />
    </label>
  );
}

function ReadOnlyField({ label, value }: { label: string; value: string }) {
  return (
    <label className="block text-sm font-semibold text-slate-700">
      {label}
      <input className={dialogInputClassName} readOnly value={value} />
    </label>
  );
}

function expressionKind(expression: string): ExpressionKind {
  if (expression.startsWith("trim(")) return "trim";
  if (expression.startsWith("convert(")) return "convert";
  if (expression.startsWith("add(")) return "add";
  if (expression.startsWith("concat(")) return "concat";
  if (expression.startsWith("case ")) return "case";
  if (expression.startsWith("source.")) return "source";
  return "constant";
}

function buildExpression({
  kind,
  sourceField,
  conversionType,
  compareValue,
  thenValue,
  elseValue,
  constantValue,
  operands,
  separator,
}: {
  kind: ExpressionKind;
  sourceField: string;
  conversionType: string;
  compareValue: string;
  thenValue: string;
  elseValue: string;
  constantValue: string;
  operands: string[];
  separator: string;
}) {
  switch (kind) {
    case "source":
      return `source.${sourceField}`;
    case "trim":
      return `trim(source.${sourceField})`;
    case "convert":
      return `convert(source.${sourceField}, ${conversionType})`;
    case "add":
      return `add(${operands.map((field) => `source.${field}`).join(", ")})`;
    case "concat":
      return `concat(${operands
        .map((field) => `source.${field}`)
        .join(`, ${formatLiteral(separator)}, `)})`;
    case "case":
      return `case source.${sourceField} = ${formatLiteral(compareValue)} then ${formatLiteral(thenValue)} else ${formatLiteral(elseValue)}`;
    case "constant":
      return `constant(${formatLiteral(constantValue)})`;
  }
}

function formatLiteral(value: string) {
  const trimmed = value.trim();
  if (trimmed === "true" || trimmed === "false" || trimmed === "null") {
    return trimmed;
  }
  if (trimmed !== "" && Number.isFinite(Number(trimmed))) return trimmed;
  return JSON.stringify(value);
}

function isProposedExpression(expression: string) {
  return expression.startsWith("add(") || expression.startsWith("concat(");
}

function CompactField({ label, value }: { label: string; value: string }) {
  return (
    <label className="block text-[10px] font-semibold uppercase tracking-wide text-slate-500">
      {label}
      <input
        className="mt-1 w-full rounded border border-slate-300 bg-white px-2 py-1.5 font-mono text-[11px] normal-case tracking-normal text-slate-800"
        readOnly
        value={value}
      />
    </label>
  );
}
