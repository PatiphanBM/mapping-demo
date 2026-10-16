"use client";

import { useState } from "react";
import type {
  FieldAssignment,
  MappingCapabilities,
  MappingDraftDiagnostic,
  TableDefinition,
  TransformationOutput,
  TransformationProgram,
  ValueExpression,
} from "@/lib/api/contracts";
import {
  assignmentPathByColumn,
  diagnosticForMissingTarget,
  diagnosticsAtPath,
  expressionValueType,
  transformationDependencies,
  transformationExpressionStats,
} from "@/lib/transformation-draft";
import {
  BooleanExpressionEditor,
  ValueExpressionEditor,
} from "./value-expression-editor";

interface TransformationEditorProps {
  program: TransformationProgram;
  sourceTable: TableDefinition;
  normalizedTable: TableDefinition;
  capabilities: MappingCapabilities;
  diagnostics: MappingDraftDiagnostic[];
  validating: boolean;
  onChange: (program: TransformationProgram) => void;
  onValidate: () => void;
}

const inputClassName =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-950 outline-none transition focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100";

function Messages({ messages }: { messages: MappingDraftDiagnostic[] }) {
  if (messages.length === 0) return null;

  return (
    <ul className="mt-1 space-y-1 text-xs font-medium text-rose-700">
      {messages.map((diagnostic, index) => (
        <li key={`${diagnostic.path}:${diagnostic.code}:${index}`}>
          {diagnostic.code}: {diagnostic.message}
        </li>
      ))}
    </ul>
  );
}

function uniqueOutputKey(program: TransformationProgram, base: string) {
  const keys = new Set(program.outputs.map((output) => output.outputKey));
  if (!keys.has(base)) return base;

  let suffix = 2;
  while (keys.has(`${base}-${suffix}`)) suffix += 1;
  return `${base}-${suffix}`;
}

function cloneOutput(output: TransformationOutput): TransformationOutput {
  return structuredClone(output);
}

