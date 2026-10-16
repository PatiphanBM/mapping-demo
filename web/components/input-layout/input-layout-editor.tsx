"use client";

import { useState } from "react";
import { ErrorPanel } from "@/components/page-state";
import { TransformationEditor } from "@/components/transformation/transformation-editor";
import type {
  DelimitedInputLayout,
  FixedWidthInputLayout,
  InputLayout,
  MappingCapabilities,
  MappingConfigVersion,
  MappingDraftDiagnostic,
  PreviewMappingConfigVersionResponse,
  TableDefinition,
  TransformationProgram,
} from "@/lib/api/contracts";
import {
  createMappingConfigVersion,
  previewMappingConfigVersion,
  validateMappingConfigVersion,
} from "@/lib/api/mapping-configs";
import {
  MAX_SAMPLE_BYTES,
  readUtf8SampleFile,
  SampleInputError,
  validateSampleText,
} from "@/lib/input-preview";
import { createBasicTransformationProgram } from "@/lib/transformation-draft";
import { DelimitedLayoutForm } from "./delimited-layout-form";
import { FixedWidthLayoutForm } from "./fixed-width-layout-form";
import { PreviewPanel } from "./preview-panel";

interface InputLayoutEditorProps {
  configId: number;
  initialLayout?: InputLayout;
  transformationProgram?: TransformationProgram;
  sourceTable: TableDefinition;
  normalizedTable: TableDefinition;
  capabilities: MappingCapabilities;
  versionNo?: number;
  onVersionCreated?: (version: MappingConfigVersion) => void;
}

function firstOrFallback(values: string[], fallback: string) {
  return values[0] ?? fallback;
}

function defaultDelimitedLayout(
  sourceTable: TableDefinition,
  capabilities: MappingCapabilities,
): DelimitedInputLayout {
  return {
    kind: "delimited",
    encoding: firstOrFallback(capabilities.input.encodings, "utf-8"),
    delimiter: ",",
    preambleLines: 0,
    headerPolicy: "SingleRow",
    fields: sourceTable.columns.map((column) => ({
      inputHeader: column.name,
      sourceColumn: column.name,
    })),
  };
}

function defaultFixedWidthLayout(
  sourceTable: TableDefinition,
  capabilities: MappingCapabilities,
): FixedWidthInputLayout {
  const fields = sourceTable.columns.map((column, index) => ({
    sourceColumn: column.name,
    start: index * 10 + 2,
    length: 10,
  }));

  return {
    kind: "fixedWidth",
    encoding: firstOrFallback(capabilities.input.encodings, "utf-8"),
    positionUnit: firstOrFallback(
      capabilities.input.positionUnits,
      "utf16CodeUnit",
    ),
    unmatchedRecordPolicy: "Error",
    recordRules: [
      {
        key: "detail",
        discriminator: { start: 1, length: 1, value: "D" },
        action: "Import",
        expectedLength: Math.max(1, fields.length * 10 + 1),
        fields,
      },
    ],
    recordSequence: [
      { recordRule: "detail", minOccurs: 1, maxOccurs: null },
    ],
  };
}

