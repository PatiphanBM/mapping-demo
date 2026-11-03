"use client";

import { useEffect, useMemo, useState } from "react";
import { ApiContractPreview } from "@/components/contracts/api-contract-preview";
import { JsonFilePreview } from "@/components/contracts/json-file-preview";
import { DelimitedLayoutForm } from "@/components/input-layout/delimited-layout-form";
import { ExcelLayoutForm } from "@/components/input-layout/excel-layout-form";
import { FixedWidthLayoutForm } from "@/components/input-layout/fixed-width-layout-form";
import { ErrorPanel, LoadingPanel } from "@/components/page-state";
import type {
  ApiDestinationProfile,
  ArrayGroup,
  ContractPathDescriptor,
  ContractSchemaResolveResponse,
  ContractResolveResponse,
  DestinationDefinition,
  ExpressionValueType,
  FileDestinationProfileSummary,
  InputLayout,
  JsonValue,
  MappingCapabilities,
  MappingConfigVersion,
  MappingConfigVersionDetails,
  PreviewMappingConfigVersionResponse,
  TableDefinition,
  TransformationProgram,
  ValueExpression,
} from "@/lib/api/contracts";
import {
  createMappingConfigVersion,
  previewMappingConfigFile,
  previewMappingConfigVersion,
  resolveApiContract,
  resolveFileContract,
} from "@/lib/api/mapping-configs";
import {
  acceptedExtensionsForLayout,
  defaultDelimitedLayout,
  defaultExcelLayout,
  defaultFixedWidthLayout,
} from "@/lib/input-layout-draft";

interface Props {
  configId: number;
  destination: Extract<DestinationDefinition, { kind: "apiContract" | "jsonFile" }>;
  capabilities: MappingCapabilities;
  profile: ApiDestinationProfile | FileDestinationProfileSummary;
  sourceTable: TableDefinition;
  version?: MappingConfigVersionDetails;
  onVersionCreated(version: MappingConfigVersion): void;
}

interface SlotDraft {
  id: number;
  sourceSlot: number;
  fields: SlotFieldDraft[];
}

interface SlotFieldDraft {
  id: number;
  sourceField: string;
  targetPath: string;
}

const inputClass =
  "block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-950";