export function TransformationEditor({
  program,
  sourceTable,
  normalizedTable,
  capabilities,
  diagnostics,
  validating,
  onChange,
  onValidate,
}: TransformationEditorProps) {
  const [selectedOutputIndex, setSelectedOutputIndex] = useState(0);
  const activeIndex = Math.min(
    selectedOutputIndex,
    Math.max(program.outputs.length - 1, 0),
  );
  const output = program.outputs[activeIndex];
  const limits = capabilities.transformation.limits;
  const stats = transformationExpressionStats(program);
  const dependencies = transformationDependencies(program);
  const fieldsPath = `transformationProgram.outputs[${activeIndex}].fields`;
  const pathByColumn = assignmentPathByColumn(program, activeIndex);
  const transformationDiagnostics = diagnostics.filter((diagnostic) =>
    diagnostic.path.startsWith("transformationProgram."),
  );
  const canAddOutput = program.outputs.length < limits.maxOutputs;
  const canCloneOutput =
    output &&
    canAddOutput &&
    transformationExpressionStats({
      ...program,
      outputs: [...program.outputs, cloneOutput(output)],
    }).nodes <= limits.maxExpressionNodes;

  if (!output) {
    return (
      <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">
        The transformation requires at least one output. Reset this draft from a
        version or start a new draft.
      </div>
    );
  }

  function changeOutput(
    update: (current: TransformationOutput) => TransformationOutput,
  ) {
    onChange({
      ...program,
      outputs: program.outputs.map((item, index) =>
        index === activeIndex ? update(item) : item,
      ),
    });
  }

  function changeAssignment(
    normalizedColumn: string,
    value: ValueExpression | null,
  ) {
    const existingIndex = output.fields.findIndex(
      (field) =>
        field.normalizedColumn.toLowerCase() === normalizedColumn.toLowerCase(),
    );

    changeOutput((current) => {
      if (value === null) {
        return {
          ...current,
          fields: current.fields.filter(
            (_, fieldIndex) => fieldIndex !== existingIndex,
          ),
        };
      }

      const next: FieldAssignment = {
        normalizedColumn,
        value,
        ...(existingIndex >= 0
          ? { inputFormat: current.fields[existingIndex].inputFormat }
          : {}),
      };
      return {
        ...current,
        fields:
          existingIndex >= 0
            ? current.fields.map((field, fieldIndex) =>
                fieldIndex === existingIndex ? next : field,
              )
            : [...current.fields, next],
      };
    });
  }

  function changeInputFormat(normalizedColumn: string, inputFormat: string) {
    changeOutput((current) => ({
      ...current,
      fields: current.fields.map((field) =>
        field.normalizedColumn.toLowerCase() === normalizedColumn.toLowerCase()
          ? { ...field, inputFormat: inputFormat || undefined }
          : field,
      ),
    }));
  }

  function addOutput() {
    if (!canAddOutput) return;
    const next: TransformationOutput = {
      outputKey: uniqueOutputKey(program, "output"),
      fields: [],
    };
    onChange({ ...program, outputs: [...program.outputs, next] });
    setSelectedOutputIndex(program.outputs.length);
  }

  function duplicateOutput() {
    if (!canCloneOutput) return;
    const next = cloneOutput(output);
    next.outputKey = uniqueOutputKey(program, `${output.outputKey}-copy`);
    onChange({ ...program, outputs: [...program.outputs, next] });
    setSelectedOutputIndex(program.outputs.length);
  }

  function removeOutput() {
    if (program.outputs.length <= 1) return;
    onChange({
      ...program,
      outputs: program.outputs.filter((_, index) => index !== activeIndex),
    });
    setSelectedOutputIndex(Math.max(0, activeIndex - 1));
  }

  return (
    <div className="space-y-6">
      <section className="rounded-xl border border-slate-200 bg-slate-50 p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h3 className="font-semibold text-slate-900">Common source filter</h3>
            <p className="mt-1 text-xs text-slate-500">
              Applied once before any output branch is evaluated.
            </p>
          </div>
          <button
            className="rounded-lg border border-indigo-300 bg-white px-4 py-2 text-sm font-semibold text-indigo-800 transition hover:bg-indigo-50 disabled:opacity-50"
            disabled={validating}
            onClick={onValidate}
            type="button"
          >
            {validating ? "Validating…" : "Validate draft"}
          </button>
        </div>
        <div className="mt-3">
          <BooleanExpressionEditor
            capabilities={capabilities}
            diagnostics={diagnostics}
            onChange={(sourceWhen) =>
              onChange({ ...program, sourceWhen: sourceWhen ?? undefined })
            }
            path="transformationProgram.sourceWhen"
            programNodeCount={stats.nodes}
            sourceTable={sourceTable}
            value={program.sourceWhen ?? null}
          />
        </div>
      </section>

      <section className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap gap-2" role="tablist">
            {program.outputs.map((item, index) => (
              <button
                aria-selected={index === activeIndex}
                className={`rounded-lg border px-3 py-2 text-sm font-semibold transition ${
                  index === activeIndex
                    ? "border-indigo-600 bg-indigo-600 text-white"
                    : "border-slate-300 bg-white text-slate-700 hover:border-indigo-300"
                }`}
                key={`${item.outputKey}:${index}`}
                onClick={() => setSelectedOutputIndex(index)}
                role="tab"
                type="button"
              >
                {item.outputKey || `Output ${index + 1}`}
              </button>
            ))}
          </div>
          <div className="flex flex-wrap gap-2">
            <button
              className="rounded-lg border border-slate-300 px-3 py-2 text-xs font-semibold text-slate-700 disabled:opacity-40"
              disabled={!canAddOutput}
              onClick={addOutput}
              type="button"
            >
              Add output
            </button>
            <button
              className="rounded-lg border border-slate-300 px-3 py-2 text-xs font-semibold text-slate-700 disabled:opacity-40"
              disabled={!canCloneOutput}
              onClick={duplicateOutput}
              type="button"
            >
              Clone output
            </button>
            <button
              className="rounded-lg border border-rose-200 px-3 py-2 text-xs font-semibold text-rose-700 disabled:opacity-40"
              disabled={program.outputs.length <= 1}
              onClick={removeOutput}
              type="button"
            >
              Remove output
            </button>
          </div>
        </div>

        <div className="grid gap-4 rounded-xl border border-slate-200 p-4 lg:grid-cols-2">
          <label className="text-sm font-semibold text-slate-700">
            Output key
            <input
              className={`${inputClassName} mt-2 font-mono`}
              onChange={(event) =>
                changeOutput((current) => ({
                  ...current,
                  outputKey: event.target.value,
                }))
              }
              value={output.outputKey}
            />
            <Messages
              messages={diagnosticsAtPath(
                diagnostics,
                `transformationProgram.outputs[${activeIndex}].outputKey`,
              )}
            />
          </label>
          <div>
            <p className="mb-2 text-sm font-semibold text-slate-700">
              Emit this output when
            </p>
            <BooleanExpressionEditor
              capabilities={capabilities}
              diagnostics={diagnostics}
              onChange={(emitWhen) =>
                changeOutput((current) => ({
                  ...current,
                  emitWhen: emitWhen ?? undefined,
                }))
              }
              path={`transformationProgram.outputs[${activeIndex}].emitWhen`}
              programNodeCount={stats.nodes}
              sourceTable={sourceTable}
              value={output.emitWhen ?? null}
            />
          </div>
        </div>
      </section>

      <div className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-slate-500">
        <span>
          Language {program.languageVersion ?? "default"} · {program.semanticsProfile}
        </span>
        <span>
          Outputs {program.outputs.length}/{limits.maxOutputs} · Expressions {stats.nodes}/
          {limits.maxExpressionNodes} · Max depth {stats.nodes === 0 ? 0 : stats.maxDepth + 1}/
          {limits.maxExpressionDepth}
        </span>
        <span>Collection limit {limits.maxCollectionItems}</span>
        <span>
          {transformationDiagnostics.length === 0
            ? "No transformation diagnostics"
            : `${transformationDiagnostics.length} transformation diagnostic(s)`}
        </span>
      </div>

      <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
          Logical dependencies
        </p>
        {dependencies.functions.length === 0 &&
        dependencies.lookups.length === 0 ? (
          <p className="mt-2 text-sm text-slate-500">No named dependencies.</p>
        ) : (
          <div className="mt-2 flex flex-wrap gap-2">
            {dependencies.functions.map((name) => (
              <span
                className="rounded-full bg-violet-100 px-3 py-1 font-mono text-xs font-semibold text-violet-800"
                key={`function:${name}`}
              >
                transform · {name}
              </span>
            ))}
            {dependencies.lookups.map((name) => (
              <span
                className="rounded-full bg-sky-100 px-3 py-1 font-mono text-xs font-semibold text-sky-800"
                key={`lookup:${name}`}
              >
                lookup · {name}
              </span>
            ))}
          </div>
        )}
        <p className="mt-2 text-xs text-slate-500">
          Logical names only. Connection details, storage and query execution
          remain server-managed.
        </p>
      </div>

      <div className="overflow-x-auto rounded-xl border border-slate-200">
        <table className="w-full min-w-4xl text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-3 font-semibold">Normalized target</th>
              <th className="px-4 py-3 font-semibold">Value</th>
              <th className="px-4 py-3 font-semibold">Input format</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {normalizedTable.columns.map((column) => {
              const assignment = output.fields.find(
                (field) =>
                  field.normalizedColumn.toLowerCase() ===
                  column.name.toLowerCase(),
              );
              const assignmentPath = pathByColumn.get(column.name.toLowerCase());
              const supportsInputFormat =
                expressionValueType(assignment?.value, sourceTable) === "text" &&
                (column.dataType === "Date" || column.dataType === "DateTime");
              const formats =
                column.dataType === "Date"
                  ? capabilities.transformation.inputFormats.date
                  : capabilities.transformation.inputFormats.dateTime;
              const missingDiagnostics = !assignment
                ? diagnosticForMissingTarget(diagnostics, fieldsPath, column.name)
                : [];

              return (
                <tr key={column.name}>
                  <th className="w-56 px-4 py-4 align-top">
                    <div className="font-mono text-xs font-semibold text-slate-900">
                      {column.name}
                    </div>
                    <div className="mt-1 text-xs font-normal text-slate-500">
                      {column.dataType}
                      {column.isRequired ? " · Required" : " · Optional"}
                    </div>
                    <Messages messages={missingDiagnostics} />
                  </th>
                  <td className="min-w-80 px-4 py-4 align-top">
                    <ValueExpressionEditor
                      capabilities={capabilities}
                      diagnostics={diagnostics}
                      onChange={(value) => changeAssignment(column.name, value)}
                      path={assignmentPath ? `${assignmentPath}.value` : null}
                      programNodeCount={stats.nodes}
                      sourceTable={sourceTable}
                      targetType={column.dataType}
                      value={assignment?.value ?? null}
                    />
                  </td>
                  <td className="w-64 px-4 py-4 align-top">
                    {supportsInputFormat && assignment ? (
                      <>
                        <select
                          aria-label={`Input format for ${column.name}`}
                          className={inputClassName}
                          onChange={(event) =>
                            changeInputFormat(column.name, event.target.value)
                          }
                          value={assignment.inputFormat ?? ""}
                        >
                          <option value="">Default format</option>
                          {formats.map((format) => (
                            <option key={format} value={format}>
                              {format}
                            </option>
                          ))}
                        </select>
                        {assignmentPath ? (
                          <Messages
                            messages={diagnosticsAtPath(
                              diagnostics,
                              `${assignmentPath}.inputFormat`,
                            )}
                          />
                        ) : null}
                      </>
                    ) : (
                      <span className="text-xs text-slate-400">
                        {assignment ? "Already typed" : "Not applicable"}
                      </span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
