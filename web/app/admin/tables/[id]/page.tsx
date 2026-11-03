import { Suspense } from "react";
import { notFound } from "next/navigation";
import TableDetailClient from "./table-detail-client";

export default function TableDetailPage({
  params,
}: PageProps<"/admin/tables/[id]">) {
  return (
    <Suspense fallback={<TableDetailFallback />}>
      {params.then(({ id }) => (
        <ValidatedTableDetail id={id} />
      ))}
    </Suspense>
  );
}

function ValidatedTableDetail({ id }: { id: string }) {
  const tableId = Number(id);
  if (!Number.isSafeInteger(tableId) || tableId <= 0) {
    notFound();
  }

  return <TableDetailClient tableId={tableId} />;
}

function TableDetailFallback() {
  return (
    <main className="mx-auto max-w-7xl px-6 py-8 lg:px-8">
      <p role="status">Loading table details…</p>
    </main>
  );
}
