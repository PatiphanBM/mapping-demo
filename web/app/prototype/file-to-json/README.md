# File-to-JSON prototype decision record

This directory is the primary-source capture of the throwaway Phase 25 UI exploration. Variant C, `Mapping workbench`, was selected because destination choice belongs above one shared input/contract editor and makes `LegacyTable`, whole-file `JsonFile`, and per-record `ApiContract` visibly exclusive.

The production implementation is `web/components/mapping/mapping-workbench.tsx` plus the real API clients and contract editors. Production code must not import this directory, its mock state, or its fixtures. Keep `/prototype/file-to-json` only as decision evidence; changes to runtime behavior belong in production modules, specifications and acceptance tests.
