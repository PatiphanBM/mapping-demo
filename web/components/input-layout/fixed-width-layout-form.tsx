import type {
  FixedWidthInputLayout,
  FixedWidthRecordRule,
  MappingCapabilities,
} from "@/lib/api/contracts";
import { fieldEnd, fixedWidthWarnings } from "@/lib/input-preview";

interface FixedWidthLayoutFormProps {
  layout: FixedWidthInputLayout;
  sourceColumns: string[];
  capabilities: MappingCapabilities;
  onChange: (layout: FixedWidthInputLayout) => void;
}

const inputClassName =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-950 outline-none transition focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100";

function positiveInteger(value: string, fallback = 1) {
  return Math.max(1, Number(value) || fallback);
}

export function FixedWidthLayoutForm({
  layout,
  sourceColumns,
  capabilities,
  onChange,
}: FixedWidthLayoutFormProps) {
  const warnings = fixedWidthWarnings(layout);

  function updateRule(index: number, rule: FixedWidthRecordRule) {
    const previousKey = layout.recordRules[index].key;
    onChange({
      ...layout,
      recordRules: layout.recordRules.map((item, itemIndex) =>
        itemIndex === index ? rule : item,
      ),
      recordSequence:
        previousKey === rule.key
          ? layout.recordSequence
          : layout.recordSequence.map((item) =>
              item.recordRule === previousKey
                ? { ...item, recordRule: rule.key }
                : item,
            ),
    });
  }

  function addRule() {
    let suffix = layout.recordRules.length + 1;
    let key = `record-${suffix}`;
    while (layout.recordRules.some((rule) => rule.key === key)) {
      suffix += 1;
      key = `record-${suffix}`;
    }

    const rule: FixedWidthRecordRule = {
      key,
      discriminator: { start: 1, length: 1, value: "X" },
      action: "Ignore",
      expectedLength: 1,
      fields: [],
    };
    onChange({
      ...layout,
      recordRules: [...layout.recordRules, rule],
      recordSequence: [
        ...layout.recordSequence,
        { recordRule: key, minOccurs: 0, maxOccurs: null },
      ],
    });
  }

  function removeRule(index: number) {
    const key = layout.recordRules[index].key;
    onChange({
      ...layout,
      recordRules: layout.recordRules.filter(
        (_, itemIndex) => itemIndex !== index,
      ),
      recordSequence: layout.recordSequence.filter(
        (item) => item.recordRule !== key,
      ),
    });
  }

  function moveSequence(index: number, offset: -1 | 1) {
    const target = index + offset;
    if (target < 0 || target >= layout.recordSequence.length) {
      return;
    }

    const sequence = [...layout.recordSequence];
    [sequence[index], sequence[target]] = [sequence[target], sequence[index]];
    onChange({ ...layout, recordSequence: sequence });
  }

  return (
    <div className="space-y-8">
      <div className="grid gap-4 sm:grid-cols-3">
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
          Position unit
          <select
            className={`${inputClassName} mt-2`}
            onChange={(event) =>
              onChange({ ...layout, positionUnit: event.target.value })
            }
            value={layout.positionUnit}
          >
            {capabilities.input.positionUnits.map((unit) => (
              <option key={unit} value={unit}>
                {unit}
              </option>
            ))}
          </select>
        </label>
        <label className="text-sm font-semibold text-slate-700">
          Unmatched records
          <input
            className={`${inputClassName} mt-2 bg-slate-50`}
            disabled
            value={layout.unmatchedRecordPolicy}
          />
        </label>
      </div>

      <div>
        <div className="flex items-center justify-between gap-4">
          <div>
            <h4 className="font-semibold text-slate-900">Record rules</h4>
            <p className="mt-1 text-sm text-slate-500">
              Positions are 1-based. End is calculated as start + length − 1.
            </p>
          </div>
          <button
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-50"
            onClick={addRule}
            type="button"
          >
            Add rule
          </button>
        </div>

        <div className="mt-4 space-y-5">
          {layout.recordRules.map((rule, ruleIndex) => (
            <article
              className="rounded-xl border border-slate-200 bg-slate-50/60 p-4"
              key={`${rule.key}:${ruleIndex}`}
            >
              <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-7">
                <label className="text-xs font-semibold text-slate-600">
                  Rule key
                  <input
                    className={`${inputClassName} mt-1`}
                    onChange={(event) =>
                      updateRule(ruleIndex, {
                        ...rule,
                        key: event.target.value,
                      })
                    }
                    value={rule.key}
                  />
                </label>
                <label className="text-xs font-semibold text-slate-600">
                  Action
                  <select
                    className={`${inputClassName} mt-1`}
                    onChange={(event) =>
                      updateRule(ruleIndex, {
                        ...rule,
                        action: event.target.value as "Import" | "Ignore",
                      })
                    }
                    value={rule.action}
                  >
                    <option value="Import">Import</option>
                    <option value="Ignore">Ignore</option>
                  </select>
                </label>
                <label className="text-xs font-semibold text-slate-600">
                  Expected length
                  <input
                    className={`${inputClassName} mt-1`}
                    min={1}
                    onChange={(event) =>
                      updateRule(ruleIndex, {
                        ...rule,
                        expectedLength: positiveInteger(event.target.value),
                      })
                    }
                    type="number"
                    value={rule.expectedLength}
                  />
                </label>
                <label className="text-xs font-semibold text-slate-600">
                  Match start
                  <input
                    className={`${inputClassName} mt-1`}
                    min={1}
                    onChange={(event) =>
                      updateRule(ruleIndex, {
                        ...rule,
                        discriminator: {
                          ...rule.discriminator,
                          start: positiveInteger(event.target.value),
                        },
                      })
                    }
                    type="number"
                    value={rule.discriminator.start}
                  />
                </label>
                <label className="text-xs font-semibold text-slate-600">
                  Match length
                  <input
                    className={`${inputClassName} mt-1`}
                    min={1}
                    onChange={(event) =>
                      updateRule(ruleIndex, {
                        ...rule,
                        discriminator: {
                          ...rule.discriminator,
                          length: positiveInteger(event.target.value),
                        },
                      })
                    }
                    type="number"
                    value={rule.discriminator.length}
                  />
                </label>
                <label className="text-xs font-semibold text-slate-600">
                  Match value
                  <input
                    className={`${inputClassName} mt-1 font-mono`}
                    onChange={(event) =>
                      updateRule(ruleIndex, {
                        ...rule,
                        discriminator: {
                          ...rule.discriminator,
                          value: event.target.value,
                        },
                      })
                    }
                    value={rule.discriminator.value}
                  />
                </label>
                <div className="flex items-end justify-end">
                  <button
                    className="px-2 py-2 text-sm font-semibold text-rose-700 hover:text-rose-900"
                    onClick={() => removeRule(ruleIndex)}
                    type="button"
                  >
                    Remove rule
                  </button>
                </div>
              </div>

              <div className="mt-4 flex items-center justify-between gap-4">
                <h5 className="text-sm font-semibold text-slate-800">
                  Source fields
                </h5>
                <button
                  className="text-sm font-semibold text-indigo-700 hover:text-indigo-900"
                  onClick={() => {
                    const sourceColumn =
                      sourceColumns.find(
                        (column) =>
                          !rule.fields.some(
                            (field) => field.sourceColumn === column,
                          ),
                      ) ?? sourceColumns[0] ?? "";
                    const start =
                      rule.fields.reduce(
                        (end, field) =>
                          Math.max(end, fieldEnd(field.start, field.length)),
                        0,
                      ) + 1;
                    updateRule(ruleIndex, {
                      ...rule,
                      fields: [
                        ...rule.fields,
                        { sourceColumn, start, length: 1 },
                      ],
                    });
                  }}
                  type="button"
                >
                  Add field
                </button>
              </div>

              {rule.fields.length > 0 ? (
                <div className="mt-2 overflow-x-auto rounded-lg border border-slate-200 bg-white">
                  <table className="w-full min-w-2xl text-left text-sm">
                    <thead className="bg-slate-100 text-xs uppercase tracking-wide text-slate-500">
                      <tr>
                        <th className="px-3 py-2 font-semibold">Source column</th>
                        <th className="px-3 py-2 font-semibold">Start</th>
                        <th className="px-3 py-2 font-semibold">Length</th>
                        <th className="px-3 py-2 font-semibold">End</th>
                        <th className="px-3 py-2" />
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {rule.fields.map((field, fieldIndex) => (
                        <tr key={`${field.sourceColumn}:${fieldIndex}`}>
                          <td className="px-3 py-2">
                            <select
                              className={inputClassName}
                              onChange={(event) =>
                                updateRule(ruleIndex, {
                                  ...rule,
                                  fields: rule.fields.map((item, itemIndex) =>
                                    itemIndex === fieldIndex
                                      ? {
                                          ...item,
                                          sourceColumn: event.target.value,
                                        }
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
                          <td className="px-3 py-2">
                            <input
                              className={inputClassName}
                              min={1}
                              onChange={(event) =>
                                updateRule(ruleIndex, {
                                  ...rule,
                                  fields: rule.fields.map((item, itemIndex) =>
                                    itemIndex === fieldIndex
                                      ? {
                                          ...item,
                                          start: positiveInteger(
                                            event.target.value,
                                          ),
                                        }
                                      : item,
                                  ),
                                })
                              }
                              type="number"
                              value={field.start}
                            />
                          </td>
                          <td className="px-3 py-2">
                            <input
                              className={inputClassName}
                              min={1}
                              onChange={(event) =>
                                updateRule(ruleIndex, {
                                  ...rule,
                                  fields: rule.fields.map((item, itemIndex) =>
                                    itemIndex === fieldIndex
                                      ? {
                                          ...item,
                                          length: positiveInteger(
                                            event.target.value,
                                          ),
                                        }
                                      : item,
                                  ),
                                })
                              }
                              type="number"
                              value={field.length}
                            />
                          </td>
                          <td className="px-3 py-2 font-mono text-xs text-slate-600">
                            {fieldEnd(field.start, field.length)}
                          </td>
                          <td className="px-3 py-2 text-right">
                            <button
                              className="text-sm font-semibold text-rose-700 hover:text-rose-900"
                              onClick={() =>
                                updateRule(ruleIndex, {
                                  ...rule,
                                  fields: rule.fields.filter(
                                    (_, itemIndex) =>
                                      itemIndex !== fieldIndex,
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
              ) : (
                <p className="mt-2 rounded-lg border border-dashed border-slate-300 bg-white px-4 py-3 text-sm text-slate-500">
                  Ignore rules need no source fields.
                </p>
              )}

              {(warnings[rule.key] ?? []).length > 0 ? (
                <ul className="mt-3 space-y-1 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                  {(warnings[rule.key] ?? []).map((warning) => (
                    <li key={warning}>Warning: {warning}</li>
                  ))}
                </ul>
              ) : null}
            </article>
          ))}
        </div>
      </div>

      <div>
        <div className="flex items-center justify-between gap-4">
          <div>
            <h4 className="font-semibold text-slate-900">Record sequence</h4>
            <p className="mt-1 text-sm text-slate-500">
              The API enforces this order and each min/max occurrence limit.
            </p>
          </div>
          <button
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
            disabled={layout.recordRules.length === 0}
            onClick={() =>
              onChange({
                ...layout,
                recordSequence: [
                  ...layout.recordSequence,
                  {
                    recordRule: layout.recordRules[0].key,
                    minOccurs: 0,
                    maxOccurs: null,
                  },
                ],
              })
            }
            type="button"
          >
            Add sequence item
          </button>
        </div>

        <div className="mt-3 space-y-2">
          {layout.recordSequence.map((item, index) => (
            <div
              className="grid gap-3 rounded-xl border border-slate-200 bg-white p-3 sm:grid-cols-[minmax(0,1fr)_9rem_9rem_auto]"
              key={`${item.recordRule}:${index}`}
            >
              <label className="text-xs font-semibold text-slate-600">
                Record rule
                <select
                  className={`${inputClassName} mt-1`}
                  onChange={(event) =>
                    onChange({
                      ...layout,
                      recordSequence: layout.recordSequence.map(
                        (sequenceItem, itemIndex) =>
                          itemIndex === index
                            ? {
                                ...sequenceItem,
                                recordRule: event.target.value,
                              }
                            : sequenceItem,
                      ),
                    })
                  }
                  value={item.recordRule}
                >
                  {layout.recordRules.map((rule) => (
                    <option key={rule.key} value={rule.key}>
                      {rule.key}
                    </option>
                  ))}
                </select>
              </label>
              <label className="text-xs font-semibold text-slate-600">
                Min occurs
                <input
                  className={`${inputClassName} mt-1`}
                  min={0}
                  onChange={(event) =>
                    onChange({
                      ...layout,
                      recordSequence: layout.recordSequence.map(
                        (sequenceItem, itemIndex) =>
                          itemIndex === index
                            ? {
                                ...sequenceItem,
                                minOccurs: Math.max(
                                  0,
                                  Number(event.target.value) || 0,
                                ),
                              }
                            : sequenceItem,
                      ),
                    })
                  }
                  type="number"
                  value={item.minOccurs}
                />
              </label>
              <label className="text-xs font-semibold text-slate-600">
                Max occurs
                <input
                  className={`${inputClassName} mt-1`}
                  min={0}
                  onChange={(event) =>
                    onChange({
                      ...layout,
                      recordSequence: layout.recordSequence.map(
                        (sequenceItem, itemIndex) =>
                          itemIndex === index
                            ? {
                                ...sequenceItem,
                                maxOccurs:
                                  event.target.value === ""
                                    ? null
                                    : Math.max(
                                        0,
                                        Number(event.target.value) || 0,
                                      ),
                              }
                            : sequenceItem,
                      ),
                    })
                  }
                  placeholder="Unlimited"
                  type="number"
                  value={item.maxOccurs ?? ""}
                />
              </label>
              <div className="flex items-end justify-end gap-1">
                <button
                  aria-label="Move sequence item up"
                  className="rounded-md border border-slate-300 px-2 py-2 text-sm disabled:opacity-30"
                  disabled={index === 0}
                  onClick={() => moveSequence(index, -1)}
                  type="button"
                >
                  ↑
                </button>
                <button
                  aria-label="Move sequence item down"
                  className="rounded-md border border-slate-300 px-2 py-2 text-sm disabled:opacity-30"
                  disabled={index === layout.recordSequence.length - 1}
                  onClick={() => moveSequence(index, 1)}
                  type="button"
                >
                  ↓
                </button>
                <button
                  className="px-2 py-2 text-sm font-semibold text-rose-700"
                  onClick={() =>
                    onChange({
                      ...layout,
                      recordSequence: layout.recordSequence.filter(
                        (_, itemIndex) => itemIndex !== index,
                      ),
                    })
                  }
                  type="button"
                >
                  Remove
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