export function ApiContractEditor({
  capabilities,
  configId,
  destination,
  profile,
  sourceTable,
  version,
  onVersionCreated,
}: Props) {
  const initialOutput =
    version?.transformationProgram.languageVersion === 2
      ? version.transformationProgram.outputs[0]
      : undefined;
  const initialGroup = initialOutput?.arrayGroups?.[0];
  const [contract, setContract] = useState<ContractSchemaResolveResponse | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState<"create" | "preview" | "filePreview" | null>(null);
  const [layout, setLayout] = useState<InputLayout>(() =>
    version?.inputLayout ?? defaultDelimitedLayout(sourceTable, capabilities),
  );
  const [sampleFile, setSampleFile] = useState<File | null>(null);
  const [search, setSearch] = useState("");
  const [assignments, setAssignments] = useState<Record<string, ValueExpression>>(() =>
    Object.fromEntries((initialOutput?.fields ?? []).flatMap((field) =>
      field.targetPath ? [[field.targetPath, field.value]] : [],
    )),
  );
  const [inputFormats, setInputFormats] = useState<Record<string, string>>(() =>
    Object.fromEntries((initialOutput?.fields ?? []).flatMap((field) =>
      field.targetPath && field.inputFormat
        ? [[field.targetPath, field.inputFormat]]
        : [],
    )),
  );
  const [arrayPath, setArrayPath] = useState(initialGroup?.targetPath ?? "");
  const [sequencePath, setSequencePath] = useState(initialGroup?.sequencePath ?? "");
  const [slots, setSlots] = useState<SlotDraft[]>(() =>
    initialGroup?.sourceSlots.map((slot, index) => ({
      id: index + 1,
      sourceSlot: slot.sourceSlot,
      fields: slot.fields.flatMap((field, fieldIndex) =>
        field.value.kind === "source"
          ? [{
              id: (index + 1) * 100 + fieldIndex,
              sourceField: field.value.field,
              targetPath: field.targetPath,
            }]
          : [],
      ),
    })) ?? [],
  );
  const [sample, setSample] = useState<Record<string, string>>(() =>
    Object.fromEntries(sourceTable.columns.map((column) => [column.name, ""])),
  );
  const [preview, setPreview] = useState<PreviewMappingConfigVersionResponse | null>(null);

  useEffect(() => {
    let cancelled = false;
    const request = destination.kind === "apiContract"
      ? resolveApiContract(destination.apiDestinationProfileId, destination.contract)
      : resolveFileContract(destination.fileDestinationProfileId, destination.contract);
    request
      .then((value) => {
        if (!cancelled) setContract(value);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason);
      });
    return () => {
      cancelled = true;
    };
  }, [destination]);

  const systemPaths = useMemo(
    () => new Map(profile.payloadBindings.map((binding) => [binding.targetPath, binding.source])),
    [profile],
  );
  const visiblePaths = (contract?.paths ?? []).filter((path) =>
    path.path.toLowerCase().includes(search.toLowerCase()),
  );
  const arrays = (contract?.paths ?? []).filter((path) => path.kind === "Array" && !path.path.endsWith("[]"));
  const selectedArrayPath = arrays.some((path) => path.path === arrayPath) ? arrayPath : "";
  const itemLeaves = selectedArrayPath
    ? (contract?.paths ?? []).filter((path) => path.isAssignable && path.path.startsWith(`${selectedArrayPath}[].`))
    : [];
  const sequenceCandidates = itemLeaves.filter((path) => path.scalarType === "Integer");
  const businessPaths = (contract?.paths ?? []).filter((path) =>
    path.isAssignable && !path.path.includes("[]") && !systemPaths.has(path.path),
  );
  const businessPathNames = new Set(businessPaths.map((path) => path.path));

  function program(): TransformationProgram {
    const arrayGroups: ArrayGroup[] = selectedArrayPath && sequencePath
      ? [{
          targetPath: selectedArrayPath,
          sequencePath,
          emptyItemPolicy: "SkipWhenAllBusinessFieldsEmpty",
          sourceSlots: slots
            .sort((left, right) => left.sourceSlot - right.sourceSlot)
            .map((slot) => ({
              sourceSlot: slot.sourceSlot,
              fields: slot.fields
                .filter((field) => field.sourceField && field.targetPath)
                .map((field) => ({
                  targetPath: field.targetPath,
                  value: { kind: "source" as const, field: field.sourceField },
                })),
            })),
        }]
      : [];
    return {
      languageVersion: 2,
      semanticsProfile: "sqlserver-legacy-v1",
      outputs: [{
        outputKey: contractOutputKey(initialOutput?.outputKey, profile, contract),
        fields: Object.entries(assignments)
          .filter(([targetPath]) => businessPathNames.has(targetPath))
          .map(([targetPath, value]) => ({
            targetPath,
            inputFormat: inputFormats[targetPath] || undefined,
            value,
          })),
        arrayGroups,
      }],
    };
  }

  const sourceColumns = sourceTable.columns.map((column) => column.name);

  function changeLayout(next: InputLayout) {
    setLayout(next);
    setPreview(null);
    setError(null);
  }

  function selectFormat(kind: InputLayout["kind"]) {
    if (kind === layout.kind) return;
    changeLayout(kind === "delimited"
      ? defaultDelimitedLayout(sourceTable, capabilities)
      : kind === "fixedWidth"
        ? defaultFixedWidthLayout(sourceTable, capabilities)
        : defaultExcelLayout(sourceTable));
    setSampleFile(null);
  }

  function applyPaMtlInformStarter() {
    setAssignments(paMtlInformStarterAssignments(sourceTable.columns.map((column) => column.name)));
    setInputFormats({});
    setArrayPath("inform.beneficiaries");
    setSequencePath("seq");
    setSlots([
      { id: 1, sourceSlot: 1, fields: [
        { id: 101, sourceField: "beneficiary_1", targetPath: "fullName" },
        { id: 102, sourceField: "relationship_1", targetPath: "relationship" },
      ] },
      { id: 2, sourceSlot: 2, fields: [
        { id: 201, sourceField: "beneficiary_2", targetPath: "fullName" },
        { id: 202, sourceField: "relationship_2", targetPath: "relationship" },
      ] },
    ]);
    setPreview(null);
    setError(null);
  }

  async function createVersion() {
    if (contract === null) return;
    setBusy("create");
    setError(null);
    try {
      const created = await createMappingConfigVersion(configId, {
        inputLayout: layout,
        transformationProgram: program(),
        destination: { ...destination, contract: contract.contract },
        acceptedExtensions: acceptedExtensionsForLayout(layout),
      });
      onVersionCreated(created);
    } catch (reason) {
      setError(reason);
    } finally {
      setBusy(null);
    }
  }

  async function runPreview() {
    if (contract === null) return;
    setBusy("preview");
    setError(null);
    try {
      const value = await previewMappingConfigVersion(configId, {
        evaluationAt: new Date().toISOString(),
        inputLayout: layout,
        transformationProgram: program(),
        destination: { ...destination, contract: contract.contract },
        acceptedExtensions: acceptedExtensionsForLayout(layout),
        sample: {
          kind: "sourceRows",
          rows: [{ values: sample, rowKey: "preview-1", rowNumber: 1, sourceRowId: 1 }],
        },
      });
      setPreview(value);
    } catch (reason) {
      setError(reason);
    } finally {
      setBusy(null);
    }
  }

  async function runWorkbookPreview() {
    if (contract === null || layout.kind !== "excel" || sampleFile === null) return;
    setBusy("filePreview");
    setError(null);
    try {
      const value = await previewMappingConfigFile(configId, sampleFile, {
        evaluationAt: new Date().toISOString(),
        inputLayout: layout,
        transformationProgram: program(),
        destination: { ...destination, contract: contract.contract },
        acceptedExtensions: acceptedExtensionsForLayout(layout),
      });
      setPreview(value);
    } catch (reason) {
      setError(reason);
    } finally {
      setBusy(null);
    }
  }

  if (error && contract === null) {
    return <ErrorPanel error={error} title="Could not resolve contract schema" />;
  }
  if (contract === null) return <LoadingPanel label="Resolving contract schema…" />;

  const drift = version?.contractSnapshot && version.contractSnapshot.schemaHash !== contract.schemaHash;
  const apiContract = destination.kind === "apiContract" && "requestSummary" in contract
    ? contract as ContractResolveResponse
    : null;

  return (
    <section className="mt-8 space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" id="api-contract-editor">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-indigo-600">{destination.kind === "apiContract" ? "API Contract" : "Whole file · JSON file"} · language v2</p>
          <h2 className="mt-1 text-xl font-semibold text-slate-950">{"displayName" in profile ? profile.displayName : profile.id}</h2>
          <p className="mt-2 font-mono text-xs text-slate-600">{contract.contract.reference} · {contract.schemaHash}</p>
          <p className="mt-1 text-xs text-slate-500">Fetched {new Date(contract.fetchedAt).toLocaleString()}{apiContract ? ` · ${apiContract.requestSummary.method} ${apiContract.requestSummary.pathTemplate}` : " · schema-only file contract"}</p>
          {drift ? <p className="mt-2 text-sm font-semibold text-amber-700">Schema drift detected from the pinned version. Creating a version will snapshot the current schema.</p> : null}
        </div>
        <div className="flex gap-2">
          <button className="rounded-lg border border-indigo-300 px-3 py-2 text-sm font-semibold text-indigo-800 disabled:opacity-50" disabled={busy !== null} onClick={runPreview} type="button">{busy === "preview" ? "Previewing…" : "Preview"}</button>
          <button className="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-semibold text-white disabled:bg-indigo-300" disabled={busy !== null} onClick={createVersion} type="button">{busy === "create" ? "Creating…" : "Create version"}</button>
        </div>
      </div>

      {error ? <ErrorPanel error={error} title="Contract draft action failed" /> : null}

      {contract.contract.reference === "pa-inform/1" && "displayName" in profile ? (
        <div className="rounded-xl border border-indigo-200 bg-indigo-50 p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="text-sm font-semibold text-indigo-950">PA MTLS starter mapping</p>
              <p className="mt-1 text-xs text-indigo-800">Maps the MTLS source columns, fixed contract values, nullable saleId, and both beneficiary slots. Review the values before creating a version.</p>
            </div>
            <button className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white" onClick={applyPaMtlInformStarter} type="button">Apply PA MTLS starter mapping</button>
          </div>
        </div>
      ) : null}

      <div className="border-t border-slate-100 pt-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h3 className="font-semibold text-slate-900">Input file layout</h3>
            <p className="mt-1 text-sm text-slate-500">Choose how the API and Worker read the incoming file.</p>
          </div>
          <div className="inline-flex rounded-xl border border-slate-300 bg-slate-50 p-1">
            {capabilities.input.formats.map((kind) => (
              <button
                className={`rounded-lg px-4 py-2 text-sm font-semibold transition ${layout.kind === kind ? "bg-white text-indigo-700 shadow-sm" : "text-slate-600 hover:text-slate-900"}`}
                key={kind}
                onClick={() => selectFormat(kind)}
                type="button"
              >
                {kind === "fixedWidth" ? "Fixed width" : kind === "excel" ? "Excel" : "Delimited"}
              </button>
            ))}
          </div>
        </div>
        <div className="mt-5">
          {layout.kind === "delimited" ? (
            <DelimitedLayoutForm capabilities={capabilities} layout={layout} onChange={changeLayout} sourceColumns={sourceColumns} />
          ) : layout.kind === "fixedWidth" ? (
            <FixedWidthLayoutForm capabilities={capabilities} layout={layout} onChange={changeLayout} sourceColumns={sourceColumns} />
          ) : (
            <ExcelLayoutForm layout={layout} onChange={changeLayout} sourceColumns={sourceColumns} />
          )}
        </div>
        {layout.kind === "excel" ? (
          <div className="mt-5 rounded-xl border border-indigo-200 bg-indigo-50 p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="text-sm font-semibold text-indigo-950">Workbook sample</p>
                <p className="mt-1 text-xs text-indigo-800">Uploaded only for preview; the API parses it without persisting the file.</p>
              </div>
              <label className="cursor-pointer rounded-lg border border-indigo-300 bg-white px-3 py-2 text-sm font-semibold text-indigo-800">
                Choose workbook
                <input
                  accept={layout.workbookKind === "Xls" ? ".xls" : layout.workbookKind === "Xlsx" ? ".xlsx" : ".xlsx,.xls"}
                  aria-label="Choose workbook"
                  className="sr-only"
                  onChange={(event) => {
                    setSampleFile(event.target.files?.[0] ?? null);
                    setPreview(null);
                    setError(null);
                    event.target.value = "";
                  }}
                  type="file"
                />
              </label>
            </div>
            <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
              <span className="text-xs text-indigo-900">{sampleFile ? `${sampleFile.name} · ${sampleFile.size.toLocaleString()} bytes` : "No workbook selected"}</span>
              <button
                className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:bg-indigo-300"
                disabled={busy !== null || sampleFile === null}
                onClick={() => void runWorkbookPreview()}
                type="button"
              >
                {busy === "filePreview" ? "Previewing workbook…" : "Preview workbook with API"}
              </button>
            </div>
          </div>
        ) : null}
      </div>

      <div>
        <label className="text-sm font-semibold text-slate-800">Search contract paths<input className={`${inputClass} mt-2`} onChange={(event) => setSearch(event.target.value)} placeholder="insured, claim, date…" value={search} /></label>
        <div className="mt-3 max-h-96 overflow-auto rounded-xl border border-slate-200">
          <table className="w-full text-left text-sm">
            <thead className="sticky top-0 bg-slate-50 text-xs uppercase text-slate-500"><tr><th className="px-3 py-2">Contract path</th><th className="px-3 py-2">Type</th><th className="px-3 py-2">Source expression</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {visiblePaths.map((path) => {
                const systemSource = systemPaths.get(path.path);
                const temporalFormats = path.scalarType === "Date"
                  ? capabilities.transformation.inputFormats.date
                  : path.scalarType === "Time"
                    ? capabilities.transformation.inputFormats.time
                    : path.scalarType === "DateTime"
                      ? capabilities.transformation.inputFormats.dateTime
                      : [];
                return <tr key={path.path}>
                  <td className="px-3 py-3"><span className="font-mono text-xs">{path.path}</span><span className="ml-2 text-xs text-slate-400">{path.isRequired ? "required" : path.isNullable ? "nullable" : "optional"}</span>{path.description ? <p className="mt-1 text-xs text-slate-500">{path.description}</p> : null}</td>
                  <td className="px-3 py-3 text-xs text-slate-600">{path.kind}{path.scalarType ? ` · ${path.scalarType}` : ""}</td>
                  <td className="px-3 py-3">
                    {systemSource ? <span className="rounded bg-slate-100 px-2 py-1 text-xs font-semibold text-slate-600">System · {systemSource}</span> : path.isAssignable && !path.path.includes("[]") ? <AssignmentEditor assignment={assignments[path.path]} inputFormat={inputFormats[path.path] ?? ""} inputClass={inputClass} onAssignmentChange={(assignment) => { setAssignments((current) => { const next = { ...current }; if (assignment) next[path.path] = assignment; else delete next[path.path]; return next; }); if (!assignment || assignment.kind !== "source") setInputFormats((current) => { const next = { ...current }; delete next[path.path]; return next; }); }} onInputFormatChange={(format) => setInputFormats((current) => ({ ...current, [path.path]: format }))} path={path} sourceColumns={sourceTable.columns.map((column) => column.name)} temporalFormats={temporalFormats} /> : <span className="text-xs text-slate-400">Container / Array Group only</span>}
                  </td>
                </tr>;
              })}
            </tbody>
          </table>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
        <h3 className="font-semibold text-slate-900">Array Group</h3>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <label className="text-xs font-semibold text-slate-600">Target array<select className={`${inputClass} mt-1`} onChange={(event) => { setArrayPath(event.target.value); setSequencePath(""); setSlots([]); }} value={arrayPath}><option value="">No array group</option>{arrays.map((path) => <option key={path.path} value={path.path}>{path.path}</option>)}</select></label>
          <label className="text-xs font-semibold text-slate-600">Sequence integer leaf<select className={`${inputClass} mt-1`} disabled={!selectedArrayPath} onChange={(event) => setSequencePath(event.target.value)} value={selectedArrayPath ? sequencePath : ""}><option value="">Choose sequence</option>{sequenceCandidates.map((path) => <option key={path.path} value={path.path.slice(`${selectedArrayPath}[].`.length)}>{path.path}</option>)}</select></label>
        </div>
        <div className="mt-3 space-y-3">{slots.map((slot, index) => <div className="rounded-lg bg-white p-3" key={slot.id}><div className="flex items-center gap-2"><label className="w-28 text-xs font-semibold text-slate-600">Source slot<input aria-label="Source slot" className={`${inputClass} mt-1`} min={1} onChange={(event) => setSlots((current) => current.map((item) => item.id === slot.id ? { ...item, sourceSlot: Number(event.target.value) } : item))} type="number" value={slot.sourceSlot} /></label><div className="ml-auto flex gap-2"><button aria-label="Move slot up" disabled={index === 0} onClick={() => setSlots((current) => { const next = [...current]; [next[index - 1], next[index]] = [next[index], next[index - 1]]; return next; })} type="button">↑</button><button aria-label="Remove slot" className="text-rose-700" onClick={() => setSlots((current) => current.filter((item) => item.id !== slot.id))} type="button">Remove slot</button></div></div><div className="mt-3 space-y-2">{slot.fields.map((field) => <div className="grid gap-2 sm:grid-cols-[1fr_1fr_auto]" key={field.id}><select className={inputClass} onChange={(event) => setSlots((current) => current.map((item) => item.id === slot.id ? { ...item, fields: item.fields.map((candidate) => candidate.id === field.id ? { ...candidate, sourceField: event.target.value } : candidate) } : item))} value={field.sourceField}><option value="">Source field</option>{sourceTable.columns.map((column) => <option key={column.name} value={column.name}>{column.name}</option>)}</select><select className={inputClass} onChange={(event) => setSlots((current) => current.map((item) => item.id === slot.id ? { ...item, fields: item.fields.map((candidate) => candidate.id === field.id ? { ...candidate, targetPath: event.target.value } : candidate) } : item))} value={field.targetPath}><option value="">Relative item path</option>{itemLeaves.filter((path) => path.path.slice(`${arrayPath}[].`.length) !== sequencePath).map((path) => <option key={path.path} value={path.path.slice(`${arrayPath}[].`.length)}>{path.path}</option>)}</select><button aria-label="Remove item field" onClick={() => setSlots((current) => current.map((item) => item.id === slot.id ? { ...item, fields: item.fields.filter((candidate) => candidate.id !== field.id) } : item))} type="button">×</button></div>)}</div><button className="mt-2 text-xs font-semibold text-indigo-700" onClick={() => setSlots((current) => current.map((item) => item.id === slot.id ? { ...item, fields: [...item.fields, { id: Date.now(), sourceField: "", targetPath: "" }] } : item))} type="button">Add item field</button></div>)}</div>
        {selectedArrayPath ? <button className="mt-3 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold" onClick={() => setSlots((current) => [...current, { id: Date.now(), sourceSlot: current.length + 1, fields: [{ id: Date.now() + 1, sourceField: "", targetPath: "" }] }])} type="button">Add source slot</button> : null}
        <p className="mt-2 text-xs text-slate-500">Blank, whitespace and “-” items are skipped; 0 and false are retained. Emitted items are compact while seq keeps the source slot.</p>
      </div>

      <div>
        <h3 className="font-semibold text-slate-900">Preview input values</h3>
        <div className="mt-3 grid gap-2 sm:grid-cols-3">{sourceTable.columns.map((column) => <label className="text-xs font-semibold text-slate-600" key={column.name}>{column.name}<input className={`${inputClass} mt-1`} onChange={(event) => setSample((current) => ({ ...current, [column.name]: event.target.value }))} value={sample[column.name] ?? ""} /></label>)}</div>
      </div>

      {apiContract && "displayName" in profile ? (
        <ApiContractPreview contract={apiContract} preview={preview} profile={profile} sample={sample} />
      ) : destination.kind === "jsonFile" && !("displayName" in profile) ? (
        <JsonFilePreview contract={contract} preview={preview} profile={profile} />
      ) : null}
    </section>
  );
}