export function InputLayoutEditor({
  configId,
  initialLayout,
  transformationProgram,
  sourceTable,
  normalizedTable,
  capabilities,
  versionNo,
  onVersionCreated,
}: InputLayoutEditorProps) {
  const [layout, setLayout] = useState<InputLayout>(() =>
    initialLayout ?? defaultDelimitedLayout(sourceTable, capabilities),
  );
  const [program, setProgram] = useState<TransformationProgram>(() =>
    transformationProgram ??
    createBasicTransformationProgram(
      sourceTable,
      normalizedTable,
      capabilities,
    ),
  );
  const [diagnostics, setDiagnostics] = useState<MappingDraftDiagnostic[]>([]);
  const [validating, setValidating] = useState(false);
  const [sampleText, setSampleText] = useState("");
  const [fileName, setFileName] = useState("sample.txt");
  const [sampleError, setSampleError] = useState<SampleInputError | null>(null);
  const [previewError, setPreviewError] = useState<unknown>(null);
  const [preview, setPreview] =
    useState<PreviewMappingConfigVersionResponse | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<unknown>(null);

  const byteCount = new TextEncoder().encode(sampleText).byteLength;
  const sourceColumns = sourceTable.columns.map((column) => column.name);
  const inputDiagnostics = diagnostics.filter((diagnostic) =>
    diagnostic.path.startsWith("inputLayout."),
  );

  function changeLayout(next: InputLayout) {
    setLayout(next);
    setPreview(null);
    setPreviewError(null);
    setSampleError(null);
    setDiagnostics([]);
  }

  function changeProgram(next: TransformationProgram) {
    setProgram(next);
    setPreview(null);
    setPreviewError(null);
    setDiagnostics([]);
  }

  async function requestValidation() {
    const response = await validateMappingConfigVersion(configId, {
      inputLayout: layout,
      transformationProgram: program,
    });
    setDiagnostics(response.diagnostics);
    return response.isValid;
  }

  async function runValidation() {
    setValidating(true);
    setPreviewError(null);
    try {
      await requestValidation();
    } catch (error) {
      setPreviewError(error);
    } finally {
      setValidating(false);
    }
  }

  async function createVersion() {
    setSaving(true);
    setSaveError(null);
    setPreviewError(null);

    try {
      const isValid = await requestValidation();
      if (!isValid) {
        return;
      }

      const created = await createMappingConfigVersion(configId, {
        inputLayout: layout,
        transformationProgram: program,
      });
      onVersionCreated?.(created);
    } catch (error) {
      setSaveError(error);
    } finally {
      setSaving(false);
    }
  }

  function selectFormat(kind: InputLayout["kind"]) {
    if (kind === layout.kind) {
      return;
    }

    changeLayout(
      kind === "delimited"
        ? defaultDelimitedLayout(sourceTable, capabilities)
        : defaultFixedWidthLayout(sourceTable, capabilities),
    );
  }

  async function chooseFile(file: File | undefined) {
    if (!file) {
      return;
    }

    setPreview(null);
    setPreviewError(null);
    try {
      const content = await readUtf8SampleFile(file, layout);
      setSampleText(content);
      setFileName(file.name);
      setSampleError(null);
    } catch (error) {
      setSampleText("");
      setFileName(file.name);
      setSampleError(
        error instanceof SampleInputError
          ? error
          : new SampleInputError(
              "InvalidEncoding",
              "InvalidEncoding: The selected file could not be read.",
            ),
      );
    }
  }

  async function runPreview() {
    setPreviewing(true);
    setPreview(null);
    setPreviewError(null);
    setSampleError(null);

    try {
      validateSampleText(sampleText, layout);
      const isValid = await requestValidation();
      if (!isValid) {
        return;
      }
      const response = await previewMappingConfigVersion(configId, {
        evaluationAt: new Date().toISOString(),
        sample: { kind: "textFile", fileName, content: sampleText },
        inputLayout: layout,
        transformationProgram: program,
      });
      setPreview(response);
      const runtimeDiagnostics = response.records.flatMap((record) =>
        (record.transformation?.errors ?? []).map((error) => ({
          path: `transformationProgram.${error.expressionPath}`,
          code: error.reason,
          message: `${record.rowKey}: ${error.reason}`,
          severity: "Error" as const,
        })),
      );
      setDiagnostics(runtimeDiagnostics);
    } catch (error) {
      if (error instanceof SampleInputError) {
        setSampleError(error);
      } else {
        setPreviewError(error);
      }
    } finally {
      setPreviewing(false);
    }
  }

  return (
    <section className="mt-8 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-indigo-600">
            {versionNo
              ? `Draft based on version ${versionNo}`
              : "New version draft"}
          </p>
          <h2 className="mt-1 text-xl font-semibold tracking-tight text-slate-950">
            Input Layout editor
          </h2>
          <p className="mt-1 max-w-3xl text-sm leading-6 text-slate-600">
            Browser checks give fast feedback; parsing and position slicing come
            from the API shared with the Worker. The sample is sent as JSON and
            is not uploaded or persisted as a file.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <div className="inline-flex rounded-xl border border-slate-300 bg-slate-50 p-1">
            {capabilities.input.formats.map((kind) => (
              <button
                className={`rounded-lg px-4 py-2 text-sm font-semibold transition ${
                  layout.kind === kind
                    ? "bg-white text-indigo-700 shadow-sm"
                    : "text-slate-600 hover:text-slate-900"
                }`}
                key={kind}
                onClick={() => selectFormat(kind)}
                type="button"
              >
                {kind === "fixedWidth" ? "Fixed width" : "Delimited"}
              </button>
            ))}
          </div>
          <button
            className="rounded-xl bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:bg-emerald-300"
            disabled={saving || validating || previewing}
            onClick={() => void createVersion()}
            type="button"
          >
            {saving ? "Creating version…" : "Create version"}
          </button>
        </div>
      </div>

      {saveError ? (
        <div className="mt-5">
          <ErrorPanel error={saveError} title="Could not create version" />
        </div>
      ) : null}

      <div className="mt-6 border-t border-slate-100 pt-6">
        {layout.kind === "delimited" ? (
          <DelimitedLayoutForm
            capabilities={capabilities}
            layout={layout}
            onChange={changeLayout}
            sourceColumns={sourceColumns}
          />
        ) : (
          <FixedWidthLayoutForm
            capabilities={capabilities}
            layout={layout}
            onChange={changeLayout}
            sourceColumns={sourceColumns}
          />
        )}
        {inputDiagnostics.length > 0 ? (
          <ul className="mt-4 space-y-1 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900">
            {inputDiagnostics.map((diagnostic, index) => (
              <li key={`${diagnostic.path}:${diagnostic.code}:${index}`}>
                <code className="font-mono text-xs font-semibold">
                  {diagnostic.path}
                </code>
                {": "}
                {diagnostic.code}: {diagnostic.message}
              </li>
            ))}
          </ul>
        ) : null}
      </div>

      <div className="mt-8 border-t border-slate-200 pt-6">
        <div>
          <h3 className="font-semibold text-slate-900">
            Basic transformation
          </h3>
          <p className="mt-1 text-sm text-slate-500">
            Map each normalized target with assignment coercion, a typed
            constant, the current date, or no value.
          </p>
        </div>
        <div className="mt-4">
          <TransformationEditor
            capabilities={capabilities}
            diagnostics={diagnostics}
            normalizedTable={normalizedTable}
            onChange={changeProgram}
            onValidate={() => void runValidation()}
            program={program}
            sourceTable={sourceTable}
            validating={validating}
          />
        </div>
      </div>

      <div className="mt-8 border-t border-slate-200 pt-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h3 className="font-semibold text-slate-900">Raw-record sample</h3>
            <p className="mt-1 text-sm text-slate-500">
              UTF-8 only · up to 1 MiB · up to 50 parsed records
            </p>
          </div>
          <label className="cursor-pointer rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-50">
            Choose UTF-8 file
            <input
              accept="text/plain,.txt,.csv"
              className="sr-only"
              onChange={(event) => {
                void chooseFile(event.target.files?.[0]);
                event.target.value = "";
              }}
              type="file"
            />
          </label>
        </div>
        <textarea
          className="mt-3 min-h-44 w-full rounded-xl border border-slate-300 bg-slate-950 px-4 py-3 font-mono text-sm text-slate-100 outline-none transition focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100"
          onChange={(event) => {
            setSampleText(event.target.value);
            setFileName("pasted-sample.txt");
            setSampleError(null);
            setPreviewError(null);
            setPreview(null);
          }}
          placeholder="Paste raw file content here…"
          spellCheck={false}
          value={sampleText}
        />
        <div className="mt-2 flex flex-wrap items-center justify-between gap-3 text-xs text-slate-500">
          <span>{fileName}</span>
          <span className={byteCount > MAX_SAMPLE_BYTES ? "text-rose-700" : ""}>
            {byteCount.toLocaleString()} / {MAX_SAMPLE_BYTES.toLocaleString()} UTF-8 bytes
          </span>
        </div>

        {sampleError ? (
          <div
            data-error-code={sampleError.code}
            className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900"
            role="alert"
          >
            {sampleError.message}
          </div>
        ) : null}
        {previewError ? (
          <div className="mt-4">
            <ErrorPanel error={previewError} title="Preview failed" />
          </div>
        ) : null}

        <button
          className="mt-4 rounded-xl bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-indigo-700 disabled:cursor-not-allowed disabled:bg-indigo-300"
          disabled={previewing || sampleText.length === 0}
          onClick={() => void runPreview()}
          type="button"
        >
          {previewing ? "Parsing preview…" : "Preview with API"}
        </button>
      </div>

      <div className="mt-8 border-t border-slate-200 pt-6">
        <h3 className="mb-3 font-semibold text-slate-900">Preview records</h3>
        <PreviewPanel preview={preview} />
      </div>
    </section>
  );
}
