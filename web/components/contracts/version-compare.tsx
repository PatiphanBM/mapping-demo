import type { MappingConfigVersionDetails } from "@/lib/api/contracts";

interface Props {
  current: MappingConfigVersionDetails;
  previous: MappingConfigVersionDetails;
}

export function VersionCompare({ current, previous }: Props) {
  return (
    <section
      aria-label={`Compare version ${current.versionNo} with version ${previous.versionNo}`}
      className="mt-4 space-y-4 rounded-xl border border-indigo-100 bg-indigo-50/50 p-4"
    >
      <h4 className="font-semibold text-slate-900">
        Version {current.versionNo} compared with version {previous.versionNo}
      </h4>

      <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
        <table className="w-full min-w-2xl text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-3 py-2">Contract setting</th>
              <th className="px-3 py-2">Version {previous.versionNo}</th>
              <th className="px-3 py-2">Version {current.versionNo}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            <ComparisonRow
              current={destinationLabel(current)}
              label="Destination"
              previous={destinationLabel(previous)}
            />
            <ComparisonRow
              current={contractReference(current)}
              label="Contract reference"
              previous={contractReference(previous)}
            />
            <ComparisonRow
              current={current.contractSnapshot?.schemaHash ?? "—"}
              label="Schema hash"
              previous={previous.contractSnapshot?.schemaHash ?? "—"}
            />
          </tbody>
        </table>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <CollectionComparison
          current={targetPaths(current)}
          label="Mapped target paths"
          previous={targetPaths(previous)}
        />
        <CollectionComparison
          current={arrayGroups(current)}
          label="Array groups"
          previous={arrayGroups(previous)}
        />
        <CollectionComparison
          current={payloadBindings(current)}
          label="System bindings"
          previous={payloadBindings(previous)}
        />
      </div>
    </section>
  );
}

function ComparisonRow({
  current,
  label,
  previous,
}: {
  current: string;
  label: string;
  previous: string;
}) {
  const changed = current !== previous;
  return (
    <tr>
      <th className="px-3 py-2 text-xs font-semibold text-slate-600">{label}</th>
      <td className="px-3 py-2 font-mono text-xs text-slate-700">{previous}</td>
      <td
        className={`px-3 py-2 font-mono text-xs font-semibold ${
          changed ? "text-amber-700" : "text-slate-700"
        }`}
      >
        {current} {changed ? <span className="font-sans">(changed)</span> : null}
      </td>
    </tr>
  );
}

function CollectionComparison({
  current,
  label,
  previous,
}: {
  current: string[];
  label: string;
  previous: string[];
}) {
  const added = current.filter((item) => !previous.includes(item));
  const removed = previous.filter((item) => !current.includes(item));
  const unchanged = current.filter((item) => previous.includes(item));

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4">
      <h5 className="text-sm font-semibold text-slate-900">{label}</h5>
      <ChangeList items={added} label="Added" tone="added" />
      <ChangeList items={removed} label="Removed" tone="removed" />
      <ChangeList items={unchanged} label="Unchanged" tone="unchanged" />
      {current.length === 0 && previous.length === 0 ? (
        <p className="mt-2 text-xs text-slate-400">None in either version</p>
      ) : null}
    </div>
  );
}

function ChangeList({
  items,
  label,
  tone,
}: {
  items: string[];
  label: string;
  tone: "added" | "removed" | "unchanged";
}) {
  if (items.length === 0) return null;
  const toneClass = {
    added: "text-emerald-700",
    removed: "text-rose-700",
    unchanged: "text-slate-500",
  }[tone];

  return (
    <div className="mt-3">
      <p className={`text-xs font-semibold ${toneClass}`}>{label}</p>
      <ul className="mt-1 space-y-1">
        {items.map((item) => (
          <li className="break-all font-mono text-xs text-slate-700" key={item}>
            {item}
          </li>
        ))}
      </ul>
    </div>
  );
}

function destinationLabel(version: MappingConfigVersionDetails) {
  return version.destination.kind === "legacyTable"
    ? `Legacy Table #${version.destination.normalizedTableId}`
    : version.destination.kind === "apiContract"
      ? `API Contract · ${version.destination.apiDestinationProfileId}`
      : `JSON File · ${version.destination.fileDestinationProfileId}`;
}

function contractReference(version: MappingConfigVersionDetails) {
  return version.destination.kind === "legacyTable"
    ? "—"
    : version.destination.contract.reference;
}

function targetPaths(version: MappingConfigVersionDetails) {
  return uniqueSorted(
    version.transformationProgram.outputs.flatMap((output) => [
      ...output.fields.flatMap((field) =>
        field.targetPath ?? field.normalizedColumn
          ? [`${output.outputKey}: ${field.targetPath ?? field.normalizedColumn}`]
          : [],
      ),
      ...(output.arrayGroups ?? []).flatMap((group) => {
        const itemRoot = group.targetPath.endsWith("[]")
          ? group.targetPath
          : `${group.targetPath}[]`;
        return [
          `${output.outputKey}: ${itemRoot}.${group.sequencePath}`,
          ...group.sourceSlots.flatMap((slot) =>
            slot.fields.map(
              (field) => `${output.outputKey}: ${itemRoot}.${field.targetPath}`,
            ),
          ),
        ];
      }),
    ]),
  );
}

function arrayGroups(version: MappingConfigVersionDetails) {
  return uniqueSorted(
    version.transformationProgram.outputs.flatMap((output) =>
      (output.arrayGroups ?? []).map(
        (group) =>
          `${output.outputKey}: ${group.targetPath} · seq=${group.sequencePath} · slots=${group.sourceSlots.map((slot) => slot.sourceSlot).join(",") || "none"}`,
      ),
    ),
  );
}

function payloadBindings(version: MappingConfigVersionDetails) {
  return uniqueSorted(
    (version.contractSnapshot?.payloadBindings ?? []).map(
      (binding) => `${binding.targetPath} ← ${binding.source}`,
    ),
  );
}

function uniqueSorted(values: string[]) {
  return [...new Set(values)].sort((left, right) => left.localeCompare(right));
}