interface AssignmentEditorProps {
  assignment?: ValueExpression;
  inputFormat: string;
  inputClass: string;
  path: ContractPathDescriptor;
  sourceColumns: string[];
  temporalFormats: string[];
  onAssignmentChange(assignment: ValueExpression | null): void;
  onInputFormatChange(format: string): void;
}

function AssignmentEditor({
  assignment,
  inputFormat,
  inputClass,
  onAssignmentChange,
  onInputFormatChange,
  path,
  sourceColumns,
  temporalFormats,
}: AssignmentEditorProps) {
  const mode = assignment?.kind === "source"
    ? assignment.field
    : assignment?.kind === "literal" && assignment.value === null
      ? "@null"
      : assignment?.kind === "literal"
        ? "@literal"
        : assignment
          ? "@advanced"
          : "";
  const literal = assignment?.kind === "literal" && assignment.value !== null
    ? assignment
    : null;

  return (
    <div className="space-y-2">
      <select
        aria-label={`Expression for ${path.path}`}
        className={inputClass}
        onChange={(event) => {
          const value = event.target.value;
          if (!value) return onAssignmentChange(null);
          if (value === "@null") {
            return onAssignmentChange({ kind: "literal", type: expressionTypeForPath(path), value: null });
          }
          if (value === "@literal") {
            return onAssignmentChange({
              kind: "literal",
              type: expressionTypeForPath(path),
              value: defaultLiteralForPath(path),
            });
          }
          if (value !== "@advanced") {
            onAssignmentChange({ kind: "source", field: value });
          }
        }}
        value={mode}
      >
        <option value="">Not mapped</option>
        <optgroup label="Source column">
          {sourceColumns.map((column) => <option key={column} value={column}>{column}</option>)}
        </optgroup>
        <option value="@literal">Literal value</option>
        {path.isNullable ? <option value="@null">Literal null</option> : null}
        {mode === "@advanced" ? <option value="@advanced">Advanced expression (preserved)</option> : null}
      </select>
      {literal ? (
        literal.type === "boolean" ? (
          <select
            aria-label={`Literal value for ${path.path}`}
            className={inputClass}
            onChange={(event) => onAssignmentChange({ ...literal, value: event.target.value === "true" })}
            value={String(literal.value)}
          >
            <option value="true">true</option>
            <option value="false">false</option>
          </select>
        ) : (
          <input
            aria-label={`Literal value for ${path.path}`}
            className={inputClass}
            onChange={(event) => onAssignmentChange({
              ...literal,
              value: literal.type === "integer" || literal.type === "decimal"
                ? Number(event.target.value)
                : event.target.value,
            })}
            type={literal.type === "integer" || literal.type === "decimal" ? "number" : "text"}
            value={String(literal.value)}
          />
        )
      ) : null}
      {assignment?.kind === "source" && temporalFormats.length > 0 ? (
        <label className="block text-xs font-semibold text-slate-600">Input format
          <select aria-label={`Input format for ${path.path}`} className={`${inputClass} mt-1`} onChange={(event) => onInputFormatChange(event.target.value)} value={inputFormat}>
            <option value="">Default</option>
            {temporalFormats.map((format) => <option key={format} value={format}>{format}</option>)}
          </select>
        </label>
      ) : null}
    </div>
  );
}

