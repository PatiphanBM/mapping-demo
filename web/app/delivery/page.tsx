"use client";

import { useEffect, useState, type FormEvent } from "react";
import { ErrorPanel, LoadingPanel } from "@/components/page-state";
import { ApiError } from "@/lib/api/client";
import type { DatabaseDeliveryOperations, DeliveryJob, DeliveryOperations, FileJob, JsonExportHistory } from "@/lib/api/contracts";
import {
  getDatabaseDeliveryOperations,
  getDeliveryOperations,
  listDeliveryJobs,
  listFileJobs,
  listJsonExports,
  retryDeliveryJob,
  retryJsonExport,
  resolveDatabaseDelivery,
  updateDeliveryControl,
} from "@/lib/api/delivery-jobs";

export default function DeliveryPage() {
  const [fileJobs, setFileJobs] = useState<FileJob[]>([]);
  const [selectedFileJobId, setSelectedFileJobId] = useState<number | null>(null);
  const [deliveries, setDeliveries] = useState<DeliveryJob[]>([]);
  const [exports, setExports] = useState<JsonExportHistory[]>([]);
  const [operations, setOperations] = useState<DeliveryOperations | null>(null);
  const [databaseOperations, setDatabaseOperations] = useState<DatabaseDeliveryOperations | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [actionId, setActionId] = useState<number | null>(null);
  const [exportActionId, setExportActionId] = useState<number | null>(null);
  const [databaseActionId, setDatabaseActionId] = useState<number | null>(null);
  const [controlBusy, setControlBusy] = useState(false);
  const [operatorId, setOperatorId] = useState("");
  const [reason, setReason] = useState("");

  async function load(preferredId?: number | null) {
    setLoading(true);
    try {
      const result = await fetchDashboard(preferredId ?? selectedFileJobId);
      setFileJobs(result.jobs);
      setOperations(result.snapshot);
      setDatabaseOperations(result.databaseSnapshot);
      setSelectedFileJobId(result.selectedId);
      setDeliveries(result.rows);
      setExports(result.exports);
      setError(null);
    } catch (loadError) {
      setError(loadError);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    async function initialLoad() {
      try {
        const result = await fetchDashboard(null);
        if (!cancelled) {
          setFileJobs(result.jobs);
          setOperations(result.snapshot);
          setDatabaseOperations(result.databaseSnapshot);
          setSelectedFileJobId(result.selectedId);
          setDeliveries(result.rows);
          setExports(result.exports);
          setError(null);
        }
      } catch (loadError) {
        if (!cancelled) setError(loadError);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void initialLoad();
    return () => { cancelled = true; };
  }, []);

  async function selectFileJob(value: string) {
    const id = Number(value);
    setSelectedFileJobId(id);
    setLoading(true);
    try {
      const [deliveryRows, exportRows] = await Promise.all([
        listDeliveryJobs(id),
        listJsonExports(id),
      ]);
      setDeliveries(deliveryRows);
      setExports(exportRows);
      setError(null);
    } catch (loadError) {
      setError(loadError);
    } finally {
      setLoading(false);
    }
  }

  async function retryExport(job: JsonExportHistory) {
    if (!operatorId.trim() || !reason.trim()) {
      setNotice("Operator ID and reason are required for JSON export retry.");
      return;
    }
    if (!window.confirm(`Retry JSON export revision ${job.artifactRevision} with the same staged bytes and path?`)) {
      return;
    }
    setExportActionId(job.id);
    setNotice(null);
    try {
      await retryJsonExport(job.id, operatorId.trim(), reason.trim());
      setNotice(`JSON export revision ${job.artifactRevision} was queued with its pinned artifact identity.`);
    } catch (actionError) {
      if (actionError instanceof ApiError && actionError.status === 409) {
        setNotice("The JSON export changed state. The list was refreshed.");
      } else {
        setError(actionError);
      }
    } finally {
      setExportActionId(null);
      await load(job.fileJobId);
    }
  }

  async function saveControl(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!operations || !operatorId.trim() || !reason.trim()) {
      setNotice("Operator ID and reason are required to change runtime controls.");
      return;
    }
    setControlBusy(true);
    setNotice(null);
    try {
      const form = new FormData(event.currentTarget);
      await updateDeliveryControl({
        deliveryPaused: form.get("deliveryPaused") === "on",
        apiContractActivationEnabled: form.get("apiContractActivationEnabled") === "on",
        jsonFileActivationEnabled: form.get("jsonFileActivationEnabled") === "on",
        jsonFileFinalizerPaused: form.get("jsonFileFinalizerPaused") === "on",
        databaseTableActivationEnabled: form.get("databaseTableActivationEnabled") === "on",
        databaseDeliveryPaused: form.get("databaseDeliveryPaused") === "on",
      }, operatorId.trim(), reason.trim());
      setNotice("Runtime delivery controls were updated and audited.");
      await load();
    } catch (actionError) {
      setError(actionError);
    } finally {
      setControlBusy(false);
    }
  }

  async function retry(job: DeliveryJob) {
    if (!window.confirm(`Retry delivery #${job.id} with the same payload and idempotency key?`)) {
      return;
    }
    setActionId(job.id);
    setNotice(null);
    try {
      await retryDeliveryJob(job.id);
      setNotice(`Delivery #${job.id} was queued with its persisted payload.`);
    } catch (actionError) {
      if (actionError instanceof ApiError && actionError.status === 409) {
        setNotice(`Delivery #${job.id} changed state. The list was refreshed.`);
      } else {
        setError(actionError);
      }
    } finally {
      setActionId(null);
      await load(job.fileJobId);
    }
  }

  async function resolveDatabase(
    job: DatabaseDeliveryOperations["jobs"][number],
    action: "retry" | "mark-delivered" | "mark-failed",
  ) {
    if (!operatorId.trim() || !reason.trim()) {
      setNotice("Operator ID and reason are required for database delivery resolution.");
      return;
    }
    const riskAccepted = action === "retry" && job.retryRequiresDuplicateRiskAcceptance
      ? window.confirm("The target has no usable unique key. Retrying can insert a duplicate. Accept this risk and continue?")
      : window.confirm(`Apply ${action} to database delivery #${job.id}?`);
    if (!riskAccepted) return;
    setDatabaseActionId(job.id);
    setNotice(null);
    try {
      await resolveDatabaseDelivery(
        job.id,
        action,
        operatorId.trim(),
        reason.trim(),
        job.retryRequiresDuplicateRiskAcceptance,
      );
      setNotice(`Database delivery #${job.id} was resolved and audited.`);
    } catch (actionError) {
      if (actionError instanceof ApiError && actionError.status === 409) {
        setNotice(`Database delivery #${job.id} changed state or requires additional confirmation.`);
      } else {
        setError(actionError);
      }
    } finally {
      setDatabaseActionId(null);
      await load();
    }
  }

  return (
    <main className="mx-auto w-full max-w-7xl px-6 py-10 lg:px-8">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm font-semibold text-indigo-600">Operations</p>
          <h1 className="mt-1 text-3xl font-semibold tracking-tight text-slate-950">Delivery &amp; JSON export</h1>
          <p className="mt-2 text-sm text-slate-600">Status, aggregate metrics and audited controls. Payload bodies, credentials and absolute Output Roots are never shown.</p>
        </div>
        <button className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50" onClick={() => void load()} type="button">
          Refresh
        </button>
      </div>

      {loading && fileJobs.length === 0 ? <div className="mt-8"><LoadingPanel label="Loading delivery operations…" /></div> : null}
      {error ? <div className="mt-8"><ErrorPanel error={error} onRetry={() => void load()} title="Could not load delivery operations" /></div> : null}

      {operations ? (
        <>
          <section className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4" aria-label="Delivery metrics">
            <Metric label="Success" value={percent(operations.successRate)} />
            <Metric label="Retryable / permanent" value={`${percent(operations.retryableFailureRate)} / ${percent(operations.permanentFailureRate)}`} />
            <Metric label="Latency p50 / p95 / p99" value={`${Math.round(operations.p50LatencyMilliseconds)} / ${Math.round(operations.p95LatencyMilliseconds)} / ${Math.round(operations.p99LatencyMilliseconds)} ms`} />
            <Metric label="p95 source → delivered" value={operations.p95SourceToDeliveredSeconds === null ? "—" : `${Math.round(operations.p95SourceToDeliveredSeconds)} s`} />
            <Metric label="Oldest queue item" value={`${Math.round(operations.oldestQueueAgeSeconds)} s`} />
            <Metric label="Retries / conflicts / schema" value={`${operations.retryCount} / ${operations.duplicateConflictCount} / ${operations.schemaValidationFailureCount}`} />
          </section>
          <section className={`mt-4 rounded-2xl border p-4 ${operations.control.deliveryPaused ? "border-amber-300 bg-amber-50" : "border-emerald-200 bg-emerald-50"}`} aria-label="Delivery control status">
            <p className="font-semibold text-slate-950">Delivery {operations.control.deliveryPaused ? "paused" : "running"} · new API activation {operations.control.apiContractActivationEnabled ? "enabled" : "disabled"}</p>
            <p className="mt-1 font-semibold text-slate-950">New JSON activation {operations.control.jsonFileActivationEnabled ? "enabled" : "disabled"} · finalizer {operations.control.jsonFileFinalizerPaused ? "paused" : "running"}</p>
            <p className="mt-1 font-semibold text-slate-950">New database-table activation {operations.control.databaseTableActivationEnabled ? "enabled" : "disabled"} · database delivery {operations.control.databaseDeliveryPaused ? "paused" : "running"}</p>
            <p className="mt-1 text-sm text-slate-700">Last changed by {operations.control.updatedBy}: {operations.control.reason}</p>
          </section>
          <form className="mt-4 rounded-2xl border border-slate-200 bg-white p-5" key={operations.control.updatedAt} onSubmit={(event) => void saveControl(event)}>
            <h2 className="font-semibold text-slate-950">Audited runtime controls</h2>
            <p className="mt-1 text-sm text-slate-600">Pausing the JSON finalizer leaves staged payload, revision, path and hash identity unchanged.</p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <ControlToggle defaultChecked={operations.control.deliveryPaused} label="Pause API delivery" name="deliveryPaused" />
              <ControlToggle defaultChecked={operations.control.apiContractActivationEnabled} label="Allow new API activation" name="apiContractActivationEnabled" />
              <ControlToggle defaultChecked={operations.control.jsonFileActivationEnabled} label="Allow new JSON activation" name="jsonFileActivationEnabled" />
              <ControlToggle defaultChecked={operations.control.jsonFileFinalizerPaused} label="Pause JSON finalizer" name="jsonFileFinalizerPaused" />
              <ControlToggle defaultChecked={operations.control.databaseTableActivationEnabled} label="Allow new database-table activation" name="databaseTableActivationEnabled" />
              <ControlToggle defaultChecked={operations.control.databaseDeliveryPaused} label="Pause database delivery" name="databaseDeliveryPaused" />
            </div>
            <button className="mt-4 rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" disabled={controlBusy} type="submit">{controlBusy ? "Applying…" : "Apply controls"}</button>
          </form>
          <section className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4" aria-label="JSON export metrics">
            <Metric label="JSON queue age" value={`${Math.round(Math.max(0, ...operations.jsonExports.groups.map((group) => group.oldestQueueAgeSeconds)))} s`} />
            <Metric label="JSON p95 finalize" value={`${Math.round(Math.max(0, ...operations.jsonExports.groups.map((group) => group.p95FinalizeLatencyMilliseconds)))} ms`} />
            <Metric label="JSON bytes / objects" value={`${operations.jsonExports.groups.reduce((sum, group) => sum + group.byteCount, 0).toLocaleString()} / ${operations.jsonExports.groups.reduce((sum, group) => sum + group.objectCount, 0).toLocaleString()}`} />
            <Metric label="Blocked / retries" value={`${operations.jsonExports.groups.reduce((sum, group) => sum + group.blocked, 0)} / ${operations.jsonExports.groups.reduce((sum, group) => sum + group.retryCount, 0)}`} />
            <Metric label="Hash / cleanup failures" value={`${operations.jsonExports.groups.reduce((sum, group) => sum + group.hashConflictCount, 0)} / ${operations.jsonExports.groups.reduce((sum, group) => sum + group.cleanupFailureCount, 0)}`} />
          </section>
          {operations.jsonExports.alerts.length > 0 ? (
            <section className="mt-4 rounded-2xl border border-rose-200 bg-rose-50 p-4" aria-label="JSON export alerts" role="alert">
              <h2 className="font-semibold text-rose-950">JSON export alerts</h2>
              <ul className="mt-2 space-y-1 text-sm text-rose-800">{operations.jsonExports.alerts.map((alert) => <li key={alert.code}><strong>{alert.severity}:</strong> {alert.message}</li>)}</ul>
            </section>
          ) : null}
          <section className="mt-4 overflow-hidden rounded-2xl border border-slate-200 bg-white" aria-label="JSON export status by config and profile">
            <div className="border-b border-slate-200 px-5 py-4"><h2 className="font-semibold text-slate-950">JSON export status by config and profile</h2></div>
            {operations.jsonExports.groups.length === 0 ? <p className="px-5 py-6 text-sm text-slate-500">No JSON export metrics yet.</p> : <div className="overflow-x-auto"><table className="w-full text-left text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr><th className="px-4 py-3">Config / version</th><th className="px-4 py-3">Profile</th><th className="px-4 py-3">Total</th><th className="px-4 py-3">Pending / finalizing</th><th className="px-4 py-3">Published</th><th className="px-4 py-3">Blocked / retryable</th><th className="px-4 py-3">Bytes / objects</th></tr></thead>
              <tbody className="divide-y divide-slate-100">{operations.jsonExports.groups.map((group) => <tr key={`${group.configVersionId}:${group.destinationProfileId}`}><td className="px-4 py-3">config #{group.configId}<p className="text-xs text-slate-500">version #{group.configVersionId}</p></td><td className="px-4 py-3 font-mono text-xs">{group.destinationProfileId}</td><td className="px-4 py-3">{group.total}</td><td className="px-4 py-3">{group.pending} / {group.finalizing}</td><td className="px-4 py-3">{group.published}</td><td className="px-4 py-3">{group.blocked} / {group.retryableFailed}</td><td className="px-4 py-3">{group.byteCount.toLocaleString()} / {group.objectCount.toLocaleString()}</td></tr>)}</tbody>
            </table></div>}
          </section>
          {operations.alerts.length > 0 ? (
            <section className="mt-4 rounded-2xl border border-rose-200 bg-rose-50 p-4" aria-label="Delivery alerts" role="alert">
              <h2 className="font-semibold text-rose-950">Threshold alerts</h2>
              <ul className="mt-2 space-y-1 text-sm text-rose-800">{operations.alerts.map((alert) => <li key={alert.code}><strong>{alert.severity}:</strong> {alert.message}</li>)}</ul>
            </section>
          ) : null}
          <section className="mt-4 overflow-hidden rounded-2xl border border-slate-200 bg-white" aria-label="Delivery status by config and contract">
            <div className="border-b border-slate-200 px-5 py-4">
              <h2 className="font-semibold text-slate-950">Status by Config Version and contract</h2>
            </div>
            {operations.groups.length === 0 ? <p className="px-5 py-6 text-sm text-slate-500">No delivery metrics yet.</p> : (
              <div className="overflow-x-auto"><table className="w-full text-left text-sm">
                <thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr><th className="px-4 py-3">Config version</th><th className="px-4 py-3">Contract</th><th className="px-4 py-3">Total</th><th className="px-4 py-3">Pending</th><th className="px-4 py-3">Delivering</th><th className="px-4 py-3">Delivered</th><th className="px-4 py-3">Retryable</th><th className="px-4 py-3">Permanent</th></tr></thead>
                <tbody className="divide-y divide-slate-100">{operations.groups.map((group) => <tr key={`${group.configVersionId}:${group.contractReference}`}><td className="px-4 py-3">#{group.configVersionId}</td><td className="px-4 py-3 font-mono text-xs">{group.contractReference}</td><td className="px-4 py-3">{group.total}</td><td className="px-4 py-3">{group.pending}</td><td className="px-4 py-3">{group.delivering}</td><td className="px-4 py-3">{group.delivered}</td><td className="px-4 py-3">{group.retryableFailed}</td><td className="px-4 py-3">{group.permanentFailed}</td></tr>)}</tbody>
              </table></div>
            )}
          </section>
          {operations.rollouts.length > 0 ? (
            <section className="mt-4 rounded-2xl border border-slate-200 bg-white p-5" aria-label="API delivery rollouts">
              <h2 className="font-semibold text-slate-950">Config rollout</h2>
              <ul className="mt-3 grid gap-2 sm:grid-cols-2">{operations.rollouts.map((rollout) => <li className="rounded-xl bg-slate-50 px-3 py-2 text-sm" key={rollout.configId}><span className="font-semibold">{rollout.configName}</span> · {rollout.stage} · {rollout.enabled ? "enabled" : "disabled"}</li>)}</ul>
            </section>
          ) : null}
        </>
      ) : null}

      {databaseOperations ? (
        <section className="mt-6 space-y-4" aria-label="Database delivery operations">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Metric label="Database delivered / existing" value={`${databaseOperations.metrics.delivered} / ${databaseOperations.metrics.alreadyDelivered}`} />
            <Metric label="Database pending / retryable" value={`${databaseOperations.metrics.pending} / ${databaseOperations.metrics.retryableFailed}`} />
            <Metric label="Schema blocked / conflict" value={`${databaseOperations.metrics.blockedSchemaDrift} / ${databaseOperations.metrics.deliveryConflict}`} />
            <Metric label="Unknown outcome" value={String(databaseOperations.metrics.unknownOutcome)} />
          </div>
          {databaseOperations.alerts.length > 0 ? <div className="rounded-2xl border border-rose-200 bg-rose-50 p-4" role="alert"><h2 className="font-semibold text-rose-950">Database delivery alerts</h2><ul className="mt-2 space-y-1 text-sm text-rose-800">{databaseOperations.alerts.map((alert) => <li key={alert.code}><strong>{alert.severity}:</strong> {alert.message}</li>)}</ul></div> : null}
          <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white">
            <div className="border-b border-slate-200 px-5 py-4"><h2 className="font-semibold text-slate-950">Target schema health</h2><p className="mt-1 text-xs text-slate-500">Latest bounded inspection only; no connection strings, SQL or row payloads are returned.</p></div>
            {databaseOperations.schemaChecks.length === 0 ? <p className="px-5 py-6 text-sm text-slate-500">No schema checks yet.</p> : <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr><th className="px-4 py-3">Target</th><th className="px-4 py-3">Profile</th><th className="px-4 py-3">Compatibility</th><th className="px-4 py-3">Checked</th></tr></thead><tbody className="divide-y divide-slate-100">{databaseOperations.schemaChecks.map((check) => <tr key={`${check.destinationProfileId}:${check.targetSchema}:${check.targetTable}:${check.configuredSchemaHash}`}><td className="px-4 py-3 font-mono text-xs">{check.targetSchema}.{check.targetTable}</td><td className="px-4 py-3 font-mono text-xs">{check.destinationProfileId}</td><td className="px-4 py-3"><DatabaseStatus value={check.compatibility} /></td><td className="px-4 py-3 text-xs">{formatDate(check.checkedAt)}</td></tr>)}</tbody></table></div>}
          </div>
          <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white">
            <div className="border-b border-slate-200 px-5 py-4"><h2 className="font-semibold text-slate-950">Database delivery jobs</h2></div>
            {databaseOperations.jobs.length === 0 ? <p className="px-5 py-6 text-sm text-slate-500">No database deliveries yet.</p> : <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead className="bg-slate-50 text-xs uppercase text-slate-500"><tr><th className="px-4 py-3">Job / target</th><th className="px-4 py-3">Status</th><th className="px-4 py-3">Attempts / key</th><th className="px-4 py-3">Safe diagnostics</th><th className="px-4 py-3 text-right">Resolution</th></tr></thead><tbody className="divide-y divide-slate-100">{databaseOperations.jobs.map((job) => <tr key={job.id}><td className="px-4 py-4">#{job.id}<p className="font-mono text-xs text-slate-500">{job.targetSchema}.{job.targetTable} · {job.provider}</p></td><td className="px-4 py-4"><DatabaseStatus value={job.status} /></td><td className="px-4 py-4">{job.attempts}<p className="text-xs text-slate-500">{job.hasExistingUniqueKey ? "existing unique key" : "no reconciliation key"}</p></td><td className="px-4 py-4"><p className="max-w-72 text-xs text-rose-700">{job.lastErrorRedacted ?? "No error"}</p><p className="mt-1 max-w-72 truncate font-mono text-xs text-slate-500">{job.mappedValuesHash}</p></td><td className="px-4 py-4 text-right"><div className="flex justify-end gap-2">{job.canRetry ? <button className="rounded-lg bg-amber-600 px-3 py-2 text-xs font-semibold text-white disabled:opacity-50" disabled={databaseActionId === job.id} onClick={() => void resolveDatabase(job, "retry")} type="button">Retry{job.retryRequiresDuplicateRiskAcceptance ? " (duplicate risk)" : ""}</button> : null}{job.canMarkDelivered ? <button className="rounded-lg bg-emerald-700 px-3 py-2 text-xs font-semibold text-white disabled:opacity-50" disabled={databaseActionId === job.id} onClick={() => void resolveDatabase(job, "mark-delivered")} type="button">Mark delivered</button> : null}{job.canMarkFailed ? <button className="rounded-lg bg-slate-700 px-3 py-2 text-xs font-semibold text-white disabled:opacity-50" disabled={databaseActionId === job.id} onClick={() => void resolveDatabase(job, "mark-failed")} type="button">Mark failed</button> : null}</div></td></tr>)}</tbody></table></div>}
          </div>
        </section>
      ) : null}

      <section className="mt-6 grid gap-4 rounded-2xl border border-slate-200 bg-white p-5 sm:grid-cols-2" aria-label="Operator audit context">
        <label className="text-sm font-semibold text-slate-800">Operator ID<input autoComplete="username" className="mt-2 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" onChange={(event) => setOperatorId(event.target.value)} value={operatorId} /></label>
        <label className="text-sm font-semibold text-slate-800">Action reason<input className="mt-2 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" maxLength={500} onChange={(event) => setReason(event.target.value)} value={reason} /></label>
        <p className="text-xs text-slate-500 sm:col-span-2">Required for runtime controls, JSON retry and database delivery resolution. Every action is server-authorized and written to audit history.</p>
      </section>

      <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <label className="block text-sm font-semibold text-slate-800" htmlFor="file-job">File job</label>
        <select className="mt-2 w-full rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm" id="file-job" onChange={(event) => void selectFileJob(event.target.value)} value={selectedFileJobId ?? ""}>
          {fileJobs.length === 0 ? <option value="">No file jobs</option> : fileJobs.map((job) => <option key={job.id} value={job.id}>#{job.id} · {job.fileName} · {job.endToEndStatus}</option>)}
        </select>
        <p className="mt-2 text-xs text-slate-500">Automatic retry follows the persisted next-at time. Manual retry is a separate action; reprocess and business resend are not offered here.</p>
      </section>

      {notice ? <p className="mt-4 rounded-xl border border-indigo-200 bg-indigo-50 px-4 py-3 text-sm text-indigo-900" role="status">{notice}</p> : null}

      <section className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm" aria-label="JSON export history">
        <div className="border-b border-slate-200 px-5 py-4"><h2 className="font-semibold text-slate-950">JSON export history</h2><p className="mt-1 text-xs text-slate-500">Relative display path and integrity metadata only. Download is intentionally unavailable in v1.</p></div>
        {exports.length === 0 ? <p className="px-6 py-12 text-center text-sm text-slate-500">No JSON exports for this file job.</p> : <div className="overflow-x-auto"><table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-4 py-3">Revision / status</th><th className="px-4 py-3">Relative path</th><th className="px-4 py-3">Objects / bytes</th><th className="px-4 py-3">SHA-256</th><th className="px-4 py-3">Updated / published</th><th className="px-4 py-3 text-right">Action</th></tr></thead>
          <tbody className="divide-y divide-slate-100">{exports.map((job) => <tr key={job.id}><td className="px-4 py-4"><Status value={job.status} /><p className="mt-1 text-xs text-slate-500">revision {job.artifactRevision} · attempt {job.attemptCount}</p></td><td className="px-4 py-4 font-mono text-xs">{job.relativeDisplayPath}</td><td className="px-4 py-4">{job.objectCount ?? "—"} / {job.byteCount?.toLocaleString() ?? "—"}</td><td className="px-4 py-4"><p className="max-w-64 truncate font-mono text-xs" title={job.sha256 ?? undefined}>{job.sha256 ?? "—"}</p>{job.lastErrorRedacted ? <p className="mt-1 max-w-64 text-xs text-rose-700">{job.lastErrorRedacted}</p> : null}</td><td className="px-4 py-4 text-xs">{formatDate(job.updatedAt)}<p className="mt-1 text-slate-500">{job.publishedAt ? formatDate(job.publishedAt) : "not published"}</p></td><td className="px-4 py-4 text-right">{job.canRetry ? <button className="rounded-lg bg-indigo-600 px-3 py-2 text-xs font-semibold text-white disabled:opacity-50" disabled={exportActionId === job.id} onClick={() => void retryExport(job)} type="button">{exportActionId === job.id ? "Queuing…" : "Retry export"}</button> : <span className="text-xs text-slate-400">Not allowed</span>}</td></tr>)}</tbody>
        </table></div>}
      </section>

      <section className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm" aria-label="Delivery jobs">
        {deliveries.length === 0 ? <p className="px-6 py-12 text-center text-sm text-slate-500">No API deliveries for this file job.</p> : (
          <div className="overflow-x-auto"><table className="w-full text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-4 py-3">Delivery</th><th className="px-4 py-3">Contract / output</th><th className="px-4 py-3">Status</th><th className="px-4 py-3">Attempt</th><th className="px-4 py-3">Response</th><th className="px-4 py-3">Hashes / error</th><th className="px-4 py-3 text-right">Action</th></tr></thead>
            <tbody className="divide-y divide-slate-100">{deliveries.map((job) => <tr key={job.id}>
              <td className="px-4 py-4 font-medium">#{job.id}<p className="mt-1 text-xs text-slate-500">row {job.sourceRowNumber} · {job.operationKind}</p></td>
              <td className="px-4 py-4">{job.contractReference}<p className="mt-1 font-mono text-xs text-slate-500">{job.outputKey} · config v#{job.configVersionId}</p></td>
              <td className="px-4 py-4"><Status value={job.status} /></td>
              <td className="px-4 py-4">{job.attempts}<p className="mt-1 text-xs text-slate-500">{job.nextAttemptAt ? `next ${formatDate(job.nextAttemptAt)}` : "no automatic retry"}</p></td>
              <td className="px-4 py-4">{job.httpStatus ?? "—"}<p className="mt-1 max-w-48 truncate font-mono text-xs text-slate-500" title={job.correlationId ?? undefined}>{job.correlationId ?? "no correlation"}</p></td>
              <td className="px-4 py-4"><p className="max-w-52 truncate font-mono text-xs" title={job.payloadHash}>{job.payloadHash}</p><p className="mt-1 max-w-52 text-xs text-rose-700">{job.lastErrorRedacted ?? "No error"}</p></td>
              <td className="px-4 py-4 text-right">{job.canRetry ? <button className="rounded-lg bg-indigo-600 px-3 py-2 text-xs font-semibold text-white disabled:opacity-50" disabled={actionId === job.id} onClick={() => void retry(job)} type="button">{actionId === job.id ? "Queuing…" : "Retry"}</button> : <span className="text-xs text-slate-400">Not allowed</span>}</td>
            </tr>)}</tbody>
          </table></div>
        )}
      </section>
    </main>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><p className="text-sm text-slate-500">{label}</p><p className="mt-2 text-2xl font-semibold">{value}</p></div>;
}

function Status({ value }: { value: DeliveryJob["status"] | JsonExportHistory["status"] }) {
  const tone = value === "Delivered" || value === "Published" ? "bg-emerald-100 text-emerald-800" : value === "PermanentFailed" || value === "Blocked" ? "bg-rose-100 text-rose-800" : value === "RetryableFailed" ? "bg-amber-100 text-amber-800" : "bg-slate-100 text-slate-700";
  return <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${tone}`}>{value}</span>;
}

function DatabaseStatus({ value }: { value: string }) {
  const healthy = value === "Delivered" || value === "AlreadyDelivered" || value === "Healthy";
  const critical = value === "UnknownOutcome" || value === "DeliveryConflict" || value === "BlockedSchemaDrift" || value === "BreakingChange" || value === "Unavailable";
  const tone = healthy ? "bg-emerald-100 text-emerald-800" : critical ? "bg-rose-100 text-rose-800" : "bg-amber-100 text-amber-800";
  return <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${tone}`}>{value}</span>;
}

function ControlToggle({ defaultChecked, label, name }: { defaultChecked: boolean; label: string; name: string }) {
  return <label className="flex items-center gap-3 rounded-xl border border-slate-200 px-4 py-3 text-sm font-semibold text-slate-800"><input defaultChecked={defaultChecked} name={name} type="checkbox" />{label}</label>;
}

function percent(value: number) { return `${(value * 100).toFixed(1)}%`; }
function formatDate(value: string) { return new Date(value).toLocaleString(); }

async function fetchDashboard(preferredId: number | null) {
  const [jobs, snapshot, databaseSnapshot] = await Promise.all([
    listFileJobs(),
    getDeliveryOperations(),
    getDatabaseDeliveryOperations(),
  ]);
  const selectedId = preferredId ?? jobs[0]?.id ?? null;
  const [rows, exports] = selectedId === null
    ? [[], []] as [DeliveryJob[], JsonExportHistory[]]
    : await Promise.all([listDeliveryJobs(selectedId), listJsonExports(selectedId)]);
  return { jobs, snapshot, databaseSnapshot, selectedId, rows, exports };
}
