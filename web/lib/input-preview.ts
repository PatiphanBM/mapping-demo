import type {
  FixedWidthInputLayout,
  FixedWidthRecordRule,
  InputLayout,
} from "./api/contracts";

export const MAX_SAMPLE_BYTES = 1024 * 1024;
export const MAX_SAMPLE_RECORDS = 50;

export class SampleInputError extends Error {
  readonly code: "InvalidEncoding" | "SampleTooLarge" | "TooManySampleRecords";

  constructor(
    code: SampleInputError["code"],
    message: string,
  ) {
    super(message);
    this.name = "SampleInputError";
    this.code = code;
  }
}

export async function readUtf8SampleFile(
  file: File,
  layout: InputLayout,
): Promise<string> {
  if (file.size > MAX_SAMPLE_BYTES) {
    throw sampleTooLarge();
  }

  let content: string;
  try {
    content = new TextDecoder("utf-8", { fatal: true }).decode(
      await file.arrayBuffer(),
    );
  } catch {
    throw new SampleInputError(
      "InvalidEncoding",
      "InvalidEncoding: The selected file is not valid UTF-8.",
    );
  }

  validateSampleText(content, layout);
  return content;
}

export function validateSampleText(
  content: string,
  layout: InputLayout,
): void {
  if (!content.isWellFormed()) {
    throw new SampleInputError(
      "InvalidEncoding",
      "InvalidEncoding: Sample text is not valid Unicode text.",
    );
  }

  if (new TextEncoder().encode(content).byteLength > MAX_SAMPLE_BYTES) {
    throw sampleTooLarge();
  }

  if (countPhysicalLines(content) > physicalLineLimit(layout)) {
    throw new SampleInputError(
      "TooManySampleRecords",
      `TooManySampleRecords: A sample may contain at most ${MAX_SAMPLE_RECORDS} parsed records.`,
    );
  }
}

export function fixedWidthWarnings(
  layout: FixedWidthInputLayout,
): Record<string, string[]> {
  return Object.fromEntries(
    layout.recordRules.map((rule) => [rule.key, warningsForRule(rule)]),
  );
}

export function fieldEnd(start: number, length: number): number {
  return start + length - 1;
}

function sampleTooLarge(): SampleInputError {
  return new SampleInputError(
    "SampleTooLarge",
    `SampleTooLarge: Sample content must not exceed ${MAX_SAMPLE_BYTES} UTF-8 bytes.`,
  );
}

function countPhysicalLines(content: string): number {
  if (content.length === 0) {
    return 0;
  }

  const newlineCount = content.match(/\n/g)?.length ?? 0;
  return newlineCount + (content.endsWith("\n") ? 0 : 1);
}

function physicalLineLimit(layout: InputLayout): number {
  if (layout.kind === "fixedWidth") {
    return MAX_SAMPLE_RECORDS;
  }

  return (
    MAX_SAMPLE_RECORDS +
    layout.preambleLines +
    (layout.headerPolicy === "SingleRow" ? 1 : 0)
  );
}

function warningsForRule(rule: FixedWidthRecordRule): string[] {
  const fields = rule.fields
    .filter((field) => field.start > 0 && field.length > 0)
    .map((field) => ({
      ...field,
      end: fieldEnd(field.start, field.length),
    }))
    .sort((left, right) => left.start - right.start);
  const warnings: string[] = [];
  let previousEnd = 0;

  for (const field of fields) {
    if (field.start <= previousEnd) {
      warnings.push(
        `${field.sourceColumn} overlaps a previous field at positions ${field.start}–${Math.min(previousEnd, field.end)}.`,
      );
    } else if (field.start > previousEnd + 1) {
      warnings.push(
        `Unmapped gap at positions ${previousEnd + 1}–${field.start - 1}.`,
      );
    }

    previousEnd = Math.max(previousEnd, field.end);
  }

  if (previousEnd < rule.expectedLength) {
    warnings.push(
      `Unmapped gap at positions ${previousEnd + 1}–${rule.expectedLength}.`,
    );
  }

  return warnings;
}
