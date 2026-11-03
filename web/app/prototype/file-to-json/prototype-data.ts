import type { Dispatch, SetStateAction } from "react";

export interface DraftState {
  configName: string;
  inputFolder: string;
  sourceTable: string;
  targetSchema: string;
  extensions: string[];
  sheetName: string;
  headerRow: number;
  dataStartRow: number;
  outputFolder: string;
  fileNameTemplate: string;
  indented: boolean;
  deliveryMode: "fileBatch" | "perRecord";
  mappingExpressions: Record<string, string>;
}

export interface VariantProps {
  draft: DraftState;
  setDraft: Dispatch<SetStateAction<DraftState>>;
}

export const initialDraft: DraftState = {
  configName: "Customer Excel Export",
  inputFolder: "customer-in",
  sourceTable: "customer_source",
  targetSchema: "customer_json_schema",
  extensions: [".xlsx", ".xls"],
  sheetName: "Customer",
  headerRow: 3,
  dataStartRow: 4,
  outputFolder: "customers",
  fileNameTemplate: "{sourceBaseName}.{fileJobId}.json",
  indented: false,
  deliveryMode: "fileBatch",
  mappingExpressions: {
    customerId: "trim(source.customer_no)",
    name: "trim(source.full_name)",
    registeredDate: "convert(source.register_date_raw, date)",
    active: 'case source.status = "ACTIVE" then true else false',
  },
};

export const mappingRows = [
  {
    target: "customerId",
    type: "Text · Required",
    expression: "trim(source.customer_no)",
    sample: '"C0001"',
  },
  {
    target: "name",
    type: "Text · Required",
    expression: "trim(source.full_name)",
    sample: '"Somchai Jaidee"',
  },
  {
    target: "registeredDate",
    type: "Date · Optional",
    expression: "convert(source.register_date_raw, date)",
    sample: '"2026-10-19"',
  },
  {
    target: "active",
    type: "Boolean · Required",
    expression: 'case status = "ACTIVE" then true else false',
    sample: "true",
  },
];

export const excelColumns = [
  ["A", "Customer No", "customer_no", "C0001"],
  ["B", "Full Name", "full_name", "Somchai Jaidee"],
  ["C", "Register Date", "register_date_raw", "2026-10-19"],
  ["D", "Status", "status", "ACTIVE"],
  ["E", "Net Amount", "net_amount", "1000.00"],
  ["F", "Tax Amount", "tax_amount", "70.00"],
];

export const previewJson = `[
  {
    "customerId": "C0001",
    "name": "Somchai Jaidee",
    "registeredDate": "2026-10-19",
    "active": true
  },
  {
    "customerId": "C0002",
    "name": "Suda Rakthai",
    "registeredDate": null,
    "active": false
  }
]`;

export const previewRecordJson = `{
  "customerId": "C0001",
  "name": "Somchai Jaidee",
  "registeredDate": "2026-10-19",
  "active": true
}`;

export const inputClassName =
  "mt-2 w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm text-slate-950 outline-none transition focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100";

export function updateDraft(
  setDraft: Dispatch<SetStateAction<DraftState>>,
  values: Partial<DraftState>,
) {
  setDraft((current) => ({ ...current, ...values }));
}
