# Core Namespace Restructure Plan

## Summary
Documentation will drop references to `KeyAttribute` and clearly recommend using `EntityModelBuilder.HasKey` with LINQ expressions. Any lingering attribute examples will be removed.

## Motivation
`architecture_diff_20250711.md` noted the mismatch between current docs and the LINQ-based implementation. Aligning with `architecture_restart_20250711.md` keeps guidance consistent with the code base.

## Scope
The focus is on documentation and example cleanup. If a `KeyAttribute` class still exists, mark it `Obsolete` for eventual removal.

## Affected Files and Approach
- `docs/namespaces/core_namespace_doc.md` – rewrite key specification section
- `docs/namespaces/window_namespace_doc.md` – update references to key attributes
- `examples/` – search and replace attribute usage with `HasKey()` examples

## Notes and Concerns
Most of the changes are textual but we must verify no tutorials rely on the old attribute style. Tests referencing sample code may require updates.
