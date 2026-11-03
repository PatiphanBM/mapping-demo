"use client";

import { useEffect, useMemo, useState, type FormEvent } from "react";
import { ErrorPanel, LoadingPanel } from "@/components/page-state";
import {
  cloneContractVersion,
  createContract,
  getContract,
  getContractCapabilities,
  listContracts,
  publishContractVersion,
  retireContractVersion,
  updateContractDraft,
  validateContractDraft,
} from "@/lib/api/contract-registry";
import { ApiError } from "@/lib/api/client";
import type {
  ContractAdminCapabilities,
  ContractAggregate,
  ContractValidation,
  ContractVersionDetails,
  ContractVersionLifecycle,
  ContractVersionSummary,
} from "@/lib/api/contracts";

const starterSchema = `{
  "type": "object",
  "additionalProperties": false,
  "properties": {}
}`;

const noCapabilities: ContractAdminCapabilities = {
  canWriteDraft: false,
  canPublish: false,
};

export default function ContractsAdminPage() {
  const [rows, setRows] = useState<ContractVersionSummary[]>([]);
  const [selected, setSelected] = useState<ContractAggregate | null>(null);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [stale, setStale] = useState(false);
  const [capabilities, setCapabilities] = useState(noCapabilities);
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState<"All" | ContractVersionLifecycle>("All");
  const [operatorId, setOperatorId] = useState("");
  const [reason, setReason] = useState("");
  const [contractKey, setContractKey] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [version, setVersion] = useState("1");
  const [schema, setSchema] = useState(starterSchema);
  const [savedSchema, setSavedSchema] = useState(starterSchema);
  const [validation, setValidation] = useState<ContractValidation | null>(null);
  const [newVersion, setNewVersion] = useState("");
  const [acknowledgeBreaking, setAcknowledgeBreaking] = useState(false);
  const [compared, setCompared] = useState<number[]>([]);

  const draft = selected?.versions.find((item) => item.lifecycle === "Draft") ?? null;
  const dirty = schema !== savedSchema;
  const jsonError = jsonMessage(schema);
  const contracts = useMemo(() => {
    const text = query.trim().toLowerCase();
    const grouped = new Map<string, ContractVersionSummary[]>();
    for (const row of rows) {
      if (filter !== "All" && row.lifecycle !== filter) continue;
      if (text && !`${row.contractKey} ${row.displayName} ${row.reference}`.toLowerCase().includes(text)) continue;
      grouped.set(row.contractKey, [...(grouped.get(row.contractKey) ?? []), row]);
    }
    return [...grouped.entries()];
  }, [filter, query, rows]);

  async function load(preferred?: string | null) {
    setLoading(true);
    try {
      const [nextRows, nextCapabilities] = await Promise.all([
        listContracts(),
        getContractCapabilities(),
      ]);
      setRows(nextRows);
      setCapabilities(nextCapabilities);
      const key = preferred ?? selectedKey ?? nextRows[0]?.contractKey ?? null;
      setSelectedKey(key);
      if (key) {
        const aggregate = await getContract(key);
        setSelected(aggregate);
        const nextDraft = aggregate.versions.find((item) => item.lifecycle === "Draft");
        if (nextDraft) {
          setSchema(nextDraft.draftSchema);
          setSavedSchema(nextDraft.draftSchema);
        }
      } else {
        setSelected(null);
      }
      setStale(false);
      setError(null);
    } catch (loadError) {
      setError(loadError);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => { void load(null); }, 0);
    return () => window.clearTimeout(timer);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const guard = (event: BeforeUnloadEvent) => {
      if (dirty) event.preventDefault();
    };
    window.addEventListener("beforeunload", guard);
    return () => window.removeEventListener("beforeunload", guard);
  }, [dirty]);

  function auditReady() {
    if (operatorId.trim() && reason.trim().length >= 10) return true;
    setNotice("Operator ID and reason (at least 10 characters) are required.");
    return false;
  }

  async function perform(action: () => Promise<void>) {
    setWorking(true);
    setError(null);
    setStale(false);
    try {
      await action();
    } catch (actionError) {
      if (actionError instanceof ApiError && actionError.status === 412) setStale(true);
      else setError(actionError);
    } finally {
      setWorking(false);
    }
  }

  function startNew() {
    if (dirty && !window.confirm("Discard unsaved Contract changes?")) return;
    setCreating(true);
    setSelected(null);
    setSelectedKey(null);
    setContractKey("");
    setDisplayName("");
    setVersion("1");
    setSchema(starterSchema);
    setSavedSchema(starterSchema);
    setValidation(null);
  }

  async function choose(key: string) {
    if (dirty && key !== selectedKey && !window.confirm("Discard unsaved Contract changes?")) return;
    setCreating(false);
    setSelectedKey(key);
    setValidation(null);
    setCompared([]);
    await perform(async () => {
      const aggregate = await getContract(key);
      setSelected(aggregate);
      const nextDraft = aggregate.versions.find((item) => item.lifecycle === "Draft");
      if (nextDraft) {
        setSchema(nextDraft.draftSchema);
        setSavedSchema(nextDraft.draftSchema);
      }
    });
  }

  async function create(event: FormEvent) {
    event.preventDefault();
    if (!auditReady() || jsonError) return;
    await perform(async () => {
      await createContract(contractKey.trim(), displayName.trim(), version.trim(), schema, operatorId.trim(), reason.trim());
      setCreating(false);
      setNotice(`Draft ${contractKey.trim()}/${version.trim()} created.`);
      await load(contractKey.trim());
    });
  }

  async function save() {
    if (!draft || !auditReady() || jsonError) return;
    await perform(async () => {
      await updateContractDraft(draft.contractVersionId, draft.concurrencyToken, schema, operatorId.trim(), reason.trim());
      setSavedSchema(schema);
      setNotice(`Draft ${draft.reference} saved.`);
      await load(draft.contractKey);
    });
  }

  async function validate() {
    if (!draft || jsonError) return;
    await perform(async () => {
      const result = await validateContractDraft(draft.contractVersionId, operatorId.trim());
      setValidation(result);
      setNotice(result.isValid ? `Validation passed: ${result.compatibility}.` : "Validation failed. Review diagnostics.");
    });
  }

  async function publish() {
    if (!draft || !auditReady() || dirty || !window.confirm(`Publish immutable ${draft.reference}?`)) return;
    await perform(async () => {
      await publishContractVersion(draft, acknowledgeBreaking, operatorId.trim(), reason.trim());
      setNotice(`${draft.reference} published.`);
      setValidation(null);
      await load(draft.contractKey);
    });
  }

  async function clone(source: ContractVersionDetails) {
    if (!newVersion.trim() || !auditReady()) return;
    await perform(async () => {
      await cloneContractVersion(source.contractVersionId, newVersion.trim(), operatorId.trim(), reason.trim());
      setNotice(`${source.reference} cloned as ${source.contractKey}/${newVersion.trim()}.`);
      setNewVersion("");
      await load(source.contractKey);
    });
  }

  async function retire(source: ContractVersionDetails) {
    if (!auditReady() || !window.confirm(`Retire ${source.reference}? Historical exact resolution remains available.`)) return;
    await perform(async () => {
      await retireContractVersion(source, operatorId.trim(), reason.trim());
      setNotice(`${source.reference} retired.`);
      await load(source.contractKey);
    });
  }

  function upload(file: File | undefined) {
    if (!file) return;
    if (file.size > 1024 * 1024) {
      setNotice("Schema file must not exceed 1 MiB.");
      return;
    }
    void file.text().then(setSchema);
  }

  if (loading && rows.length === 0 && !creating) return <LoadingPanel label="Loading Contracts" />;
  if (error && rows.length === 0 && !creating) return <ErrorPanel error={error} onRetry={() => void load(null)} />;

  return (
    <main className="mx-auto max-w-7xl space-y-6 px-4 py-8 sm:px-6 lg:px-8">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div><p className="text-sm font-semibold text-indigo-600">Admin</p><h1 className="text-3xl font-bold">Contract Registry</h1><p className="mt-2 text-sm text-slate-600">Draft, validate and publish immutable JSON Schema versions. Examples must be synthetic.</p></div>
        {capabilities.canWriteDraft ? <Button onClick={startNew}>New Contract</Button> : null}
      </header>
      {error ? <ErrorPanel error={error} onRetry={() => void load(selectedKey)} /> : null}
      {stale ? <div role="alert" className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-950">This Draft changed on the server. Your edit is preserved. <Button tone="secondary" onClick={() => void load(selectedKey)}>Refresh server version</Button></div> : null}
      {notice ? <p role="status" className="rounded-xl bg-indigo-50 p-3 text-sm text-indigo-900">{notice}</p> : null}
      <section className="grid gap-4 rounded-2xl border bg-white p-5 sm:grid-cols-2" aria-label="Audit context">
        <Field label="Operator ID" value={operatorId} onChange={setOperatorId} />
        <Field label="Reason" value={reason} onChange={setReason} />
      </section>
      <div className="grid gap-6 lg:grid-cols-[20rem_minmax(0,1fr)]">
        <aside className="h-fit space-y-3 rounded-2xl border bg-white p-4">
          <h2 className="font-semibold">Contracts</h2>
          <Field label="Search Contracts" value={query} onChange={setQuery} />
          <label className="block text-sm font-medium">Lifecycle<select aria-label="Lifecycle filter" className="mt-1 w-full rounded-lg border px-3 py-2" value={filter} onChange={(event) => setFilter(event.target.value as typeof filter)}>{["All", "Draft", "Published", "Retired"].map((item) => <option key={item}>{item}</option>)}</select></label>
          <div className="space-y-2">{contracts.map(([key, versions]) => <button key={key} onClick={() => void choose(key)} className={`w-full rounded-lg border p-3 text-left text-sm ${key === selectedKey ? "border-indigo-300 bg-indigo-50" : "border-transparent hover:bg-slate-50"}`}><span className="block font-semibold">{versions[0].displayName}</span><span>{key} · {versions.map((item) => `${item.version} ${item.lifecycle}`).join(", ")}</span></button>)}</div>
        </aside>
        <section className="min-w-0 space-y-5">
          {creating ? <form onSubmit={create} className="space-y-4 rounded-2xl border bg-white p-5"><h2 className="text-xl font-semibold">Create Contract Draft</h2><div className="grid gap-4 sm:grid-cols-3"><Field label="Contract key" value={contractKey} onChange={setContractKey} required /><Field label="Display name" value={displayName} onChange={setDisplayName} required /><Field label="Version" value={version} onChange={setVersion} required /></div><SchemaEditor schema={schema} setSchema={setSchema} jsonError={jsonError} upload={upload} /><Button type="submit" disabled={working || Boolean(jsonError)}>Create Draft</Button></form> : null}
          {!creating && selected ? <>
            <section className="rounded-2xl border bg-white p-5"><h2 className="text-xl font-semibold">{selected.displayName}</h2><p className="text-sm text-slate-500">{selected.contractKey}</p></section>
            {draft ? <section className="space-y-4 rounded-2xl border bg-white p-5"><div className="flex items-center justify-between"><h3 className="font-semibold">Draft {draft.reference}</h3>{dirty ? <span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold">Unsaved changes</span> : null}</div><SchemaEditor schema={schema} setSchema={setSchema} jsonError={jsonError} upload={upload} readOnly={!capabilities.canWriteDraft} />{capabilities.canWriteDraft ? <div className="flex flex-wrap gap-2"><Button onClick={() => void save()} disabled={working || !dirty || Boolean(jsonError)}>Save Draft</Button><Button tone="secondary" onClick={() => void validate()} disabled={working || dirty || Boolean(jsonError)}>Validate</Button>{capabilities.canPublish ? <Button onClick={() => void publish()} disabled={working || dirty || !validation?.isValid}>Publish</Button> : null}</div> : null}{validation ? <ValidationPanel value={validation} acknowledge={acknowledgeBreaking} setAcknowledge={setAcknowledgeBreaking} /> : null}</section> : null}
            <VersionHistory versions={selected.versions} capabilities={capabilities} newVersion={newVersion} setNewVersion={setNewVersion} clone={clone} retire={retire} compared={compared} setCompared={setCompared} working={working} />
          </> : null}
        </section>
      </div>
    </main>
  );
}

function SchemaEditor({ schema, setSchema, jsonError, upload, readOnly = false }: { schema: string; setSchema: (value: string) => void; jsonError: string | null; upload: (file: File | undefined) => void; readOnly?: boolean }) {
  return <div className="space-y-2"><label className="block text-sm font-medium">JSON Schema<textarea aria-label="JSON Schema" className="mt-1 min-h-80 w-full rounded-lg border px-3 py-2 font-mono text-sm" value={schema} onChange={(event) => setSchema(event.target.value)} readOnly={readOnly} /></label>{!readOnly ? <label className="block text-sm font-medium">Upload JSON Schema<input className="mt-1 block w-full text-sm" type="file" accept="application/json,.json" onChange={(event) => upload(event.target.files?.[0])} /></label> : null}{jsonError ? <p role="alert" className="text-sm text-rose-700">Malformed JSON: {jsonError}</p> : null}</div>;
}

function ValidationPanel({ value, acknowledge, setAcknowledge }: { value: ContractValidation; acknowledge: boolean; setAcknowledge: (value: boolean) => void }) {
  return <section aria-label="Validation result" className="space-y-3 rounded-xl bg-slate-50 p-4"><h4 className="font-semibold">Validation: {value.isValid ? "Passed" : "Failed"} · {value.compatibility ?? "Unknown"}</h4>{value.compatibility === "Breaking" ? <label className="flex gap-2 text-sm"><input type="checkbox" checked={acknowledge} onChange={(event) => setAcknowledge(event.target.checked)} />I acknowledge this breaking change.</label> : null}{value.diagnostics.map((item) => <p key={`${item.path}-${item.code}`} className="text-sm text-rose-700">{item.path}: {item.code} — {item.message}</p>)}{value.paths.length ? <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr><th>Path</th><th>Type</th><th>Required</th><th>Nullable</th><th>Constraints</th></tr></thead><tbody>{value.paths.map((path) => <tr key={path.path} className="border-t"><td className="py-2 font-mono">{path.path}</td><td>{path.scalarType ?? path.kind}</td><td>{path.isRequired ? "Yes" : "No"}</td><td>{path.isNullable ? "Yes" : "No"}</td><td>{path.constraints ? JSON.stringify(path.constraints) : "—"}</td></tr>)}</tbody></table></div> : null}</section>;
}

function VersionHistory({ versions, capabilities, newVersion, setNewVersion, clone, retire, compared, setCompared, working }: { versions: ContractVersionDetails[]; capabilities: ContractAdminCapabilities; newVersion: string; setNewVersion: (value: string) => void; clone: (value: ContractVersionDetails) => Promise<void>; retire: (value: ContractVersionDetails) => Promise<void>; compared: number[]; setCompared: (value: number[]) => void; working: boolean }) {
  const selected = versions.filter((item) => compared.includes(item.contractVersionId));
  return <section className="space-y-4 rounded-2xl border bg-white p-5"><h3 className="font-semibold">Version history</h3>{selected.length === 2 ? <p role="status" className="rounded-lg bg-slate-50 p-3 text-sm">Schema diff: {selected[0].draftSchema === selected[1].draftSchema ? "identical" : `${selected[0].reference} differs from ${selected[1].reference}`}</p> : null}<div className="space-y-3">{versions.map((item) => <article key={item.contractVersionId} className="rounded-xl border p-4"><div className="flex flex-wrap items-start justify-between gap-3"><div><label className="flex items-center gap-2 font-semibold"><input aria-label={`Compare ${item.reference}`} type="checkbox" checked={compared.includes(item.contractVersionId)} onChange={() => setCompared(toggle(compared, item.contractVersionId))} />{item.reference}</label><p className="text-sm text-slate-600">{item.lifecycle} · {item.compatibility ?? "Not validated"}</p><p className="break-all font-mono text-xs text-slate-500">{item.schemaHash ?? "Draft has no published hash"}</p></div>{item.lifecycle !== "Draft" && capabilities.canWriteDraft ? <div className="flex flex-wrap gap-2"><Field label="New version" value={newVersion} onChange={setNewVersion} /><Button tone="secondary" onClick={() => void clone(item)} disabled={working || !newVersion.trim()}>Clone as new version</Button>{item.lifecycle === "Published" && capabilities.canPublish ? <Button tone="danger" onClick={() => void retire(item)} disabled={working}>Retire</Button> : null}</div> : null}</div></article>)}</div></section>;
}

function toggle(values: number[], id: number) { return values.includes(id) ? values.filter((item) => item !== id) : [...values.slice(-1), id]; }
function jsonMessage(value: string) { try { JSON.parse(value); return null; } catch (error) { return error instanceof Error ? error.message : "Invalid JSON"; } }
function Field({ label, value, onChange, ...props }: { label: string; value: string; onChange: (value: string) => void } & Omit<React.InputHTMLAttributes<HTMLInputElement>, "value" | "onChange">) { return <label className="block text-sm font-medium">{label}<input {...props} className="mt-1 block w-full rounded-lg border px-3 py-2" value={value} onChange={(event) => onChange(event.target.value)} /></label>; }
function Button({ children, tone = "primary", type = "button", ...props }: React.ButtonHTMLAttributes<HTMLButtonElement> & { tone?: "primary" | "secondary" | "danger" }) { const styles = { primary: "bg-indigo-600 text-white", secondary: "border bg-white", danger: "bg-rose-600 text-white" }; return <button {...props} type={type} className={`rounded-lg px-3 py-2 text-sm font-semibold disabled:opacity-50 ${styles[tone]}`}>{children}</button>; }
