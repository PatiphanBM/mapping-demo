"use client";

import type {
  ColumnDataType,
  ColumnDefinition,
  TableKind,
} from "@/lib/api/contracts";

const dataTypes: ColumnDataType[] = [
  "Text",
  "Date",
  "Decimal",
  "Boolean",
  "Integer",
  "DateTime",
  "Guid",
];

interface ColumnEditorProps {
  column: ColumnDefinition;
  index: number;
  kind: TableKind;
  canRemove?: boolean;
  canMoveUp?: boolean;
  canMoveDown?: boolean;
  compact?: boolean;
  onChange: (column: ColumnDefinition) => void;
  onRemove?: () => void;
  onMoveUp?: () => void;
  onMoveDown?: () => void;
}

export function ColumnEditor({
  column,
  index,
  kind,
  canRemove = false,
  canMoveUp = false,
  canMoveDown = false,
  compact = false,
  onChange,
  onRemove,
  onMoveUp,
  onMoveDown,
}: ColumnEditorProps) {
  const updateType = (dataType: ColumnDataType) =>
    onChange({
      ...column,
      dataType,
      length: dataType === "Text" ? column.length : null,
      precision: dataType === "Decimal" ? column.precision : null,
      scale: dataType === "Decimal" ? column.scale : null,
    });

  return (
    <fieldset className="rounded-xl border border-slate-200 bg-slate-50 p-4">
      <legend className="px-1 text-sm font-semibold text-slate-700">
        Column {index + 1}
      </legend>
      <div
        className={`grid gap-3 ${compact ? "md:grid-cols-2" : "md:grid-cols-3"}`}
      >
        <label className="text-sm font-medium text-slate-700">
          Name
          <input
            aria-label={`Column ${index + 1} name`}
            className="mt-1 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
            value={column.name}
            onChange={(event) =>
              onChange({ ...column, name: event.target.value })
            }
            placeholder="customer_id"
          />
        </label>
        <label className="text-sm font-medium text-slate-700">
          Data type
          <select
            aria-label={`Column ${index + 1} data type`}
            className="mt-1 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 disabled:bg-slate-100"
            value={column.dataType}
            disabled={kind === "Source"}
            onChange={(event) =>
              updateType(event.target.value as ColumnDataType)
            }
          >
            {dataTypes.map((dataType) => (
              <option key={dataType}>{dataType}</option>
            ))}
          </select>
        </label>
        <label className="flex items-end gap-2 pb-2 text-sm font-medium text-slate-700">
          <input
            type="checkbox"
            aria-label={`Column ${index + 1} required`}
            checked={column.isRequired}
            disabled={kind === "Source"}
            onChange={(event) =>
              onChange({ ...column, isRequired: event.target.checked })
            }
          />
          Required mapping value
        </label>
        {column.dataType === "Text" ? (
          <label className="text-sm font-medium text-slate-700">
            Maximum length (optional)
            <input
              type="number"
              min={1}
              aria-label={`Column ${index + 1} maximum length`}
              className="mt-1 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
              value={column.length ?? ""}
              onChange={(event) =>
                onChange({
                  ...column,
                  length: numberOrNull(event.target.value),
                })
              }
            />
          </label>
        ) : null}
        {column.dataType === "Decimal" ? (
          <>
            <label className="text-sm font-medium text-slate-700">
              Precision
              <input
                type="number"
                min={1}
                max={1000}
                aria-label={`Column ${index + 1} precision`}
                className="mt-1 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
                value={column.precision ?? ""}
                onChange={(event) =>
                  onChange({
                    ...column,
                    precision: numberOrNull(event.target.value),
                  })
                }
              />
            </label>
            <label className="text-sm font-medium text-slate-700">
              Scale
              <input
                type="number"
                min={0}
                aria-label={`Column ${index + 1} scale`}
                className="mt-1 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2"
                value={column.scale ?? ""}
                onChange={(event) =>
                  onChange({
                    ...column,
                    scale: numberOrNull(event.target.value),
                  })
                }
              />
            </label>
          </>
        ) : null}
      </div>
      {onRemove || onMoveUp || onMoveDown ? (
        <div className="mt-3 flex flex-wrap gap-2">
          <button
            type="button"
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm disabled:opacity-40"
            disabled={!canMoveUp}
            onClick={onMoveUp}
          >
            Move up
          </button>
          <button
            type="button"
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm disabled:opacity-40"
            disabled={!canMoveDown}
            onClick={onMoveDown}
          >
            Move down
          </button>
          <button
            type="button"
            className="rounded-lg border border-rose-300 px-3 py-1.5 text-sm text-rose-700 disabled:opacity-40"
            disabled={!canRemove}
            onClick={onRemove}
          >
            Remove column
          </button>
        </div>
      ) : null}
    </fieldset>
  );
}

function numberOrNull(value: string) {
  return value === "" ? null : Number(value);
}
