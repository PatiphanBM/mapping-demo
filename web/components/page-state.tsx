import { ApiError } from "@/lib/api/client";

interface ErrorPanelProps {
  error: unknown;
  title?: string;
  onRetry?: () => void;
}

export function ErrorPanel({ error, title, onRetry }: ErrorPanelProps) {
  const apiError = error instanceof ApiError ? error : null;
  const heading = title ?? apiError?.title ?? "Something went wrong";
  const detail =
    apiError?.detail ??
    (error instanceof Error ? error.message : "The request could not be completed.");
  const validationEntries = Object.entries(apiError?.errors ?? {});

  return (
    <section
      className="rounded-2xl border border-rose-200 bg-rose-50 p-5"
      role="alert"
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 className="font-semibold text-rose-950">{heading}</h2>
          <p className="mt-1 text-sm leading-6 text-rose-800">{detail}</p>
        </div>
        {onRetry ? (
          <button
            className="rounded-lg border border-rose-300 bg-white px-3 py-2 text-sm font-semibold text-rose-800 transition hover:bg-rose-100"
            onClick={onRetry}
            type="button"
          >
            Retry
          </button>
        ) : null}
      </div>
      {validationEntries.length > 0 ? (
        <ul className="mt-4 space-y-2 border-t border-rose-200 pt-4 text-sm text-rose-900">
          {validationEntries.flatMap(([path, messages]) =>
            messages.map((message) => (
              <li key={`${path}:${message}`}>
                <code className="font-mono text-xs font-semibold">{path}</code>
                {": "}
                {message}
              </li>
            )),
          )}
        </ul>
      ) : null}
    </section>
  );
}

export function LoadingPanel({ label = "Loading…" }: { label?: string }) {
  return (
    <div className="grid min-h-60 place-items-center rounded-2xl border border-slate-200 bg-white">
      <div className="flex items-center gap-3 text-sm font-medium text-slate-600">
        <span className="size-4 animate-spin rounded-full border-2 border-indigo-200 border-t-indigo-600" />
        {label}
      </div>
    </div>
  );
}

export function FieldMessages({ messages }: { messages: string[] }) {
  if (messages.length === 0) {
    return null;
  }

  return (
    <div className="mt-2 space-y-1 text-sm text-rose-700">
      {messages.map((message) => (
        <p key={message}>{message}</p>
      ))}
    </div>
  );
}
