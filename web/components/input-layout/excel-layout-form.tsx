"use client";

import type { ExcelInputLayout } from "@/lib/api/contracts";

interface Props {
  layout: ExcelInputLayout;
  onChange: (layout: ExcelInputLayout) => void;
  sourceColumns: string[];
}

export function ExcelLayoutForm({ layout, onChange, sourceColumns }: Props) {
  return (
    <div className="space-y-5">
      <div>
        <h3 className="font-semibold text-slate-900">Excel workbook layout</h3>
        <p className="mt-1 text-sm text-slate-500">
          The API reads the workbook. The browser only sends the selected file for preview.
        </p>
      </div>
      <div className="grid gap-4 md:grid-cols-4">
        <label className="text-sm font-medium text-slate-700">
          Workbook kind
          <select className="mt-1 w-full rounded-lg border border-slate-300 p-2" value={layout.workbookKind} onChange={(event) => onChange({ ...layout, workbookKind: event.target.value as ExcelInputLayout["workbookKind"] })}>
            <option value="AutoDetect">Auto detect</option><option value="Xlsx">.xlsx</option><option value="Xls">.xls</option>
          </select>
        </label>
        <label className="text-sm font-medium text-slate-700">Worksheet
          <input className="mt-1 w-full rounded-lg border border-slate-300 p-2" value={layout.sheet} onChange={(event) => onChange({ ...layout, sheet: event.target.value })} />
        </label>
        <label className="text-sm font-medium text-slate-700">Header row
          <input className="mt-1 w-full rounded-lg border border-slate-300 p-2" min={1} type="number" value={layout.headerRow} onChange={(event) => onChange({ ...layout, headerRow: Number(event.target.value) })} />
        </label>
        <label className="text-sm font-medium text-slate-700">Data starts at
          <input className="mt-1 w-full rounded-lg border border-slate-300 p-2" min={2} type="number" value={layout.dataStartRow} onChange={(event) => onChange({ ...layout, dataStartRow: Number(event.target.value) })} />
        </label>
      </div>
      <label className="block max-w-xs text-sm font-medium text-slate-700">Blank rows
        <select className="mt-1 w-full rounded-lg border border-slate-300 p-2" value={layout.blankRowPolicy} onChange={(event) => onChange({ ...layout, blankRowPolicy: event.target.value as ExcelInputLayout["blankRowPolicy"] })}>
          <option value="Skip">Skip</option><option value="Error">Reject file</option>
        </select>
      </label>
      <div className="space-y-2">
        {layout.fields.map((field, index) => (
          <div className="grid gap-3 md:grid-cols-2" key={field.sourceColumn}>
            <input aria-label={`Header for ${field.sourceColumn}`} className="rounded-lg border border-slate-300 p-2 text-sm" value={field.inputHeader} onChange={(event) => onChange({ ...layout, fields: layout.fields.map((item, itemIndex) => itemIndex === index ? { ...item, inputHeader: event.target.value } : item) })} />
            <input className="rounded-lg border border-slate-200 bg-slate-50 p-2 text-sm" disabled value={sourceColumns[index] ?? field.sourceColumn} />
          </div>
        ))}
      </div>
    </div>
  );
}
