"use client";

import { useState } from "react";
import { excelColumns, mappingRows, type VariantProps } from "./prototype-data";
import { PrototypeNotice } from "./prototype-parts";

export function VariantB({ draft }: VariantProps) {
  const stages = [
    { key: "input", title: "Input file", value: ".xlsx / .xls", meta: "Workbook" },
    { key: "source", title: "Source", value: draft.sourceTable, meta: "4 fields" },
    { key: "mapping", title: "Transform", value: "main branch", meta: "4 assignments" },
    { key: "json", title: "JSON file", value: draft.targetSchema, meta: "Array" },
  ];
  const [selectedStage, setSelectedStage] = useState("mapping");

  return (
    <>
      <PrototypeNotice variant="B · Pipeline canvas" />
      <main className="min-h-[calc(100vh-104px)] bg-[#eef2f6] px-6 py-7 lg:px-8">
        <div className="mx-auto max-w-[1500px]">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.18em] text-teal-700">
                File → JSON pipeline
              </p>
              <h1 className="mt-1 text-3xl font-semibold tracking-tight text-slate-950">
                {draft.configName}
              </h1>
            </div>
            <div className="flex gap-2">
              <button
                className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700"
                type="button"
              >
                Preview file
              </button>
              <button
                className="rounded-lg bg-teal-700 px-4 py-2 text-sm font-semibold text-white"
                type="button"
              >
                Create version
              </button>
            </div>
          </div>

          <div className="mt-6 grid min-h-[680px] overflow-hidden rounded-2xl border border-slate-300 bg-white shadow-sm lg:grid-cols-[220px_1fr_330px]">
            <aside className="border-r border-slate-200 bg-slate-950 p-4 text-slate-300">
              <p className="px-2 text-[10px] font-bold uppercase tracking-[0.2em] text-slate-500">
                Config navigator
              </p>
              <nav className="mt-4 space-y-1">
                {[
                  "Overview",
                  "Input layouts",
                  "Schemas",
                  "Conditions",
                  "JSON delivery",
                  "Version history",
                ].map((label, index) => (
                  <button
                    className={`w-full rounded-lg px-3 py-2.5 text-left text-sm font-medium transition ${
                      index === 0
                        ? "bg-teal-500/15 text-teal-300"
                        : "hover:bg-white/5 hover:text-white"
                    }`}
                    key={label}
                    type="button"
                  >
                    {label}
                  </button>
                ))}
              </nav>
              <div className="mt-8 border-t border-slate-800 pt-5">
                <p className="px-2 text-[10px] font-bold uppercase tracking-[0.2em] text-slate-500">
                  Health
                </p>
                {[
                  ["Input layout", "Ready"],
                  ["Mapping", "Valid"],
                  ["Destination", "Ready"],
                ].map(([label, value]) => (
                  <div
                    className="mt-3 flex items-center justify-between px-2 text-xs"
                    key={label}
                  >
                    <span>{label}</span>
                    <span className="text-emerald-400">● {value}</span>
                  </div>
                ))}
              </div>
            </aside>

            <section className="min-w-0 bg-[radial-gradient(circle_at_1px_1px,#d7dee8_1px,transparent_0)] bg-[size:22px_22px] p-7">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs font-semibold text-slate-500">Pipeline canvas</p>
                  <h2 className="mt-1 text-xl font-semibold text-slate-950">
                    คลิกแต่ละขั้นเพื่อดู config
                  </h2>
                </div>
                <span className="rounded-full border border-amber-300 bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-800">
                  Draft
                </span>
              </div>

              <div className="mt-16 flex items-stretch justify-center gap-2">
                {stages.map((stage, index) => (
                  <div className="contents" key={stage.key}>
                    <button
                      className={`w-36 rounded-2xl border-2 bg-white p-4 text-left shadow-sm transition hover:-translate-y-1 ${
                        selectedStage === stage.key
                          ? "border-teal-600 ring-4 ring-teal-100"
                          : "border-slate-200"
                      }`}
                      onClick={() => setSelectedStage(stage.key)}
                      type="button"
                    >
                      <span className="grid size-9 place-items-center rounded-xl bg-teal-50 text-sm font-bold text-teal-700">
                        {index + 1}
                      </span>
                      <p className="mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400">
                        {stage.title}
                      </p>
                      <p className="mt-1 truncate font-mono text-xs font-semibold text-slate-900">
                        {stage.value}
                      </p>
                      <p className="mt-3 text-xs text-slate-500">{stage.meta}</p>
                    </button>
                    {index < stages.length - 1 ? (
                      <div className="flex items-center text-2xl text-teal-500">→</div>
                    ) : null}
                  </div>
                ))}
              </div>

              <div className="mx-auto mt-14 max-w-3xl rounded-2xl border border-slate-200 bg-white/95 p-5 shadow-sm">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-[0.16em] text-teal-700">
                      Data run simulation
                    </p>
                    <h3 className="mt-1 font-semibold text-slate-950">
                      customer-sample.xlsx
                    </h3>
                  </div>
                  <span className="text-xs font-semibold text-emerald-700">
                    ✓ Preview complete
                  </span>
                </div>
                <div className="mt-5 grid grid-cols-4 gap-3">
                  {[
                    ["Imported", "10"],
                    ["Outputs", "9"],
                    ["Filtered", "1"],
                    ["Invalid", "0"],
                  ].map(([label, value]) => (
                    <div className="rounded-xl bg-slate-50 p-3 text-center" key={label}>
                      <p className="text-xl font-semibold text-slate-950">{value}</p>
                      <p className="mt-1 text-[11px] text-slate-500">{label}</p>
                    </div>
                  ))}
                </div>
              </div>
            </section>

            <aside className="border-l border-slate-200 bg-white p-5">
              <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-slate-400">
                Inspector
              </p>
              <h2 className="mt-2 text-lg font-semibold capitalize text-slate-950">
                {selectedStage} settings
              </h2>
              <p className="mt-1 text-xs leading-5 text-slate-500">
                แก้ค่าของขั้นที่เลือกโดยไม่ออกจากภาพรวม pipeline
              </p>

              {selectedStage === "input" ? (
                <InspectorGroup
                  rows={[
                    ["Extensions", ".xlsx, .xls"],
                    ["Worksheet", draft.sheetName],
                    ["Header row", String(draft.headerRow)],
                    ["Data starts", String(draft.dataStartRow)],
                  ]}
                />
              ) : null}
              {selectedStage === "source" ? (
                <InspectorGroup rows={excelColumns.map((row) => [row[2], "Text"])} />
              ) : null}
              {selectedStage === "mapping" ? (
                <div className="mt-6 space-y-3">
                  <div className="rounded-xl border border-teal-200 bg-teal-50 p-4">
                    <p className="text-xs font-semibold text-teal-900">sourceWhen</p>
                    <p className="mt-2 font-mono text-xs text-teal-800">
                      notBlank(customer_no)
                    </p>
                  </div>
                  {mappingRows.map((row) => (
                    <button
                      className="w-full rounded-xl border border-slate-200 p-3 text-left hover:border-teal-300"
                      key={row.target}
                      type="button"
                    >
                      <span className="font-mono text-xs font-semibold text-slate-900">
                        {row.target}
                      </span>
                      <p className="mt-2 truncate font-mono text-[11px] text-slate-500">
                        {row.expression}
                      </p>
                    </button>
                  ))}
                </div>
              ) : null}
              {selectedStage === "json" ? (
                <InspectorGroup
                  rows={[
                    ["Target", draft.targetSchema],
                    ["Folder", draft.outputFolder],
                    ["Root", "array"],
                    ["Row error", "fail whole file"],
                  ]}
                />
              ) : null}
            </aside>
          </div>
        </div>
      </main>
    </>
  );
}

function InspectorGroup({ rows }: { rows: string[][] }) {
  return (
    <div className="mt-6 space-y-2">
      {rows.map(([label, value]) => (
        <div
          className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 px-3 py-2.5 text-xs"
          key={label}
        >
          <span className="text-slate-500">{label}</span>
          <span className="truncate font-mono font-semibold text-slate-900">
            {value}
          </span>
        </div>
      ))}
    </div>
  );
}
