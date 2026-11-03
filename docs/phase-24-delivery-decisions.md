# Phase 24 API Delivery Decisions

Owner sign-off: 2026-10-20

| Decision | Approved value | Owner role |
|---|---|---|
| PA authentication | mTLS certificate from secret reference `pa-mtls-client-certificate` | Contract / Security |
| Claims authentication | OAuth 2.0 client credentials from secret reference `claims-oauth-client` | Contract / Security |
| Idempotency | `Idempotency-Key`; initial key is `configVersionId:fileJobId:sourceRowId:outputKey`; partner retention is 30 days | Contract |
| Timeout | PA 10 seconds; Claims 15 seconds | Contract / Platform |
| Success | Every `2xx` response is successful | Contract |
| Retry | Network, timeout, `408`, `429`, and `5xx`; maximum 6 attempts; bounded exponential backoff with jitter; honor a bounded `Retry-After` | Platform / Operations |
| Correlation | PA uses `X-Correlation-Id`; Claims uses `X-Request-Id`; only response metadata is persisted | Contract / Operations |
| Payload protection | AES-256-GCM with a server-owned key reference; payload expires 30 days after terminal state; attempt metadata expires after 365 days | Security |
| Identifier format | Persisted numeric IDs use invariant decimal strings; source row number is one-based | Contract |
| Extension objects | Only properties explicitly declared by the pinned schema are accepted | Contract |
| Reprocess | A new Config Version creates a new identity; the same version remains idempotent | Business / Contract |
| Delivered resend | Never an automatic retry; explicit authorized action creates an audited job and a new key suffix `:resend:{sequence}` | Business / Security |
| Rollback thresholds | Immediate rollback for duplicate, secret/PII leak, or schema validation failure greater than zero; pause for permanent failure over 1%, retryable failure over 5%, p95 over 5 seconds, or queue age over 10 minutes | Platform / Operations |

The JSON Schema dialect/subset and `additionalProperties` behavior were accepted at Gate C. Existing file/workbook limits remain unchanged.

## Stub scenarios

The delivery test stub must support deterministic `2xx`, `400`, `408`, `429`, `5xx`, timeout, commit-before-timeout, and idempotency-conflict responses. It must record request bytes and non-secret headers without logging the payload body.

## Rollback

Phase 24 migrations are expand-only. Disable the API delivery worker and ApiContract activation to stop external side effects; do not reroute pinned API jobs to LegacyTable and do not drop delivery audit tables during the retention window.
