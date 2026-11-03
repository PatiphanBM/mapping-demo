"use client";

import { useEffect, useId, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { ErrorPanel, LoadingPanel } from "@/components/page-state";
import {
  activateAdminVersion, createAdminDraft, createAdminProfile,
  disableAdminProfile, getAdminProfileCapabilities, listAdminProfiles,
  updateAdminDraft, validateAdminDraft,
} from "@/lib/api/api-destination-profiles";
import { ApiError } from "@/lib/api/client";
import { listContracts } from "@/lib/api/contract-registry";
import type {
  ApiDestinationProfileAdmin, ApiDestinationProfileAdminCapabilities,
  ApiDestinationProfileDraft,
  ApiDestinationProfileValidation, ApiDestinationProfileVersionAdmin,
  RuntimeValueSource,
  ContractVersionSummary,
} from "@/lib/api/contracts";

const emptyDraft: ApiDestinationProfileDraft = {
  environment: "Sandbox",
  schemaBaseUrl: "https://contracts.partner.example",
  schemaPathTemplate: "/schemas/{contractReference}",
  deliveryBaseUrl: "https://delivery.partner.example",
  deliveryPathTemplate: "/proposals/{contractVersion}",
  method: "POST",
  authScheme: "Mtls",
  authSecretReference: "partner-mtls-reference",
  networkPolicyReference: "partner-egress-default",
  timeoutSeconds: 10,
  idempotencyHeaderName: "Idempotency-Key",
  correlationHeaderName: "X-Correlation-Id",
  allowedHeaderNames: ["Idempotency-Key", "X-Correlation-Id"],
  supportedContracts: [{ id: "proposal", version: "1", reference: "proposal/1" }],
  payloadBindings: [],
};

const runtimeSources: RuntimeValueSource[] = [
  "ContractReference", "FileJobId", "FileName", "SourceRowId",
  "SourceRowNumber", "ConfigVersionReference", "OutputKey",
];

const noCapabilities: ApiDestinationProfileAdminCapabilities = {
  canReadEndpoints: false,
  canWriteDraft: false,
  canActivate: false,
  canActivateProduction: false,
  canDisable: false,
};

export default function ApiDestinationProfilesAdminPage() {
  const [profiles, setProfiles] = useState<ApiDestinationProfileAdmin[]>([]);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [stale, setStale] = useState(false);
  const [validation, setValidation] = useState<ApiDestinationProfileValidation | null>(null);
  const [operatorId, setOperatorId] = useState("");
  const [reason, setReason] = useState("");
  const [profileKey, setProfileKey] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [draft, setDraft] = useState<ApiDestinationProfileDraft>(emptyDraft);
  const [savedDraft, setSavedDraft] = useState(serialize(emptyDraft));
  const [query, setQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("All");
  const [environmentFilter, setEnvironmentFilter] = useState("All");
  const [compareVersions, setCompareVersions] = useState<number[]>([]);
  const [capabilities, setCapabilities] = useState(noCapabilities);
  const [publishedContracts, setPublishedContracts] = useState<ContractVersionSummary[]>([]);

  const selected = useMemo(
    () => profiles.find((profile) => profile.profileKey === selectedKey) ?? null,
    [profiles, selectedKey],
  );
  const selectedDraft = selected?.versions.find((version) => version.lifecycle === "Draft") ?? null;
  const dirty = serialize(draft) !== savedDraft;
  const filteredProfiles = useMemo(() => {
    const text = query.trim().toLowerCase();
    return profiles.filter((profile) => {
      const active = profile.activeVersionNo !== null;
      const environments = new Set(profile.versions.map((version) => version.environment));
      return (!text || `${profile.profileKey} ${profile.displayName}`.toLowerCase().includes(text)) &&
        (statusFilter === "All" || (statusFilter === "Active" ? active : !active)) &&
        (environmentFilter === "All" || environments.has(environmentFilter as ApiDestinationProfileDraft["environment"]));
    });
  }, [environmentFilter, profiles, query, statusFilter]);

  async function load(preferred?: string | null) {
    setLoading(true);
    try {
      const [rows, allowed, contracts] = await Promise.all([
        listAdminProfiles(),
        getAdminProfileCapabilities(),
        listContracts("Published"),
      ]);
      setProfiles(rows);
      setCapabilities(allowed);
      setPublishedContracts(contracts);
      const targetKey = preferred ?? selectedKey ?? rows[0]?.profileKey ?? null;
      setSelectedKey(targetKey);
      const nextDraft = rows.find((profile) =>
        profile.profileKey === targetKey)?.versions.find((version) =>
          version.lifecycle === "Draft");
      if (nextDraft) {
        const next = toDraft(nextDraft);
        setDraft(next);
        setSavedDraft(serialize(next));
        setStale(false);
      }
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

  function startNew() {
    if (dirty && !window.confirm("Discard unsaved profile changes?")) return;
    const firstContract = publishedContracts[0];
    const nextDraft = {
      ...emptyDraft,
      supportedContracts: firstContract
        ? [{
            id: firstContract.contractKey,
            version: firstContract.version,
            reference: firstContract.reference,
          }]
        : [],
    };
    setCreating(true);
    setSelectedKey(null);
    setProfileKey("");
    setDisplayName("");
    setDraft(nextDraft);
    setSavedDraft(serialize(nextDraft));
    setValidation(null);
    setStale(false);
  }

  function chooseProfile(key: string) {
    if (dirty && key !== selectedKey && !window.confirm("Discard unsaved profile changes?")) return;
    setCreating(false);
    setSelectedKey(key);
    setCompareVersions([]);
    const nextDraft = profiles.find((profile) =>
      profile.profileKey === key)?.versions.find((version) =>
        version.lifecycle === "Draft");
    if (nextDraft) {
      const next = toDraft(nextDraft);
      setDraft(next);
      setSavedDraft(serialize(next));
    }
    setValidation(null);
    setStale(false);
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

  async function create(event: FormEvent) {
    event.preventDefault();
    if (!auditReady()) return;
    await perform(async () => {
      const created = await createAdminProfile(
        profileKey.trim(), displayName.trim(), draft, operatorId.trim(), reason.trim(),
      );
      setNotice(`Draft ${created.profileKey}/1 created.`);
      setCreating(false);
      await load(created.profileKey);
    });
  }

  async function saveDraft() {
    if (!selected || !selectedDraft || !auditReady()) return;
    await perform(async () => {
      await updateAdminDraft(
        selected.profileKey, selectedDraft.versionNo,
        selectedDraft.concurrencyToken, draft, operatorId.trim(), reason.trim(),
      );
      setSavedDraft(serialize(draft));
      setNotice(`Draft ${selected.profileKey}/${selectedDraft.versionNo} saved.`);
      await load(selected.profileKey);
    });
  }

  async function run(
    action: "new" | "validate" | "activate" | "disable" | "rollback",
    versionNo?: number,
  ) {
    if (!selected || !auditReady()) return;
    if (["activate", "disable", "rollback"].includes(action) &&
      !window.confirm(`${action[0].toUpperCase()}${action.slice(1)} ${selected.profileKey}?`)) return;
    await perform(async () => {
      if (action === "new") await createAdminDraft(selected, operatorId.trim(), reason.trim());
      if (action === "validate" && versionNo) {
        const result = await validateAdminDraft(
          selected.profileKey, versionNo, operatorId.trim(), reason.trim(),
        );
        setValidation(result);
        setNotice(result.isValid
          ? `Draft validation passed. Secret status: ${result.secretStatus}.`
          : "Draft validation failed. Review diagnostics below.");
      }
      if (action === "activate" && versionNo) {
        await activateAdminVersion(selected, versionNo, operatorId.trim(), reason.trim());
        setNotice(`Version ${versionNo} activated.`);
      }
      if (action === "rollback" && versionNo) {
        await activateAdminVersion(selected, versionNo, operatorId.trim(), reason.trim(), true);
        setNotice(`Version ${versionNo} restored as active.`);
      }
      if (action === "disable") {
        await disableAdminProfile(selected, operatorId.trim(), reason.trim());
        setNotice("Profile disabled. Historical pinned jobs remain resolvable.");
      }
      await load(selected.profileKey);
    });
  }

  if (loading && profiles.length === 0 && !creating) return <LoadingPanel label="Loading API destination profiles" />;
  if (error && profiles.length === 0 && !creating) return <ErrorPanel error={error} onRetry={() => void load(null)} />;

  return (
    <main className="mx-auto max-w-7xl space-y-6 px-4 py-8 sm:px-6 lg:px-8">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-sm font-semibold text-indigo-600">Admin</p>
          <h1 className="text-3xl font-bold">API destination profiles</h1>
          <p className="mt-2 max-w-3xl text-sm text-slate-600">Versioned endpoints and logical secret references. Secret values and payloads are never shown.</p>
        </div>
        {capabilities.canWriteDraft ? <Button onClick={startNew} disabled={publishedContracts.length === 0}>New profile</Button> : null}
      </header>
      {error ? <ErrorPanel error={error} onRetry={() => void load(selectedKey)} /> : null}
      {stale ? (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-950">
          <span>This draft changed by another operator. Your unsaved edits are preserved.</span>
          <Button tone="secondary" onClick={() => void load(selectedKey)}>Refresh server version</Button>
        </div>
      ) : null}
      {notice ? <p role="status" className="rounded-xl bg-indigo-50 p-3 text-sm text-indigo-900">{notice}</p> : null}
      <section className="grid gap-4 rounded-2xl border border-slate-200 bg-white p-5 sm:grid-cols-2" aria-label="Audit context">
        <Field label="Operator ID" value={operatorId} onChange={setOperatorId} autoComplete="username" />
        <Field label="Reason" value={reason} onChange={setReason} />
      </section>

      <div className="grid gap-6 lg:grid-cols-[20rem_minmax(0,1fr)]">
        <aside className="h-fit rounded-2xl border border-slate-200 bg-white p-4">
          <h2 className="font-semibold">Profiles</h2>
          <div className="mt-3 space-y-3">
            <Field label="Search profiles" value={query} onChange={setQuery} />
            <div className="grid grid-cols-2 gap-2">
              <Select label="Status" value={statusFilter} onChange={setStatusFilter} options={["All", "Active", "Disabled"]} />
              <Select label="Environment filter" value={environmentFilter} onChange={setEnvironmentFilter} options={["All", "Stub", "Sandbox", "Production"]} />
            </div>
          </div>
          <div className="mt-4 space-y-2">
            {filteredProfiles.map((profile) => (
              <button
                className={`w-full rounded-lg border p-3 text-left text-sm ${profile.profileKey === selectedKey && !creating ? "border-indigo-300 bg-indigo-50 text-indigo-950" : "border-transparent hover:bg-slate-50"}`}
                key={profile.profileKey}
                onClick={() => chooseProfile(profile.profileKey)}
              >
                <span className="block font-semibold">{profile.displayName}</span>
                <span>{profile.profileKey} · {profile.activeVersionNo ? `Active v${profile.activeVersionNo}` : "Disabled"}</span>
              </button>
            ))}
            {filteredProfiles.length === 0 ? <p className="rounded-lg bg-slate-50 p-3 text-sm text-slate-500">No profiles match these filters.</p> : null}
          </div>
        </aside>

        <section className="min-w-0 space-y-5">
          {(creating || !selected) && capabilities.canWriteDraft ? (
            <form className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5" onSubmit={create}>
              <h2 className="text-xl font-semibold">Create profile</h2>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Profile key" value={profileKey} onChange={setProfileKey} required />
                <Field label="Display name" value={displayName} onChange={setDisplayName} required />
              </div>
              <ProfileFields draft={draft} setDraft={setDraft} publishedContracts={publishedContracts} />
              <Button type="submit" disabled={working}>Create draft</Button>
            </form>
          ) : selected ? (
            <>
              <section className="rounded-2xl border border-slate-200 bg-white p-5">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div><h2 className="text-xl font-semibold">{selected.displayName}</h2><p className="text-sm text-slate-500">{selected.profileKey}</p></div>
                  <div className="flex flex-wrap gap-2">
                    {capabilities.canWriteDraft ? <Button tone="secondary" onClick={() => void run("new")} disabled={Boolean(selectedDraft) || working}>New draft</Button> : null}
                    {capabilities.canDisable ? <Button tone="danger" onClick={() => void run("disable")} disabled={!selected.activeVersionNo || working}>Disable</Button> : null}
                  </div>
                </div>
              </section>
              {selectedDraft ? (
                <section className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h3 className="font-semibold">Draft v{selectedDraft.versionNo}</h3>
                    {dirty ? <span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-900">Unsaved changes</span> : null}
                  </div>
                  {capabilities.canWriteDraft ? <ProfileFields draft={draft} setDraft={setDraft} publishedContracts={publishedContracts} /> : <ReadOnlyDraft version={selectedDraft} />}
                  <EndpointPreview draft={draft} endpointsVisible={selectedDraft.deliveryBaseUrl !== null} />
                  {capabilities.canWriteDraft ? <div className="flex flex-wrap gap-2">
                    <Button onClick={() => void saveDraft()} disabled={working || !dirty}>Save draft</Button>
                    <Button tone="secondary" onClick={() => void run("validate", selectedDraft.versionNo)} disabled={working || dirty}>Validate</Button>
                    {capabilities.canActivate && (selectedDraft.environment !== "Production" || capabilities.canActivateProduction) ? <Button onClick={() => void run("activate", selectedDraft.versionNo)} disabled={working || dirty}>Activate</Button> : null}
                  </div> : null}
                  <ValidationPanel validation={validation} />
                </section>
              ) : null}
              <VersionHistory profile={selected} compared={compareVersions} setCompared={setCompareVersions} onRollback={(versionNo) => void run("rollback", versionNo)} canActivate={capabilities.canActivate} canActivateProduction={capabilities.canActivateProduction} working={working} />
            </>
          ) : <p className="rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-600">No profiles are available. You do not have draft-write permission.</p>}
        </section>
      </div>
    </main>
  );
}

function ProfileFields({ draft, setDraft, publishedContracts }: { draft: ApiDestinationProfileDraft; setDraft: (value: ApiDestinationProfileDraft) => void; publishedContracts: ContractVersionSummary[] }) {
  const set = <K extends keyof ApiDestinationProfileDraft>(key: K, value: ApiDestinationProfileDraft[K]) => setDraft({ ...draft, [key]: value });
  const updateContract = (index: number, patch: Partial<ApiDestinationProfileDraft["supportedContracts"][number]>) =>
    set("supportedContracts", draft.supportedContracts.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item));
  const updateBinding = (index: number, patch: Partial<ApiDestinationProfileDraft["payloadBindings"][number]>) =>
    set("payloadBindings", draft.payloadBindings.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item));

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Select label="Environment" value={draft.environment} onChange={(value) => set("environment", value as ApiDestinationProfileDraft["environment"])} options={["Stub", "Sandbox", "Production"]} />
        <Select label="HTTP method" value={draft.method} onChange={(value) => set("method", value as ApiDestinationProfileDraft["method"])} options={["POST", "PUT", "PATCH"]} />
        <Select label="Authentication scheme" value={draft.authScheme} onChange={(value) => set("authScheme", value as ApiDestinationProfileDraft["authScheme"])} options={["None", "Mtls", "OAuth2ClientCredentials"]} />
        <Field label="Schema base URL" value={draft.schemaBaseUrl} onChange={(value) => set("schemaBaseUrl", value)} required />
        <Field label="Schema path template" value={draft.schemaPathTemplate} onChange={(value) => set("schemaPathTemplate", value)} required />
        <Field label="Delivery base URL" value={draft.deliveryBaseUrl} onChange={(value) => set("deliveryBaseUrl", value)} required />
        <Field label="Delivery path template" value={draft.deliveryPathTemplate} onChange={(value) => set("deliveryPathTemplate", value)} required />
        <Field label="Logical secret reference" value={draft.authSecretReference ?? ""} onChange={(value) => set("authSecretReference", value || null)} />
        <Field label="Network policy reference" value={draft.networkPolicyReference} onChange={(value) => set("networkPolicyReference", value)} required />
        <NumberField label="Timeout seconds" value={draft.timeoutSeconds} onChange={(value) => set("timeoutSeconds", value)} />
        <Field label="Idempotency header" value={draft.idempotencyHeaderName} onChange={(value) => set("idempotencyHeaderName", value)} required />
        <Field label="Correlation header" value={draft.correlationHeaderName} onChange={(value) => set("correlationHeaderName", value)} required />
      </div>
      <TextArea label="Allowed headers (one per line)" value={draft.allowedHeaderNames.join("\n")} onChange={(value) => set("allowedHeaderNames", lines(value))} />
      <CollectionEditor title="Supported contracts" onAdd={() => { const contract = publishedContracts[0]; if (contract) set("supportedContracts", [...draft.supportedContracts, { id: contract.contractKey, version: contract.version, reference: contract.reference }]); }}>
        {draft.supportedContracts.map((contract, index) => (
          <div className="grid gap-3 rounded-xl border border-slate-200 p-3 sm:grid-cols-[1fr_auto]" key={`${index}-${contract.reference}`}>
            <Select label={`Published Contract ${index + 1}`} value={contract.reference} onChange={(reference) => { const selected = publishedContracts.find((item) => item.reference === reference); if (selected) updateContract(index, { id: selected.contractKey, version: selected.version, reference: selected.reference }); }} options={publishedContracts.map((item) => item.reference)} />
            <Button tone="ghost" onClick={() => set("supportedContracts", draft.supportedContracts.filter((_, itemIndex) => itemIndex !== index))}>Remove</Button>
          </div>
        ))}
      </CollectionEditor>
      <CollectionEditor title="Payload bindings" onAdd={() => set("payloadBindings", [...draft.payloadBindings, { targetPath: "", source: "FileJobId" }])}>
        {draft.payloadBindings.map((binding, index) => (
          <div className="grid gap-3 rounded-xl border border-slate-200 p-3 sm:grid-cols-[1fr_1fr_auto]" key={`${index}-${binding.targetPath}`}>
            <Field label={`Binding ${index + 1} target path`} value={binding.targetPath} onChange={(value) => updateBinding(index, { targetPath: value })} />
            <Select label={`Binding ${index + 1} source`} value={binding.source} onChange={(value) => updateBinding(index, { source: value as RuntimeValueSource })} options={runtimeSources} />
            <Button tone="ghost" onClick={() => set("payloadBindings", draft.payloadBindings.filter((_, itemIndex) => itemIndex !== index))}>Remove</Button>
          </div>
        ))}
      </CollectionEditor>
    </div>
  );
}

function EndpointPreview({ draft, endpointsVisible }: { draft: ApiDestinationProfileDraft; endpointsVisible: boolean }) {
  return (
    <section className="rounded-xl bg-slate-950 p-4 text-sm text-slate-100" aria-label="Safe endpoint preview">
      <h4 className="font-semibold">Safe endpoint preview</h4>
      <p className="mt-2 break-all font-mono">{endpointsVisible ? `${draft.method} ${draft.deliveryBaseUrl}${draft.deliveryPathTemplate}` : `${draft.method} [endpoint hidden]${draft.deliveryPathTemplate}`}</p>
      <p className="mt-1 text-slate-400">Authentication uses a logical reference only; no credential is loaded in this browser.</p>
    </section>
  );
}

function ValidationPanel({ validation }: { validation: ApiDestinationProfileValidation | null }) {
  if (!validation) return null;
  return (
    <section aria-label="Validation diagnostics" className={`rounded-xl border p-4 text-sm ${validation.isValid ? "border-emerald-300 bg-emerald-50" : "border-rose-300 bg-rose-50"}`}>
      <h4 className="font-semibold">{validation.isValid ? "Validation passed" : "Validation diagnostics"}</h4>
      <p className="mt-1">Secret reference: {validation.secretStatus} · {validation.contentHash}</p>
      {validation.diagnostics.length ? <ul className="mt-3 list-disc space-y-1 pl-5">{validation.diagnostics.map((item) => <li key={`${item.path}-${item.code}`}>{item.path}: {item.message} ({item.code})</li>)}</ul> : null}
    </section>
  );
}

function ReadOnlyDraft({ version }: { version: ApiDestinationProfileVersionAdmin }) {
  return (
    <dl className="grid gap-3 rounded-xl bg-slate-50 p-4 text-sm sm:grid-cols-2">
      <div><dt className="font-semibold">Environment</dt><dd>{version.environment}</dd></div>
      <div><dt className="font-semibold">Request</dt><dd>{version.method} {version.deliveryPathTemplate}</dd></div>
      <div><dt className="font-semibold">Authentication</dt><dd>{version.authScheme}</dd></div>
      <div><dt className="font-semibold">Timeout</dt><dd>{version.timeoutSeconds} seconds</dd></div>
      <div className="sm:col-span-2"><dt className="font-semibold">Allowed headers</dt><dd>{version.allowedHeaderNames.join(", ")}</dd></div>
    </dl>
  );
}

function VersionHistory({ profile, compared, setCompared, onRollback, canActivate, canActivateProduction, working }: { profile: ApiDestinationProfileAdmin; compared: number[]; setCompared: (values: number[]) => void; onRollback: (versionNo: number) => void; canActivate: boolean; canActivateProduction: boolean; working: boolean }) {
  const selected = profile.versions.filter((version) => compared.includes(version.versionNo));
  const diffs = selected.length === 2 ? versionDiff(selected[0], selected[1]) : [];
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-5">
      <h3 className="font-semibold">Version history</h3>
      <p className="mt-1 text-sm text-slate-500">Choose two versions to compare safe configuration fields.</p>
      <div className="mt-3 space-y-2">
        {profile.versions.map((version) => (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-200 p-3 text-sm" key={version.profileVersionId}>
            <label className="flex items-center gap-3"><input type="checkbox" aria-label={`Compare version ${version.versionNo}`} checked={compared.includes(version.versionNo)} onChange={() => setCompared(toggleCompared(compared, version.versionNo))} /><span>v{version.versionNo} · {version.lifecycle} · {version.environment} · {version.method} {version.deliveryPathTemplate}</span></label>
            {version.lifecycle === "Disabled" && canActivate && (version.environment !== "Production" || canActivateProduction) ? <Button tone="secondary" onClick={() => onRollback(version.versionNo)} disabled={working}>Rollback</Button> : null}
          </div>
        ))}
      </div>
      {selected.length === 2 ? <div className="mt-4 rounded-xl bg-slate-50 p-4 text-sm"><h4 className="font-semibold">v{selected[0].versionNo} → v{selected[1].versionNo}</h4>{diffs.length ? <ul className="mt-2 list-disc space-y-1 pl-5">{diffs.map((item) => <li key={item}>{item}</li>)}</ul> : <p className="mt-2">No safe fields changed.</p>}</div> : null}
    </section>
  );
}

function versionDiff(left: ApiDestinationProfileVersionAdmin, right: ApiDestinationProfileVersionAdmin) {
  const fields: Array<keyof ApiDestinationProfileVersionAdmin> = [
    "environment", "method", "schemaPathTemplate", "deliveryPathTemplate",
    "authScheme", "networkPolicyReference", "timeoutSeconds",
    "idempotencyHeaderName", "correlationHeaderName", "allowedHeaderNames",
    "supportedContracts", "payloadBindings",
  ];
  return fields.filter((field) => serialize(left[field]) !== serialize(right[field])).map((field) => `${field} changed`);
}

function toggleCompared(current: number[], value: number) {
  return current.includes(value) ? current.filter((item) => item !== value) : [...current.slice(-1), value];
}

function toDraft(version: ApiDestinationProfileVersionAdmin): ApiDestinationProfileDraft {
  return {
    environment: version.environment,
    schemaBaseUrl: version.schemaBaseUrl ?? "",
    schemaPathTemplate: version.schemaPathTemplate,
    deliveryBaseUrl: version.deliveryBaseUrl ?? "",
    deliveryPathTemplate: version.deliveryPathTemplate,
    method: version.method,
    authScheme: version.authScheme,
    authSecretReference: version.authSecretReference,
    networkPolicyReference: version.networkPolicyReference,
    timeoutSeconds: version.timeoutSeconds,
    idempotencyHeaderName: version.idempotencyHeaderName,
    correlationHeaderName: version.correlationHeaderName,
    allowedHeaderNames: version.allowedHeaderNames,
    supportedContracts: version.supportedContracts,
    payloadBindings: version.payloadBindings,
  };
}

function lines(value: string) { return value.split(/\r?\n/).map((item) => item.trim()).filter(Boolean); }
function serialize(value: unknown) { return JSON.stringify(value); }

function CollectionEditor({ title, onAdd, children }: { title: string; onAdd: () => void; children: ReactNode }) {
  return <section className="space-y-3"><div className="flex items-center justify-between gap-3"><h4 className="font-semibold">{title}</h4><Button tone="secondary" onClick={onAdd}>Add</Button></div>{children}</section>;
}

function Field({ label, value, onChange, ...props }: { label: string; value: string; onChange: (value: string) => void } & Omit<React.InputHTMLAttributes<HTMLInputElement>, "value" | "onChange">) {
  return <label className="text-sm font-medium text-slate-700">{label}<input {...props} className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2" value={value} onChange={(event) => onChange(event.target.value)} /></label>;
}

function NumberField({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return <label className="text-sm font-medium text-slate-700">{label}<input type="number" min={1} max={120} className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2" value={value} onChange={(event) => onChange(Number(event.target.value))} /></label>;
}

function Select({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: readonly string[] }) {
  const id = useId();
  return <div className="text-sm font-medium text-slate-700"><label htmlFor={id}>{label}</label><select id={id} className="mt-1 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2" value={value} onChange={(event) => onChange(event.target.value)}>{options.map((option) => <option key={option}>{option}</option>)}</select></div>;
}

function TextArea({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return <label className="block text-sm font-medium text-slate-700">{label}<textarea className="mt-1 min-h-24 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm" value={value} onChange={(event) => onChange(event.target.value)} /></label>;
}

function Button({ children, tone = "primary", className = "", type = "button", ...props }: React.ButtonHTMLAttributes<HTMLButtonElement> & { tone?: "primary" | "secondary" | "danger" | "ghost" }) {
  const tones = { primary: "bg-indigo-600 text-white hover:bg-indigo-700", secondary: "border border-slate-300 bg-white text-slate-800 hover:bg-slate-50", danger: "bg-rose-600 text-white hover:bg-rose-700", ghost: "text-slate-700 hover:bg-slate-100" };
  return <button {...props} type={type} className={`h-fit rounded-lg px-3 py-2 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50 ${tones[tone]} ${className}`}>{children}</button>;
}
