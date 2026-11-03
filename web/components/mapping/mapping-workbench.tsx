"use client";

import { useMemo, useState } from "react";
import { ApiContractEditor } from "@/components/contracts/api-contract-editor";
import { InputLayoutEditor } from "@/components/input-layout/input-layout-editor";
import { ErrorPanel } from "@/components/page-state";
import type {
  ApiDestinationProfile,
  DestinationDefinition,
  FileDestinationProfileSummary,
  MappingCapabilities,
  MappingConfigVersion,
  MappingConfigVersionDetails,
  SupportedContract,
  TableDefinition,
} from "@/lib/api/contracts";

interface Props {
  apiProfiles: ApiDestinationProfile[];
  capabilities: MappingCapabilities;
  configDestination: DestinationDefinition | null;
  configId: number;
  normalizedTableId: number | null;
  onVersionCreated(version: MappingConfigVersion): void;
  sourceTable: TableDefinition;
  tables: TableDefinition[];
  version?: MappingConfigVersionDetails;
}

type DestinationKind = DestinationDefinition["kind"];

function contractKey(contract: Pick<SupportedContract, "id" | "version">) {
  return `${contract.id}@${contract.version}`;
}

function findContract<T extends { supportedContracts: SupportedContract[] }>(
  profile: T | undefined,
  key: string,
) {
  return profile?.supportedContracts.find((contract) => contractKey(contract) === key);
}

function destinationContract(contract: SupportedContract) {
  return { ...contract, schemaHash: "" };
}

