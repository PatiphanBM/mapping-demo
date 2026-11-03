import { Suspense } from "react";
import { FileToJsonPrototype } from "./file-to-json-prototype";

function PrototypeFallback() {
  return (
    <main className="mx-auto grid min-h-[calc(100vh-74px)] max-w-7xl place-items-center px-6">
      <p className="text-sm font-semibold text-slate-500">
        Loading File → JSON prototype…
      </p>
    </main>
  );
}

export default function FileToJsonPrototypePage() {
  return (
    <Suspense fallback={<PrototypeFallback />}>
      <FileToJsonPrototype />
    </Suspense>
  );
}
