# diff: Publish dependent packages to registries

## Summary
- Publish all `Kafka.Context.*` packages (`Abstractions` / `Messaging` / `Application` / `Infrastructure` / root `Kafka.Context`) to package registries.

## Rationale
- `Kafka.Context` NuGet package currently depends on internal packages via `ProjectReference`, so consumers need those packages available in the same feed for restore to succeed.
- This keeps the restore experience consistent for GitHub Packages RC verification and for nuget.org stable releases.

## Impact
- Release/publish pipeline changes only (`.github/workflows/*`).
- No runtime/API behavior changes.

