"use client";

// Three throwaway UI variants for the proposed File → JSON flow.
// Switch with ?variant=A, ?variant=B, or ?variant=C. No API or persistence.

import { useSearchParams } from "next/navigation";
import { useState } from "react";
import {
  PrototypeSwitcher,
  type PrototypeVariant,
} from "@/components/prototype-switcher";
import { initialDraft } from "./prototype-data";
import { StateSnapshot } from "./prototype-parts";
import { VariantA } from "./variant-a";
import { VariantB } from "./variant-b";
import { VariantC } from "./variant-c";

const variants: PrototypeVariant[] = [
  { key: "A", name: "Guided wizard" },
  { key: "B", name: "Pipeline canvas" },
  { key: "C", name: "Mapping workbench" },
];

export function FileToJsonPrototype() {
  const searchParams = useSearchParams();
  const requested = searchParams.get("variant")?.toUpperCase();
  const variant = variants.some((item) => item.key === requested)
    ? requested!
    : "A";
  const [draft, setDraft] = useState(initialDraft);

  return (
    <>
      {variant === "A" ? <VariantA draft={draft} setDraft={setDraft} /> : null}
      {variant === "B" ? <VariantB draft={draft} setDraft={setDraft} /> : null}
      {variant === "C" ? <VariantC draft={draft} setDraft={setDraft} /> : null}
      <StateSnapshot draft={draft} variant={variant} />
      <PrototypeSwitcher current={variant} variants={variants} />
    </>
  );
}
