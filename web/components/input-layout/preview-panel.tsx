import type {
  JsonValue,
  PreviewMappingConfigVersionResponse,
  TransformationRowStatus,
} from "@/lib/api/contracts";

interface PreviewPanelProps {
  preview: PreviewMappingConfigVersionResponse | null;
}

function visibleValue(value: string | null) {
  return value === null ? "null" : JSON.stringify(value);
}

function visibleJson(value: JsonValue) {
  return JSON.stringify(value);
}

const statusClassName: Record<TransformationRowStatus, string> = {
  Done: "bg-emerald-100 text-emerald-800",
  Filtered: "bg-amber-100 text-amber-800",
  Invalid: "bg-rose-100 text-rose-800",
};

export function PreviewPanel({ preview }: PreviewPanelProps) {
  if (!preview) {
    return (
      <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50 px-6 py-10 text-center">
        <h4 className="font-semibold text-slate-900">No preview yet</h4>
        <p className="mt-2 text-sm text-slate-600">
          Paste text or choose a UTF-8 file, then ask the API to parse it.
        </p>
      </div>
    );
  }

  const importCount = preview.records.filter(
    (record) => record.action === "Import",
  ).length;
  const ignoreCount = preview.records.length - importCount;
  const transformations = preview.records.flatMap((record) =>
    record.transformation ? [record.transformation] : [],
  );
  const statusCount = (status: TransformationRowStatus) =>
    transformations.filter((item) => item.status === status).length;
  const outputCount = transformations.reduce(
    (total, item) => total + item.outputs.length,
    0,
  );

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-slate-950 px-5 py-4 text-white">
        <div className="flex flex-wrap gap-5 text-sm">
          <span>
            <strong className="text-lg">{importCount}</strong> Import
          </span>
          <span>
            <strong className="text-lg">{ignoreCount}</strong> Ignore
          </span>
          <span>
            <strong className="text-lg">{statusCount("Done")}</strong> Done
          </span>
          <span>
            <strong className="text-lg">{statusCount("Filtered")}</strong>{" "}
            Filtered
          </span>
          <span>
            <strong className="text-lg">{statusCount("Invalid")}</strong>{" "}
            Invalid
          </span>
          <span>
            <strong className="text-lg">{outputCount}</strong> Outputs
          </span>
        </div>
        <p className="text-xs text-slate-300">
          Evaluation: {new Date(preview.evaluationAt).toLocaleString()}
        </p>
      </div>

      {preview.records.map((record) => (
        <article
          className="overflow-hidden rounded-xl border border-slate-200 bg-white"
          key={record.rowKey}
        >
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 bg-slate-50 px-4 py-3">
            <div className="flex flex-wrap items-center gap-2">
              <span
                className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
                  record.action === "Import"
                    ? "bg-emerald-100 text-emerald-800"
                    : "bg-slate-200 text-slate-700"
                }`}
              >
                {record.action}
              </span>
              {record.transformation ? (
                <span
                  className={`rounded-full px-2.5 py-1 text-xs font-semibold ${statusClassName[record.transformation.status]}`}
                >
                  {record.transformation.status}
                </span>
              ) : null}
              <strong className="text-sm text-slate-900">
                Physical line {record.physicalLineNumber ?? "—"}
              </strong>
              <span className="font-mono text-xs text-slate-500">
                {record.rowKey}
              </span>
              {record.recordRule ? (
                <span className="font-mono text-xs text-slate-500">
                  rule: {record.recordRule}
                </span>
              ) : null}
            </div>
            {record.transformation ? (
              <span className="text-xs font-semibold text-slate-500">
                {record.transformation.outputs.length} output(s)
              </span>
            ) : null}
          </div>

          <div className="space-y-4 p-4">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                Raw record
              </p>
              <pre className="mt-1 overflow-x-auto rounded-lg bg-slate-950 px-3 py-2 font-mono text-xs text-slate-100">
                {visibleValue(record.rawRecord)}
              </pre>
            </div>

            {record.action === "Ignore" ? (
              <p className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-sm font-medium text-slate-600">
                Ignore — no Source fields extracted
              </p>
            ) : (
              <div className="overflow-x-auto rounded-lg border border-slate-200">
                <table className="w-full min-w-xl text-left text-sm">
                  <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      <th className="px-3 py-2 font-semibold">Source field</th>
                      <th className="px-3 py-2 font-semibold">Raw slice</th>
                      <th className="px-3 py-2 font-semibold">
                        Trimmed display
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {Object.entries(record.sourceValues).map(([name, value]) => {
                      const trimmed = value?.trim() ?? null;
                      return (
                        <tr key={name}>
                          <th className="px-3 py-2 font-mono text-xs font-semibold text-slate-700">
                            {name}
                          </th>
                          <td className="px-3 py-2 font-mono text-xs text-slate-700">
                            {visibleValue(value)}
                          </td>
                          <td className="px-3 py-2 font-mono text-xs text-slate-700">
                            {trimmed === ""
                              ? "(blank)"
                              : visibleValue(trimmed)}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}

            {record.transformation?.status === "Filtered" ? (
              <p className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-900">
                Filtered — common source condition rejected this row.
              </p>
            ) : null}

            {(record.transformation?.outputs.length ?? 0) > 0 ? (
              <div className="space-y-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  Transformation outputs
                </p>
                {record.transformation?.outputs.map((output) => (
                  <div
                    className="overflow-hidden rounded-lg border border-indigo-100"
                    key={output.outputKey}
                  >
                    <div className="bg-indigo-50 px-3 py-2 font-mono text-xs font-semibold text-indigo-800">
                      {output.outputKey}
                    </div>
                    <table className="w-full text-left text-sm">
                      <tbody className="divide-y divide-slate-100">
                        {Object.entries(output.values).map(([name, value]) => (
                          <tr key={name}>
                            <th className="w-56 px-3 py-2 font-mono text-xs font-semibold text-slate-700">
                              {name}
                            </th>
                            <td className="px-3 py-2 font-mono text-xs text-slate-700">
                              {visibleJson(value)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ))}
              </div>
            ) : null}

            {(record.transformation?.errors.length ?? 0) > 0 ? (
              <div className="space-y-2 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900">
                <p className="font-semibold">Transformation errors</p>
                <ul className="space-y-2">
                  {record.transformation?.errors.map((error, index) => (
                    <li key={`${error.expressionPath}:${index}`}>
                      <div>{error.reason}</div>
                      <div className="mt-1 font-mono text-xs text-rose-700">
                        {error.outputKey ? `output=${error.outputKey} · ` : ""}
                        {error.field ? `field=${error.field} · ` : ""}
                        {error.expressionPath}
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        </article>
      ))}

      <details className="rounded-xl border border-slate-200 bg-slate-50 p-4">
        <summary className="cursor-pointer text-sm font-semibold text-slate-700">
          Read-only preview JSON
        </summary>
        <pre className="mt-3 max-h-96 overflow-auto rounded-lg bg-slate-950 p-4 font-mono text-xs text-slate-100">
          {JSON.stringify(preview, null, 2)}
        </pre>
      </details>
    </div>
  );
}
