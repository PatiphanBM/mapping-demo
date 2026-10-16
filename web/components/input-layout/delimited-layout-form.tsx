import type {
  DelimitedInputLayout,
  MappingCapabilities,
} from "@/lib/api/contracts";

interface DelimitedLayoutFormProps {
  layout: DelimitedInputLayout;
  sourceColumns: string[];
  capabilities: MappingCapabilities;
  onChange: (layout: DelimitedInputLayout) => void;
}

const inputClassName =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-950 outline-none transition focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100";

export function DelimitedLayoutForm({
  layout,
  sourceColumns,
  capabilities,
  onChange,
}: DelimitedLayoutFormProps) {
  function addField() {
    const sourceColumn =
      sourceColumns.find(
        (column) =>
          !layout.fields.some((field) => field.sourceColumn === column),
      ) ?? sourceColumns[0] ?? "";
    onChange({
      ...layout,
      fields: [
        ...layout.fields,
        { inputHeader: sourceColumn, sourceColumn },
      ],
    });
  }

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <label className="text-sm font-semibold text-slate-700">
          Encoding
          <select
            className={`${inputClassName} mt-2`}
            onChange={(event) =>
              onChange({ ...layout, encoding: event.target.value })
            }
            value={layout.encoding}
          >
            {capabilities.input.encodings.map((encoding) => (
              <option key={encoding} value={encoding}>
                {encoding}
              </option>
            ))}
          </select>
        </label>
        <label className="text-sm font-semibold text-slate-700">
          Delimiter
          <input
            className={`${inputClassName} mt-2 font-mono`}
            onChange={(event) =>
              onChange({ ...layout, delimiter: event.target.value })
            }
            value={layout.delimiter}
          />
        </label>
        <label className="text-sm font-semibold text-slate-700">
          Preamble lines
          <input
            className={`${inputClassName} mt-2`}
            min={0}
            onChange={(event) =>
              onChange({
                ...layout,
                preambleLines: Math.max(0, Number(event.target.value) || 0),
              })
            }
            type="number"
            value={layout.preambleLines}
          />
        </label>
        <label className="text-sm font-semibold text-slate-700">
          Header policy
          <select
            className={`${inputClassName} mt-2`}
            onChange={(event) =>
              onChange({
                ...layout,
                headerPolicy: event.target.value as "None" | "SingleRow",
              })
            }
            value={layout.headerPolicy}
          >
            <option value="None">No header</option>
            <option value="SingleRow">Single-row header</option>
          </select>
        </label>
      </div>

      <div>
        <div className="flex items-center justify-between gap-4">
          <div>
            <h4 className="font-semibold text-slate-900">Field mappings</h4>
            <p className="mt-1 text-sm text-slate-500">
              Mapping order is used when the file has no header row.
            </p>
          </div>
          <button
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-50"
            onClick={addField}
            type="button"
          >
            Add field
          </button>
        </div>

        <div className="mt-3 overflow-hidden rounded-xl border border-slate-200">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3 font-semibold">Position</th>
                <th className="px-4 py-3 font-semibold">Input header</th>
                <th className="px-4 py-3 font-semibold">Source column</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {layout.fields.map((field, index) => (
                <tr key={`${field.sourceColumn}:${index}`}>
                  <td className="px-4 py-3 font-mono text-xs text-slate-500">
                    {index + 1}
                  </td>
                  <td className="px-4 py-3">
                    <input
                      className={inputClassName}
                      disabled={layout.headerPolicy === "None"}
                      onChange={(event) =>
                        onChange({
                          ...layout,
                          fields: layout.fields.map((item, itemIndex) =>
                            itemIndex === index
                              ? { ...item, inputHeader: event.target.value }
                              : item,
                          ),
                        })
                      }
                      value={field.inputHeader}
                    />
                  </td>
                  <td className="px-4 py-3">
                    <select
                      className={inputClassName}
                      onChange={(event) =>
                        onChange({
                          ...layout,
                          fields: layout.fields.map((item, itemIndex) =>
                            itemIndex === index
                              ? { ...item, sourceColumn: event.target.value }
                              : item,
                          ),
                        })
                      }
                      value={field.sourceColumn}
                    >
                      {sourceColumns.map((column) => (
                        <option key={column} value={column}>
                          {column}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      className="text-sm font-semibold text-rose-700 hover:text-rose-900"
                      onClick={() =>
                        onChange({
                          ...layout,
                          fields: layout.fields.filter(
                            (_, itemIndex) => itemIndex !== index,
                          ),
                        })
                      }
                      type="button"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