export function MappingWorkbench({
  apiProfiles,
  capabilities,
  configDestination,
  configId,
  normalizedTableId,
  onVersionCreated,
  sourceTable,
  tables,
  version,
}: Props) {
  const initial = version?.destination ?? configDestination;
  const legacyTables = tables.filter((table) => table.id !== sourceTable.id);
  const [kind, setKind] = useState<DestinationKind>(initial?.kind ?? "legacyTable");
  const [legacyTableId, setLegacyTableId] = useState(
    initial?.kind === "legacyTable"
      ? initial.normalizedTableId
      : normalizedTableId ?? legacyTables[0]?.id ?? 0,
  );
  const [apiProfileId, setApiProfileId] = useState(
    initial?.kind === "apiContract" ? initial.apiDestinationProfileId : "",
  );
  const [apiContractKey, setApiContractKey] = useState(
    initial?.kind === "apiContract" ? contractKey(initial.contract) : "",
  );
  const [fileProfileId, setFileProfileId] = useState(
    initial?.kind === "jsonFile" ? initial.fileDestinationProfileId : "",
  );
  const [fileContractKey, setFileContractKey] = useState(
    initial?.kind === "jsonFile" ? contractKey(initial.contract) : "",
  );
  const [relativeFolder, setRelativeFolder] = useState(
    initial?.kind === "jsonFile" ? initial.relativeFolder : "exports",
  );
  const [fileNameTemplate, setFileNameTemplate] = useState(
    initial?.kind === "jsonFile"
      ? initial.fileNameTemplate
      : "{sourceBaseName}.{artifactRevision}.json",
  );
  const [formatting, setFormatting] = useState<"compact" | "indented">(
    initial?.kind === "jsonFile" ? initial.formatting : "indented",
  );

  const apiProfile = apiProfiles.find((profile) => profile.id === apiProfileId);
  const apiContract = findContract(apiProfile, apiContractKey);
  const fileProfiles = capabilities.destinations.jsonFile.profiles;
  const fileProfile = fileProfiles.find((profile) => profile.id === fileProfileId);
  const fileContract = findContract(fileProfile, fileContractKey);
  const normalizedTable = legacyTables.find((table) => table.id === legacyTableId);

  const destination = useMemo<DestinationDefinition | null>(() => {
    if (kind === "legacyTable") {
      return legacyTableId > 0 ? { kind, normalizedTableId: legacyTableId } : null;
    }
    if (kind === "apiContract") {
      return apiProfile && apiContract
        ? { kind, apiDestinationProfileId: apiProfile.id, contract: destinationContract(apiContract) }
        : null;
    }
    return fileProfile && fileContract && relativeFolder.trim() && fileNameTemplate.trim()
      ? {
          kind,
          fileDestinationProfileId: fileProfile.id,
          contract: destinationContract(fileContract),
          relativeFolder: relativeFolder.trim(),
          fileNameTemplate: fileNameTemplate.trim(),
          formatting,
        }
      : null;
  }, [apiContract, apiProfile, fileContract, fileNameTemplate, fileProfile, formatting, kind, legacyTableId, relativeFolder]);

  function selectKind(next: DestinationKind) {
    setKind(next);
    setApiProfileId("");
    setApiContractKey("");
    setFileProfileId("");
    setFileContractKey("");
    setRelativeFolder("exports");
    setFileNameTemplate("{sourceBaseName}.{artifactRevision}.json");
    setFormatting("indented");
    if (next === "legacyTable" && legacyTableId === 0) {
      setLegacyTableId(normalizedTableId ?? legacyTables[0]?.id ?? 0);
    }
  }

  return (
    <section className="mt-8" id="mapping-workbench">
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <p className="text-xs font-semibold uppercase tracking-wide text-indigo-600">Delivery</p>
        <h2 className="mt-1 text-xl font-semibold text-slate-950">Choose how mapped output leaves this configuration</h2>
        <fieldset className="mt-4 grid gap-3 lg:grid-cols-3">
          <legend className="sr-only">Destination type</legend>
          {([
            ["legacyTable", "Legacy Table", "Normalize rows into a database table"],
            ["jsonFile", "Whole file · JSON file", "Assemble every mapped object into one JSON array"],
            ["apiContract", "One record at a time · API Contract", "Deliver one contract payload per source record"],
          ] as const).map(([value, label, description]) => (
            <label className={`cursor-pointer rounded-xl border p-4 ${kind === value ? "border-indigo-500 bg-indigo-50" : "border-slate-200"}`} key={value}>
              <span className="flex items-start gap-3">
                <input checked={kind === value} name="destination-kind" onChange={() => selectKind(value)} type="radio" value={value} />
                <span><span className="block text-sm font-semibold text-slate-900">{label}</span><span className="mt-1 block text-xs leading-5 text-slate-600">{description}</span></span>
              </span>
            </label>
          ))}
        </fieldset>

        <div className="mt-5 border-t border-slate-100 pt-5">
          {kind === "legacyTable" ? (
            <label className="block max-w-xl text-sm font-semibold text-slate-800">Normalized table
              <select className="mt-2 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm" onChange={(event) => setLegacyTableId(Number(event.target.value))} value={legacyTableId}>
                <option value={0}>Choose a table</option>
                {legacyTables.map((table) => <option key={table.id} value={table.id}>{table.name}</option>)}
              </select>
            </label>
          ) : kind === "apiContract" ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="text-sm font-semibold text-slate-800">API destination profile
                <select className="mt-2 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm" onChange={(event) => { setApiProfileId(event.target.value); setApiContractKey(""); }} value={apiProfileId}>
                  <option value="">Choose a profile</option>
                  {apiProfiles.filter((profile) => profile.enabled).map((profile) => <option key={profile.id} value={profile.id}>{profile.displayName}</option>)}
                </select>
              </label>
              <label className="text-sm font-semibold text-slate-800">Contract version
                <select className="mt-2 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm" disabled={!apiProfile} onChange={(event) => setApiContractKey(event.target.value)} value={apiContractKey}>
                  <option value="">Choose a contract</option>
                  {apiProfile?.supportedContracts.map((contract) => <option key={contractKey(contract)} value={contractKey(contract)}>{contract.reference}</option>)}
                </select>
              </label>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <label className="text-sm font-semibold text-slate-800">File destination profile
                  <select className="mt-2 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm" onChange={(event) => { setFileProfileId(event.target.value); setFileContractKey(""); }} value={fileProfileId}>
                    <option value="">Choose a profile</option>
                    {fileProfiles.filter((profile) => profile.enabled).map((profile) => <option key={profile.id} value={profile.id}>{profile.id}</option>)}
                  </select>
                </label>
                <label className="text-sm font-semibold text-slate-800">Contract version
                  <select className="mt-2 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm" disabled={!fileProfile} onChange={(event) => setFileContractKey(event.target.value)} value={fileContractKey}>
                    <option value="">Choose a contract</option>
                    {fileProfile?.supportedContracts.map((contract) => <option key={contractKey(contract)} value={contractKey(contract)}>{contract.reference}</option>)}
                  </select>
                </label>
                <label className="text-sm font-semibold text-slate-800">Relative folder<input className="mt-2 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" onChange={(event) => setRelativeFolder(event.target.value)} value={relativeFolder} /></label>
                <label className="text-sm font-semibold text-slate-800">File name template<input className="mt-2 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" onChange={(event) => setFileNameTemplate(event.target.value)} value={fileNameTemplate} /></label>
                <label className="text-sm font-semibold text-slate-800">JSON formatting
                  <select className="mt-2 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm" onChange={(event) => setFormatting(event.target.value as "compact" | "indented")} value={formatting}>
                    {capabilities.destinations.jsonFile.formatting.map((value) => <option key={value} value={value}>{value}</option>)}
                  </select>
                </label>
              </div>
              <dl className="grid gap-3 rounded-xl bg-slate-50 p-4 text-sm sm:grid-cols-4">
                <Summary label="Payload root" value="Array" />
                <Summary label="Encoding" value="UTF-8 · no BOM" />
                <Summary label="Error policy" value="Whole file" />
                <Summary label="Visible root" value="Relative folder only" />
              </dl>
            </div>
          )}
        </div>
      </div>

      {destination?.kind === "legacyTable" && normalizedTable ? (
        <div id="input-layout-editor">
          <InputLayoutEditor capabilities={capabilities} configId={configId} destination={destination} initialLayout={version?.inputLayout} key={`${version?.id ?? "new"}:${JSON.stringify(destination)}`} normalizedTable={normalizedTable} onVersionCreated={onVersionCreated} sourceTable={sourceTable} transformationProgram={version?.transformationProgram} versionNo={version?.versionNo} />
        </div>
      ) : destination?.kind === "apiContract" && apiProfile ? (
        <ApiContractEditor capabilities={capabilities} configId={configId} destination={destination} key={`${version?.id ?? "new"}:${JSON.stringify(destination)}`} onVersionCreated={onVersionCreated} profile={apiProfile} sourceTable={sourceTable} version={version} />
      ) : destination?.kind === "jsonFile" && fileProfile ? (
        <ApiContractEditor capabilities={capabilities} configId={configId} destination={destination} key={`${version?.id ?? "new"}:${JSON.stringify(destination)}`} onVersionCreated={onVersionCreated} profile={fileProfile as FileDestinationProfileSummary} sourceTable={sourceTable} version={version} />
      ) : (
        <div className="mt-6"><ErrorPanel error={new Error("Choose all required delivery fields to open the mapping editor.")} title="Delivery draft is incomplete" /></div>
      )}
    </section>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return <div><dt className="text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</dt><dd className="mt-1 font-semibold text-slate-800">{value}</dd></div>;
}
