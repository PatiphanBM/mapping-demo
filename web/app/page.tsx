import Link from "next/link";

export default function Home() {
  return (
    <main className="mx-auto grid min-h-[calc(100vh-74px)] max-w-7xl place-items-center px-6 py-20 lg:px-8">
      <section className="max-w-3xl text-center">
        <p className="mb-4 text-sm font-semibold uppercase tracking-[0.2em] text-indigo-600">
          Mapping Demo
        </p>
        <h1 className="text-4xl font-semibold tracking-tight text-slate-950 sm:text-6xl">
          Build versioned mapping configurations with confidence.
        </h1>
        <p className="mx-auto mt-6 max-w-2xl text-lg leading-8 text-slate-600">
          Manage input layouts, transformation outputs, and activation state
          from one focused workspace.
        </p>
        <div className="mt-10 flex items-center justify-center gap-3">
          <Link
            className="rounded-xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-indigo-700"
            href="/configs"
          >
            View configurations
          </Link>
          <Link
            className="rounded-xl border border-slate-300 bg-white px-5 py-3 text-sm font-semibold text-slate-700 transition hover:border-slate-400 hover:bg-slate-50"
            href="/configs/new"
          >
            Create a config
          </Link>
        </div>
      </section>
    </main>
  );
}
