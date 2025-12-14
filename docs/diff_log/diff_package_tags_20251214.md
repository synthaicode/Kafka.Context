# diff: PackageTags fix (remove cli/devops)

## Summary
- Remove `cli` and `devops` from NuGet package tags.

## Rationale
- Current OSS scope is a library/runtime; `cli` / `devops` tags are misleading for discovery.

## Impact
- Packaging metadata only (no runtime/API changes).