function expressionTypeForPath(path: ContractPathDescriptor): ExpressionValueType {
  switch (path.scalarType) {
    case "Integer": return "integer";
    case "Number": return "decimal";
    case "Boolean": return "boolean";
    case "Date": return "date";
    case "Time": return "time";
    case "DateTime": return "dateTime";
    case "Guid": return "guid";
    default: return "text";
  }
}

function defaultLiteralForPath(path: ContractPathDescriptor): JsonValue {
  switch (expressionTypeForPath(path)) {
    case "integer":
    case "decimal": return 0;
    case "boolean": return false;
    case "date": return "2026-01-01";
    case "time": return "00:00";
    case "dateTime": return "2026-01-01T00:00:00Z";
    case "guid": return "00000000-0000-0000-0000-000000000000";
    default: return "";
  }
}

function contractOutputKey(
  initialOutputKey: string | undefined,
  profile: ApiDestinationProfile | FileDestinationProfileSummary,
  contract: ContractSchemaResolveResponse | null,
) {
  const outputKeyPath = profile.payloadBindings.find((binding) => binding.source === "OutputKey")?.targetPath;
  const pattern = contract?.paths.find((path) => path.path === outputKeyPath)?.constraints?.pattern;
  if (pattern && initialOutputKey) {
    try {
      if (new RegExp(pattern).test(initialOutputKey)) return initialOutputKey;
    } catch {
      // A malformed schema regex is reported by server-side contract validation.
    }
  }
  const fixedValue = pattern?.match(/^\^([A-Za-z0-9._/-]+)\$$/)?.[1];
  return fixedValue ?? initialOutputKey ?? "main";
}

