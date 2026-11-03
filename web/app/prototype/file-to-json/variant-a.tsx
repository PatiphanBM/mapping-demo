"use client";

import { useState } from "react";
import { excelColumns, inputClassName, type VariantProps } from "./prototype-data";
import {
  ExtensionPicker,
  Field,
  JsonPreview,
  MappingTable,
  PreviewPanel,
  PrototypeNotice,
} from "./prototype-parts";

export function VariantA({ draft, setDraft }: VariantProps) {
  const steps = ["Setup", "Input File", "Mapping", "JSON Output", "Preview"];
  const [activeStep, setActiveStep] = useState(1);

  return (
    <>
      <PrototypeNotice variant="A · Guided wizard" />
      <main className="mx-auto w-full max-w-7xl px-6 py-8 lg:px-8">
        <div className="flex flex-wrap items-start justify-between gap-5">
          <div>
            <p className="text-sm font-semibold text-indigo-700">
              File → JSON / Draft configuration
            </p>
            <h1 className="mt-2 text-3xl font-semibold tracking-tight text-slate-950">
              {draft.configName}
            </h1>
            <p className="mt-2 text-sm text-slate-600">
              ตั้งค่าแบบทีละขั้น เหมาะกับผู้ใช้ที่สร้าง config ใหม่ไม่บ่อย
            </p>
          </div>
          <div className="flex items-center gap-2">
            <span className="rounded-full bg-amber-100 px-3 py-1.5 text-xs font-semibold text-amber-800">
              Draft · Not saved
            </span>
            <button
              className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700"
              type="button"
            >
              Validate
            </button>
          </div>
        </div>

        <div className="mt-8 grid grid-cols-5 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          {steps.map((step, index) => (
            <button
              className={`px-3 py-4 text-left transition ${
                activeStep === index
                  ? "bg-indigo-50 text-indigo-800"
                  : "text-slate-500 hover:bg-slate-50"
              } ${index > 0 ? "border-l border-slate-200" : ""}`}
              key={step}
              onClick={() => setActiveStep(index)}
              type="button"
            >
              <span
                className={`mr-2 inline-grid size-6 place-items-center rounded-full text-xs font-bold ${
                  activeStep === index
                    ? "bg-indigo-600 text-white"
                    : index < activeStep
                      ? "bg-emerald-100 text-emerald-700"
                      : "bg-slate-100 text-slate-500"
                }`}
              >
                {index < activeStep ? "✓" : index + 1}
              </span>
              <span className="hidden text-sm font-semibold sm:inline">
                {step}
              </span>
            </button>
          ))}
        </div>

        <section className="mt-5 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="border-b border-slate-200 px-6 py-5">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-indigo-600">
              Step {activeStep + 1} of {steps.length}
            </p>
            <h2 className="mt-1 text-xl font-semibold text-slate-950">
              {steps[activeStep]}
            </h2>
          </div>

          {activeStep === 0 ? (
            <div className="grid gap-5 p-6 md:grid-cols-2">
              <Field
                label="Config name"
                onChange={(configName) =>
                  setDraft((current) => ({ ...current, configName }))
                }
                value={draft.configName}
              />
              <Field
                label="Input folder"
                onChange={(inputFolder) =>
                  setDraft((current) => ({ ...current, inputFolder }))
                }
                value={draft.inputFolder}
              />
              <Field label="Source Table" value={draft.sourceTable} />
              <Field
                hint="กำหนดชื่อและชนิด property ของ JSON เท่านั้น ไม่มีการ insert ลงตารางนี้"
                label="Target Table Schema"
                value={draft.targetSchema}
              />
              <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 md:col-span-2">
                <p className="text-sm font-semibold text-emerald-900">
                  Destination · JSON File
                </p>
                <p className="mt-1 text-xs text-emerald-800">
                  Source rows ยังเก็บตาม flow เดิม แต่ mapped result จะไปเป็นไฟล์ JSON
                </p>
              </div>
            </div>
          ) : null}

          {activeStep === 1 ? (
            <div className="grid gap-6 p-6 lg:grid-cols-[0.9fr_1.6fr]">
              <div className="space-y-5">
                <ExtensionPicker draft={draft} setDraft={setDraft} />
                <label className="block text-sm font-semibold text-slate-700">
                  Layout type
                  <select className={inputClassName} defaultValue="excel">
                    <option value="excel">Excel workbook</option>
                    <option value="delimited">Delimited text</option>
                    <option value="fixed">Fixed-width text</option>
                  </select>
                </label>
                <div className="rounded-xl border-2 border-dashed border-indigo-200 bg-indigo-50/60 px-5 py-7 text-center">
                  <div className="mx-auto grid size-11 place-items-center rounded-xl bg-white text-xl shadow-sm">
                    ⇧
                  </div>
                  <p className="mt-3 text-sm font-semibold text-slate-900">
                    customer-sample.xlsx
                  </p>
                  <p className="mt-1 text-xs text-slate-500">
                    Sample only · not persisted
                  </p>
                </div>
                <div className="grid grid-cols-3 gap-3">
                  <Field
                    label="Worksheet"
                    onChange={(sheetName) =>
                      setDraft((current) => ({ ...current, sheetName }))
                    }
                    value={draft.sheetName}
                  />
                  <Field
                    label="Header row"
                    onChange={(value) =>
                      setDraft((current) => ({
                        ...current,
                        headerRow: Number(value),
                      }))
                    }
                    value={draft.headerRow}
                  />
                  <Field
                    label="Data starts"
                    onChange={(value) =>
                      setDraft((current) => ({
                        ...current,
                        dataStartRow: Number(value),
                      }))
                    }
                    value={draft.dataStartRow}
                  />
                </div>
              </div>

              <div>
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="font-semibold text-slate-950">
                      Column mapping preview
                    </h3>
                    <p className="mt-1 text-xs text-slate-500">
                      Header row 3 · first data row 4 · Sheet “Customer”
                    </p>
                  </div>
                  <span className="rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-semibold text-emerald-700">
                    4 of 4 mapped
                  </span>
                </div>
                <div className="mt-4 overflow-hidden rounded-xl border border-slate-200">
                  <table className="w-full text-left text-sm">
                    <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                      <tr>
                        <th className="px-4 py-3">Col</th>
                        <th className="px-4 py-3">Excel header</th>
                        <th className="px-4 py-3">Source column</th>
                        <th className="px-4 py-3">Sample</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {excelColumns.map(([column, header, source, sample]) => (
                        <tr key={column}>
                          <td className="px-4 py-3 font-mono text-xs text-slate-400">
                            {column}
                          </td>
                          <td className="px-4 py-3 font-medium text-slate-800">
                            {header}
                          </td>
                          <td className="px-4 py-3">
                            <select
                              className="w-full rounded-lg border border-slate-200 px-2 py-1.5 font-mono text-xs text-indigo-700"
                              defaultValue={source}
                            >
                              <option>{source}</option>
                            </select>
                          </td>
                          <td className="px-4 py-3 text-xs text-slate-500">
                            {sample}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="mt-4 rounded-xl bg-slate-950 p-4 text-xs leading-6 text-slate-300">
                  <span className="text-indigo-300">Workbook</span> → Sheet → Header row → Data rows → <span className="text-emerald-300">Source Table</span>
                </div>
              </div>
            </div>
          ) : null}

          {activeStep === 2 ? <MappingTable /> : null}

          {activeStep === 3 ? (
            <div className="grid gap-7 p-6 lg:grid-cols-2">
              <div className="space-y-5">
                <Field
                  label="Output folder"
                  onChange={(outputFolder) =>
                    setDraft((current) => ({ ...current, outputFolder }))
                  }
                  value={draft.outputFolder}
                />
                <Field
                  label="File name template"
                  onChange={(fileNameTemplate) =>
                    setDraft((current) => ({ ...current, fileNameTemplate }))
                  }
                  value={draft.fileNameTemplate}
                />
                <div className="grid grid-cols-2 gap-4">
                  <Field label="Document root" value="Array" />
                  <Field label="Encoding" value="UTF-8, no BOM" />
                </div>
                <label className="flex items-center gap-3 rounded-xl border border-slate-200 p-4 text-sm font-medium text-slate-700">
                  <input
                    checked={draft.indented}
                    onChange={(event) =>
                      setDraft((current) => ({
                        ...current,
                        indented: event.target.checked,
                      }))
                    }
                    type="checkbox"
                  />
                  Pretty-print JSON output
                </label>
              </div>
              <JsonPreview />
            </div>
          ) : null}

          {activeStep === 4 ? <PreviewPanel /> : null}

          <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50 px-6 py-4">
            <button
              className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 disabled:opacity-40"
              disabled={activeStep === 0}
              onClick={() => setActiveStep((current) => Math.max(0, current - 1))}
              type="button"
            >
              ← Previous
            </button>
            <button
              className="rounded-xl bg-indigo-600 px-4 py-2.5 text-sm font-semibold text-white disabled:bg-slate-300"
              disabled={activeStep === steps.length - 1}
              onClick={() =>
                setActiveStep((current) => Math.min(steps.length - 1, current + 1))
              }
              type="button"
            >
              Next step →
            </button>
          </div>
        </section>
      </main>
    </>
  );
}
