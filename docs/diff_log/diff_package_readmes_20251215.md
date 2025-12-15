# diff: Add per-package READMEs for NuGet

## Summary
- Add `README.md` to each published package:
  - `Kafka.Context.Abstractions`
  - `Kafka.Context.Messaging`
  - `Kafka.Context.Application`
  - `Kafka.Context.Infrastructure`
- Configure each `.csproj` to expose the README on NuGet:
  - `<PackageReadmeFile>README.md</PackageReadmeFile>`
  - `<None Include="README.md" Pack="true" PackagePath="\" />`

## Rationale
- These packages are published separately, but without per-package documentation users cannot understand intent/usage.

## Impact
- Documentation-only change in package contents/metadata (no runtime behavior change).