function paMtlInformStarterAssignments(sourceColumns: string[]): Record<string, ValueExpression> {
  const available = new Set(sourceColumns);
  const result: Record<string, ValueExpression> = {
    "inform.partnerCode": { kind: "literal", type: "text", value: "MTLS" },
    "inform.saleChannel": { kind: "literal", type: "text", value: "FILE" },
    "inform.transactionType": { kind: "literal", type: "text", value: "NEW" },
    "inform.campaignCode": { kind: "literal", type: "text", value: "MTLSPA" },
    "inform.packageCode": { kind: "literal", type: "text", value: "MTL2" },
    "inform.agentCode": { kind: "literal", type: "text", value: "74000490" },
    "inform.saleId": { kind: "literal", type: "text", value: null },
    "inform.insured.isPolicyholder": { kind: "literal", type: "boolean", value: true },
    "inform.insured.relation": { kind: "literal", type: "text", value: "SELF" },
  };
  const sources: Record<string, string> = {
    "inform.sourceReference": "pa_number",
    "inform.sourceTransactionType": "transaction_type",
    "inform.effectiveDate": "coverage_start_date",
    "inform.expiryDate": "coverage_end_date",
    "inform.coverStartTime": "coverage_start_time",
    "inform.totalPremium": "amount",
    "inform.salesBranchCode": "branch_code",
    "inform.salesBranchName": "branch_name",
    "inform.insured.fullName": "full_name",
    "inform.insured.idNo": "national_id",
    "inform.insured.birthDate": "birth_date",
    "inform.insured.declaredAge": "age",
    "inform.insured.occupation": "occupation",
    "inform.insured.address": "address",
  };
  for (const [targetPath, source] of Object.entries(sources)) {
    if (available.has(source)) result[targetPath] = { kind: "source", field: source };
  }
  return result;
}
