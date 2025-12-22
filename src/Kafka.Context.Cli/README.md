# Kafka.Context.Cli (CLI)

A dotnet tool that fetches Avro schemas from Schema Registry and helps you generate/validate POCOs for Kafka.Context.

## Install

```powershell
dotnet tool install -g Kafka.Context.Cli
```

Update:

```powershell
dotnet tool update -g Kafka.Context.Cli
```

## Commands

### 1) `schema scaffold`

Generate a C# type (record/class) from a Schema Registry subject.

```powershell
kafka-context schema scaffold --sr-url http://127.0.0.1:18081 --subject orders-value --output ./Generated
```

Options:
- `--sr-url <url>`: Schema Registry URL (or env `KAFKA_CONTEXT_SCHEMA_REGISTRY_URL`)
- `--subject <subject>`: Schema Registry subject (required)
- `--output <dir>`: Output directory (default: `./`)
- `--namespace <ns>`: Generated namespace (default: `Kafka.Context.Generated`)
- `--style <record|class>`: Type style (default: `record`)
- `--topic <topic>`: Force `[KafkaTopic]` value (default: derived from `*-value` / `*-key` subject names)
- `--force`: Overwrite existing files
- `--dry-run`: Print generated code to stdout (no file write)

The generated type includes:
- `[KafkaTopic("<topic>")]` (when derived/provided)
- `[SchemaSubject("<subject>")]`
- `[SchemaFingerprint("<fingerprint>")]`

### 2) `schema verify` (recommended in CI)

Validate that the latest Schema Registry schema fingerprint matches the fingerprint embedded in your type.

```powershell
kafka-context schema verify --sr-url http://127.0.0.1:18081 --subject orders-value --type "Kafka.Context.Generated.Order, MyApp"
```

Options:
- `--sr-url <url>`: Schema Registry URL (or env `KAFKA_CONTEXT_SCHEMA_REGISTRY_URL`)
- `--subject <subject>`: Schema Registry subject (required)
- `--type <assembly-qualified-type>`: Target type (optional)
- `--fingerprint <hex>`: Expected fingerprint (optional)

You must provide either `--type` or `--fingerprint`.

Examples:

```powershell
# Verify using an embedded fingerprint attribute on your type
kafka-context schema verify --sr-url http://127.0.0.1:18081 --subject orders-value --type "Kafka.Context.Generated.Order, MyApp"

# Verify without loading any assemblies (CI-friendly)
kafka-context schema verify --sr-url http://127.0.0.1:18081 --subject orders-value --fingerprint 0123abcd...
```

Exit codes:
- `0`: OK (match)
- `4`: Mismatch

### 3) `schema subjects`

List subjects from Schema Registry (useful to discover what to scaffold/verify).

```powershell
kafka-context schema subjects --sr-url http://127.0.0.1:18081
```

Filtering:

```powershell
kafka-context schema subjects --sr-url http://127.0.0.1:18081 --prefix orders-
kafka-context schema subjects --sr-url http://127.0.0.1:18081 --contains -value
```

JSON output:

```powershell
kafka-context schema subjects --sr-url http://127.0.0.1:18081 --json
```

### 4) `streaming flink with-preview`

Preview merged Flink Kafka connector `WITH (...)` properties from `appsettings.json` as **DDL**.

```powershell
kafka-context streaming flink with-preview --config ./appsettings.json
```

With POCO assemblies (include column definitions):

```powershell
kafka-context streaming flink with-preview --config ./appsettings.json --assembly ./MyApp.dll
```

Filter:

```powershell
kafka-context streaming flink with-preview --config ./appsettings.json --kind source
kafka-context streaming flink with-preview --config ./appsettings.json --kind sink --topic orders
```

JSON output:

```powershell
kafka-context streaming flink with-preview --config ./appsettings.json --json
```

Notes:
- If `--assembly` is provided, the CLI loads POCOs with `[KafkaTopic]` to emit column definitions.
- If no matching POCO exists, use `--allow-missing-types` to fall back to skeleton output.

This command reads:
- `KsqlDsl:Common` (bootstrap + security properties)
- `KsqlDsl:SchemaRegistry` (Schema Registry URL)
- `KsqlDsl:Streaming:Flink:With` (global WITH)
- `KsqlDsl:Streaming:Flink:Sources` (per-topic source WITH)
- `KsqlDsl:Streaming:Flink:Sinks` (per-topic sink WITH)

Additional options:
- `--assembly <path[,path]>`: Load assemblies to resolve POCOs by `[KafkaTopic]` name.
- `--allow-missing-types`: Allow topics without matching POCOs (placeholder columns).

### 5) `ai guide`

Use this to teach your AI assistant how to support Kafka.Context users.
It outputs a curated guide you can paste into ChatGPT/Copilot/Cursor so the AI can answer with correct APIs, constraints, and examples.
Use `--rules` when you only want the conversation protocol and response style.

```powershell
kafka-context ai guide
kafka-context ai guide --rules
kafka-context ai guide --copy
kafka-context ai guide --rules --copy
```

Options:
- `--rules`: Output `ai_guide_conversation_rules.md` instead of `AI_DEVELOPMENT_GUIDE.md`.
- `--copy`: Copy the output to the clipboard (best-effort).

Notes:
- Clipboard support uses OS-native tools: `clip` (Windows), `pbcopy` (macOS), `xclip`/`xsel` (Linux).
- If no clipboard tool is available, the CLI prints a warning and still writes to stdout.

## What if a topic has multiple SR subjects?

This CLI operates on **subjects**, not topics. If your environment has multiple subjects for a single topic, you must run `scaffold`/`verify` **per subject**.

Typical Kafka.Context convention:
- key: `<topic>-key`
- value: `<topic>-value`

In that case, run it twice if you need both. `--topic` only affects the generated `[KafkaTopic]` attribute; it does not select or discover subjects.

## Notes / limitations

- Fingerprint calculation is: normalized JSON (object keys sorted) + SHA-256, to keep `scaffold` and `verify` consistent.
- Only schemas where the **root is an Avro `record`** are supported (no full coverage for advanced Avro features like references).
