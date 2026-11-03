"use client";

import { useEffect } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";

export interface PrototypeVariant {
  key: string;
  name: string;
}

interface PrototypeSwitcherProps {
  current: string;
  variants: PrototypeVariant[];
}

export function PrototypeSwitcher({
  current,
  variants,
}: PrototypeSwitcherProps) {
  const pathname = usePathname();
  const router = useRouter();
  const searchParams = useSearchParams();
  const currentIndex = Math.max(
    variants.findIndex((variant) => variant.key === current),
    0,
  );

  function select(index: number) {
    const next = variants[(index + variants.length) % variants.length];
    const params = new URLSearchParams(searchParams.toString());
    params.set("variant", next.key);
    router.replace(`${pathname}?${params.toString()}`, { scroll: false });
  }

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null;
      if (
        target?.matches("input, textarea, select, [contenteditable='true']")
      ) {
        return;
      }

      if (event.key === "ArrowLeft") {
        select(currentIndex - 1);
      } else if (event.key === "ArrowRight") {
        select(currentIndex + 1);
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  });

  if (process.env.NODE_ENV === "production") return null;

  const active = variants[currentIndex];

  return (
    <div className="fixed inset-x-0 bottom-5 z-50 mx-auto flex w-fit items-center gap-2 rounded-full border border-slate-700 bg-slate-950 p-1.5 text-white shadow-2xl shadow-slate-950/30">
      <button
        aria-label="Previous prototype variant"
        className="grid size-9 place-items-center rounded-full text-lg transition hover:bg-white/15"
        onClick={() => select(currentIndex - 1)}
        type="button"
      >
        ←
      </button>
      <div className="min-w-52 px-3 text-center">
        <p className="text-[10px] font-semibold uppercase tracking-[0.18em] text-amber-300">
          Throwaway prototype
        </p>
        <p className="text-sm font-semibold">
          {active.key} · {active.name}
        </p>
      </div>
      <button
        aria-label="Next prototype variant"
        className="grid size-9 place-items-center rounded-full text-lg transition hover:bg-white/15"
        onClick={() => select(currentIndex + 1)}
        type="button"
      >
        →
      </button>
    </div>
  );
}
