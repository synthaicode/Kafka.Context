# Repository structure (proposal)

このリポジトリのフォルダ構成案（MVP）を示す。

```
.
├─ src/
│  ├─ Kafka.Context/                 # main package (Kafka.Context)
│  ├─ Kafka.Context.Abstractions/    # public abstractions/interfaces
│  ├─ Kafka.Context.Application/     # options/builder (KafkaContextOptions etc.)
│  ├─ Kafka.Context.Messaging/       # MessageMeta, DlqEnvelope, headers, guards
│  └─ Kafka.Context.Infrastructure/  # provisioning/admin/SR tools integrations
│
├─ tests/
│  ├─ unit/
│  │  └─ Kafka.Context.Tests/        # unit tests (MVP)
│  └─ physical/
│     └─ README.md                   # physical tests (docker-backed)
│
├─ examples/
│  ├─ quickstart/                    # minimal AddAsync/ForEachAsync
│  ├─ manual-commit/                 # autoCommit=false + Commit(entity)
│  └─ error-handling-dlq/            # retry + DLQ
│
├─ docs/
│  ├─ contracts/                     # external contracts (API/Retry/DLQ/Provisioning)
│  ├─ environment/                   # docker compose etc.
│  ├─ diff_log/                      # decision history
│  ├─ workflows/                     # working agreements
│  └─ samples/                       # target code shape (docs-only)
│
└─ features/
   └─ mvp/                           # instruction.md as work entry point
```
