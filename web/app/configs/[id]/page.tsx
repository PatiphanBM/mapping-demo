import { Suspense } from "react";
import { LoadingPanel } from "@/components/page-state";
import { ConfigDetails } from "./config-details";

export default function ConfigDetailsPage() {
  return (
    <Suspense
      fallback={
        <main className="mx-auto w-full max-w-7xl px-6 py-10 lg:px-8">
          <LoadingPanel label="Loading configuration…" />
        </main>
      }
    >
      <ConfigDetails />
    </Suspense>
  );
}
