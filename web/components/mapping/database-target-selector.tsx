"use client";

import { useEffect, useRef, useState } from "react";
import { ErrorPanel } from "@/components/page-state";
import type {
  DatabaseDestinationProfile,
  DatabaseTargetSchema,
  DatabaseTargetTableSummary,
} from "@/lib/api/contracts";
import {
  inspectDatabaseTargetTable,
  listDatabaseTargetTables,
} from "@/lib/api/mapping-configs";

export interface DatabaseTargetSelection {
  profileId: string;
  schema: string;
  table: string;
  schemaSnapshot: DatabaseTargetSchema | null;
  duplicateKeyColumns: string[];
}

export const EMPTY_DATABASE_TARGET_SELECTION: DatabaseTargetSelection = {
  profileId: "",
  schema: "",
  table: "",
  schemaSnapshot: null,
  duplicateKeyColumns: [],
};

interface DatabaseTargetSelectorProps {
  profiles: DatabaseDestinationProfile[];
  value: DatabaseTargetSelection;
  onChange: (value: DatabaseTargetSelection) => void;
}

const fieldClassName =
  "mt-2 block w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm text-slate-950 shadow-sm outline-none transition focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100 disabled:cursor-not-allowed disabled:bg-slate-100";
const keySeparator = "\u001f";

export function DatabaseTargetSelector({
  profiles,
  value,
  onChange,
}: DatabaseTargetSelectorProps) {
  const [tables, setTables] = useState<DatabaseTargetTableSummary[]>([]);
  const [loadError, setLoadError] = useState<unknown>(null);
  const tableRequestId = useRef(0);
  const schemaRequestId = useRef(0);

  useEffect(() => () => {
    tableRequestId.current += 1;
    schemaRequestId.current += 1;
  }, []);

  function selectProfile(profileId: string) {
    tableRequestId.current += 1;
    schemaRequestId.current += 1;
    setTables([]);
    setLoadError(null);
    onChange({
      ...EMPTY_DATABASE_TARGET_SELECTION,
      profileId,
    });
  }

  function selectSchema(schema: string) {
    schemaRequestId.current += 1;
    setTables([]);
    setLoadError(null);
    onChange({
      ...EMPTY_DATABASE_TARGET_SELECTION,
      profileId: value.profileId,
      schema,
    });

    if (schema) {
      void loadTables(value.profileId, schema);
    } else {
      tableRequestId.current += 1;
    }
  }

  function selectTable(table: string) {
    schemaRequestId.current += 1;
    setLoadError(null);
    const selection: DatabaseTargetSelection = {
      ...EMPTY_DATABASE_TARGET_SELECTION,
      profileId: value.profileId,
      schema: value.schema,
      table,
    };
    onChange(selection);

    if (table) {
      void inspectTable(selection);
    }
  }

  async function loadTables(profileId: string, schema: string) {
    const requestId = ++tableRequestId.current;
    setLoadError(null);

    try {
      const rows = await listDatabaseTargetTables(profileId, schema);
      if (requestId === tableRequestId.current) {
        setTables(rows);
      }
    } catch (error) {
      if (requestId === tableRequestId.current) {
        setTables([]);
        setLoadError(error);
      }
    }
  }

  async function inspectTable(selection: DatabaseTargetSelection) {
    const requestId = ++schemaRequestId.current;
    setLoadError(null);

    try {
      const schemaSnapshot = await inspectDatabaseTargetTable(
        selection.profileId,
        selection.schema,
        selection.table,
      );
      if (requestId === schemaRequestId.current) {
        onChange({ ...selection, schemaSnapshot });
      }
    } catch (error) {
      if (requestId === schemaRequestId.current) {
        setLoadError(error);
      }
    }
  }

  const selectedProfile = profiles.find((profile) => profile.id === value.profileId);
  const usableKeys = value.schemaSnapshot?.uniqueConstraints.filter((key) =>
    key.columns.every((name) => value.schemaSnapshot?.columns.some(
      (column) => column.name === name
        && column.isWritable
        && !column.isIdentity
        && !column.isGenerated,
    )),
  ) ?? [];

  return (
    <div className="grid gap-4 sm:col-span-2 sm:grid-cols-3">
      <label className="block text-sm font-semibold text-slate-800">
        Database profile
        <select
          className={fieldClassName}
          onChange={(event) => selectProfile(event.target.value)}
          value={value.profileId}
        >
          <option value="">Choose a profile</option>
          {profiles.map((profile) => (
            <option key={profile.id} value={profile.id}>
              {profile.id} · {profile.provider}{profile.writeEnabled ? "" : " · inspect only"}
            </option>
          ))}
        </select>
      </label>
      <label className="block text-sm font-semibold text-slate-800">
        Schema
        <select
          className={fieldClassName}
          disabled={!value.profileId}
          onChange={(event) => selectSchema(event.target.value)}
          value={value.schema}
        >
          <option value="">Choose a schema</option>
          {(selectedProfile?.allowedSchemas ?? []).map((schema) => (
            <option key={schema}>{schema}</option>
          ))}
        </select>
      </label>
      <label className="block text-sm font-semibold text-slate-800">
        Table
        <select
          aria-label="Target database table"
          className={fieldClassName}
          disabled={!value.schema}
          onChange={(event) => selectTable(event.target.value)}
          value={value.table}
        >
          <option value="">Choose a table</option>
          {tables.map((table) => (
            <option key={table.table}>{table.table}</option>
          ))}
        </select>
      </label>
      {value.schemaSnapshot ? (
        <label className="block text-sm font-semibold text-slate-800 sm:col-span-3">
          Duplicate reconciliation
          <select
            className={fieldClassName}
            onChange={(event) => onChange({
              ...value,
              duplicateKeyColumns: event.target.value
                ? event.target.value.split(keySeparator)
                : [],
            })}
            value={value.duplicateKeyColumns.join(keySeparator)}
          >
            <option value="">Manual UnknownOutcome</option>
            {usableKeys.map((key) => (
              <option key={key.name} value={key.columns.join(keySeparator)}>
                {key.name} · {key.columns.join(", ")}
              </option>
            ))}
          </select>
          <span className="mt-2 block break-all font-mono text-xs font-normal text-slate-500">
            {value.schemaSnapshot.schemaHash}
          </span>
        </label>
      ) : null}
      {loadError ? (
        <div className="sm:col-span-3">
          <ErrorPanel
            error={loadError}
            title="Could not create config"
          />
        </div>
      ) : null}
    </div>
  );
}
