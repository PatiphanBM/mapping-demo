import type { ApiProblemDetails, ValidationErrors } from "./contracts";

export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail?: string;
  readonly errors: ValidationErrors;

  constructor(
    status: number,
    title: string,
    detail?: string,
    errors: ValidationErrors = {},
  ) {
    super(detail ?? title);
    this.name = "ApiError";
    this.status = status;
    this.title = title;
    this.detail = detail;
    this.errors = errors;
  }
}

export interface ApiRequestOptions extends Omit<RequestInit, "body"> {
  body?: unknown;
}

export async function apiRequest<TResponse>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<TResponse> {
  const { body, headers: initialHeaders, ...requestInit } = options;
  const headers = new Headers(initialHeaders);
  headers.set("Accept", "application/json");

  if (body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(toApiPath(path), {
    ...requestInit,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  const content = await response.text();
  return content.length === 0
    ? (undefined as TResponse)
    : (JSON.parse(content) as TResponse);
}

function toApiPath(path: string): string {
  return `/api${path.startsWith("/") ? path : `/${path}`}`;
}

async function toApiError(response: Response): Promise<ApiError> {
  const payload = (await response
    .json()
    .catch(() => null)) as ApiProblemDetails | null;
  const title = payload?.title ?? response.statusText ?? "Request failed";

  return new ApiError(
    response.status,
    title,
    payload?.detail,
    readValidationErrors(payload?.errors),
  );
}

function readValidationErrors(
  errors: ValidationErrors | undefined,
): ValidationErrors {
  return errors ?? {};
}
