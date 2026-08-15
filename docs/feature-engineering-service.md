# Feature Engineering Service

ASP.NET Core 8 background worker that consumes `MetricEvent` records
produced by the Phase 3 telemetry service, maintains a per-workload
sliding window of samples, computes the 47-dimensional state vector
the reinforcement-learning agent will consume, and persists a snapshot
of every tracked workload to PostgreSQL every 60 seconds.

Source: [`src/api/feature-engineering/`](../src/api/feature-engineering/)
· Tests: [`tests/AFIE.FeatureEngineering.Tests/`](../tests/AFIE.FeatureEngineering.Tests/)

## 1. Responsibilities

- Tail today's `telemetry_{utc-date}.jsonl` file (dev) or consume from
  Azure Event Hub (Phase 8) and materialise `MetricEvent` records into
  an in-memory sliding window per workload.
- Compute the 47-dim state vector on demand via
  `GET /state/{workloadName}`, invariant-checked and clamped so the RL
  agent's observation space is deterministic.
- Emit one persisted vector per tracked workload every 60 seconds to
  Postgres, so the RL trainer has a historical dataset to replay.
- Expose `/health`, `/readiness`, and `/health/live` so Kubernetes and
  operators can distinguish "process alive" from "system healthy".

Not responsible for: model training, PCL rule evaluation, or
recommendation issuance — those live in Phase 5 (RL agent) and Phase 6
(operator).

## 2. Public surface

### 2.1 HTTP endpoints

| Verb | Path | Purpose |
| --- | --- | --- |
| GET | `/state/{workloadName}` | Current 47-dim vector as a JSON array. `404 {error, workload}` if the workload is not tracked. |
| GET | `/state/{workloadName}/latest` | Same values plus `{timestamp}` for debugging. |
| GET | `/health` | Composite: `feature-engineering` check + `postgres` (`AddNpgSql`) check. Degraded per rules in §6. |
| GET | `/readiness` | Same composite. Wired to the readiness probe. |
| GET | `/health/live` | Liveness only — predicate excludes all registered checks, so the endpoint returns 200 as long as the process is alive. Wired to the liveness probe. |

The service pulls (or reads a file, or reads Event Hub) — it never
receives push traffic.

### 2.2 Configuration

Every property comes from `appsettings.json`, overridable by
`FeatureEngineering__*` and `EventHub__*` environment variables
(double-underscore = `:` in ASP.NET config). One property is
**required at runtime** and never lives in the repo — see §4.4.

| Key | Default | Source in prod | Notes |
| --- | --- | --- | --- |
| `FeatureEngineering:WindowCapacity` | `240` | appsettings | 240 samples × 15s = 1h window |
| `FeatureEngineering:EventStalenessThresholdSeconds` | `60` | appsettings | Health-check staleness gate (informational) |
| `FeatureEngineering:ConsumerMode` | `local` | env in manifest | `local` \| `eventhub` |
| `FeatureEngineering:InputPath` | `experiments/results` | env `/app/data` in manifest | JSONL source dir |
| `FeatureEngineering:OffsetStatePath` | `experiments/state/fe_consumer_offset.json` | env `/app/experiments/state/…` | Consumer checkpoint |
| `FeatureEngineering:PollingIntervalMs` | `500` | appsettings | Tailer poll cadence |
| `FeatureEngineering:PublisherMode` | `postgres` | env in manifest | `postgres` \| `azureml` (Phase 8) |
| `FeatureEngineering:PostgresConnectionString` | *empty* | **Kubernetes Secret** via `secretKeyRef` (in cluster) or `dotnet user-secrets` (dev) | Startup throws if missing |
| `FeatureEngineering:EmitIntervalSeconds` | `60` | appsettings | Emitter cadence |
| `FeatureEngineering:ConfiguredBudgetUsdPerHour` | `10.0` | appsettings | Simulated cost feature (dim 29) |
| `FeatureEngineering:CpuCostPerCoreHourUsd` | `0.031` | appsettings | Simulated cost — plausible AKS D-series rate |
| `FeatureEngineering:MemCostPerGiBHourUsd` | `0.004` | appsettings | Simulated cost |
| `EventHub:*` | — | Phase 8 | Consumed only when `ConsumerMode=eventhub` |

## 3. Internals

```mermaid
flowchart LR
    JSONL[(shared PVC:<br/>telemetry_YYYY-MM-DD.jsonl)] -->|tail| C[LocalJsonlTailConsumer]
    C -->|MetricEvent| WS[WindowStore]
    WS -->|Snapshot| SVB[StateVectorBuilder]
    FG[8× IFeatureGroup] -->|compose| SVB
    AH[ActionHistoryStore] -->|Recent| SVB
    SVB -->|float[47]| API[/GET /state/{workload}/]
    SVB -->|float[47]| EM[StateVectorEmitterService<br/>every 60s]
    EM -->|BYTEA 188B| PG[(afie-postgres:<br/>state_vectors)]
    HS[FeatureEngineeringHealthState] -.reads.-> HC[/GET /health/]
    C -.updates.-> HS
    EM -.updates.-> HS
```

### 3.1 Consumers

- **`IMetricEventConsumer`** — marker interface. The two implementations
  are both `IHostedService`s; `Program.cs` selects one via
  `AddHostedService<T>` based on `ConsumerMode`.
- **`LocalJsonlTailConsumer`** (dev, default). 500 ms polling
  `BackgroundService`. Tracks byte offset in
  `experiments/state/fe_consumer_offset.json`; flushes every 50 events
  and at shutdown. Handles daily UTC rollover — switches to today's file
  when the previous file has been idle >30 s. Malformed JSONL lines are
  logged and skipped. Opens with `FileShare.ReadWrite | FileShare.Delete`
  so it never blocks the telemetry writer.
- **`EventHubConsumer`** (Phase 8 stub). Selected when
  `ConsumerMode=eventhub`; logs a warning and no-ops. Real implementation
  arrives in Phase 8.

### 3.2 Sliding window

- **`CircularBuffer<T>`** — hand-rolled ~40 lines. Thread-safe via a
  per-buffer `lock`. `Snapshot()` returns `T[]` in oldest→newest order.
- **`WindowStore`** — `ConcurrentDictionary<workload, CircularBuffer<MetricEvent>>`.
  Consumer is the sole writer; HTTP handler + emitter are readers. Ctor
  validates `WindowCapacity > 0` and throws `ArgumentOutOfRangeException`
  fast at boot.
- **`ActionHistoryStore`** — same shape, capacity 3, for the action
  history feature group (dims 38–46). Empty until Phase 6 posts to it.

### 3.3 Feature groups + builder

`IFeatureGroup` contract:

```csharp
public interface IFeatureGroup
{
    int StartDim { get; }
    int Length { get; }
    void Compute(FeatureContext ctx, Span<float> destination);
}
```

Eight groups compose the 47-dim vector (see
[architecture.md §5](architecture.md#5-the-47-dimensional-state-vector)
for the full dimension table):

| Class | Dims | Content |
| --- | --- | --- |
| `CpuFeatures` | 0–8 | `{P50, P95, P99} × {5m, 15m, 1h}` of `CpuUsageRate / max(CpuLimit, ε)` (MathNet percentile) |
| `MemoryFeatures` | 9–17 | Same shape for `MemoryBytes / max(MemLimit, ε)` |
| `AppSignalFeatures` | 18–23 | req/s (`tanh`), error rate, latency P50/P95/P99, one reserved dim |
| `NodePressureFeatures` | 24–26 | CPU/mem pressure flags + eviction proximity |
| `CostFeatures` | 27–29 | Simulated `$/hr`, 7-day trend (0, Phase 8), budget fraction |
| `TemporalFeatures` | 30–34 | sin/cos hour, sin/cos day-of-week, days-since-deploy |
| `DeploymentFeatures` | 35–37 | replicas, HPA target, rolling flag (placeholders — Phase 6) |
| `ActionHistoryFeatures` | 38–46 | last 3 × (cost Δ, SLO Δ, minutes since) |

`StateVectorBuilder` composes them:
- Ctor asserts the groups cover `[0, 47)` contiguously and sum to 47;
  throws `InvalidOperationException` otherwise.
- `Build(workload, samples, now)` dispatches each group to its span
  slice, then clamps `[-2, 2]` and coerces `NaN` / `±Inf` to 0.

### 3.4 Publisher + schema

- **`IStateVectorPublisher`** — strategy interface with `EnsureReadyAsync`
  + `PublishAsync`. Selected in `Program.cs` by `PublisherMode`.
- **`PostgresStateWriter`** (default). Dapper + `NpgsqlDataSource`
  singleton. `EnsureReadyAsync` runs the DDL at boot; if Postgres is
  unreachable, the pod crashes with a clear error rather than serving a
  broken loop. `PublishAsync` writes the 47 floats as a 188-byte
  `BYTEA` blob via `Buffer.BlockCopy` — little-endian float32, matching
  how the Phase 5 Python trainer will `np.frombuffer(row['vector'],
  dtype='<f4')`. Every call updates
  `FeatureEngineeringHealthState.PostgresReachable` +
  `StateVectorsWrittenTotal`.
- **`AzureMlFeatureStorePublisher`** (Phase 8 stub). Logs a warning and
  throws `NotImplementedException` if invoked.

Schema (executed by `EnsureReadyAsync`):

```sql
CREATE TABLE IF NOT EXISTS state_vectors (
  id         BIGSERIAL PRIMARY KEY,
  workload   TEXT        NOT NULL,
  namespace  TEXT        NOT NULL,
  ts         TIMESTAMPTZ NOT NULL,
  vector     BYTEA       NOT NULL,
  CHECK (octet_length(vector) = 188)
);
CREATE INDEX IF NOT EXISTS idx_sv_workload_ts
  ON state_vectors(workload, ts DESC);
```

The `(workload, ts DESC)` index matches the Phase 5 access pattern:
"give me the last N vectors for this workload".

### 3.5 Emitter cadence

`StateVectorEmitterService` (`BackgroundService`) ticks every 60 s
(configurable via `EmitIntervalSeconds`). For each workload in
`WindowStore.Workloads` it snapshots samples, builds a vector, and
publishes. Per-workload try/catch means one failing workload doesn't
break the others. `GET /state/{workloadName}` always computes fresh
from the current buffer — it does not read from Postgres.

## 4. Deployment

### 4.1 Container image

Multi-stage [`Dockerfile`](../src/api/feature-engineering/Dockerfile):
`mcr.microsoft.com/dotnet/sdk:8.0` for build →
`mcr.microsoft.com/dotnet/aspnet:8.0-alpine` for runtime. Runs as the
non-root `app` user. Creates `/app/experiments/results` and
`/app/experiments/state` in the image so the deployment can mount
volumes at those paths without permission issues.

### 4.2 Kubernetes manifest

[`infra/gitops/manifests/feature-engineering-deployment.yaml`](../infra/gitops/manifests/feature-engineering-deployment.yaml):

- Namespace `afie-system`, `replicas: 1`, `strategy: Recreate` — the
  offset file and shared JSONL are single-writer surfaces.
- **Init container** (`busybox:1.36`) runs
  `until nc -z afie-postgres 5432; do sleep 2; done` so the app never
  starts against a cold DB.
- **Pod affinity** requires co-location with the `afie-telemetry` pod
  on the same node — both mount the shared `afie-telemetry-data` RWO
  PVC, and RWO can't cross nodes.
- **Security context**: non-root uid 1000,
  `allowPrivilegeEscalation: false`, `seccompProfile: RuntimeDefault`.
- **Volumes**:
  - `telemetry-in` → PVC `afie-telemetry-data`, `readOnly: true`,
    mounted at `/app/data`.
  - `fe-state` → PVC `afie-fe-state`, mounted at
    `/app/experiments/state` — separate PVC so a redeploy doesn't lose
    the consumer offset.
- **Env**:
  - `FeatureEngineering__ConsumerMode=local`
  - `FeatureEngineering__PublisherMode=postgres`
  - `FeatureEngineering__InputPath=/app/data`
  - `FeatureEngineering__OffsetStatePath=/app/experiments/state/fe_consumer_offset.json`
  - `FeatureEngineering__PostgresConnectionString` from
    `secretKeyRef: {name: afie-postgres, key: CONNECTION_STRING}`
- **Probes**: `readinessProbe` → `/readiness` (delay 5s, period 10s);
  `livenessProbe` → `/health/live` (delay 15s, period 30s).
- **Resources**: 100m/128Mi req, 500m/512Mi limit.

Companion resources shipped in PR 6:
- `postgres-statefulset.yaml` + `postgres-service.yaml` — single-replica
  `postgres:16-alpine` with a 5Gi PVC template, `pg_isready` probes.
- `afie-telemetry-data-pvc.yaml` (1Gi RWO) — the shared JSONL volume.
- `afie-fe-state-pvc.yaml` (1Gi RWO) — the FE consumer offset volume.
- `feature-engineering-secret.example` — template only; **the
  authoritative bootstrap is §4.4 below**. The example file uses
  outdated key names; always match what the deployment actually reads.

### 4.3 Local deploy loop

Prerequisite: the one-time secret bootstrap in §4.4.

```bash
docker build -t afie-feature-engineering:dev \
  -f src/api/feature-engineering/Dockerfile .
kind load docker-image afie-feature-engineering:dev --name afie-dev

kubectl apply -f infra/gitops/manifests/afie-telemetry-data-pvc.yaml
kubectl apply -f infra/gitops/manifests/afie-fe-state-pvc.yaml
kubectl apply -f infra/gitops/manifests/postgres-service.yaml
kubectl apply -f infra/gitops/manifests/postgres-statefulset.yaml
kubectl -n afie-system rollout status statefulset/afie-postgres --timeout=120s
kubectl apply -f infra/gitops/manifests/feature-engineering-deployment.yaml
kubectl -n afie-system rollout status deploy/afie-feature-engineering --timeout=120s

kubectl -n afie-system port-forward svc/afie-feature-engineering 8081:8080
curl -s localhost:8081/health | jq
```

### 4.4 Secret bootstrap

The manifest reads a single `Secret` named `afie-postgres` in
`afie-system`. It carries both the DB init variables (consumed by the
Postgres StatefulSet via `envFrom`) and the connection string
(consumed by the FE deployment via `secretKeyRef`). One command, one
rotation surface:

```bash
PG_PASSWORD="$(openssl rand -hex 12)"
kubectl -n afie-system create secret generic afie-postgres \
  --from-literal=POSTGRES_DB=afie \
  --from-literal=POSTGRES_USER=afie \
  --from-literal=POSTGRES_PASSWORD="$PG_PASSWORD" \
  --from-literal=CONNECTION_STRING="Host=afie-postgres;Port=5432;Database=afie;Username=afie;Password=$PG_PASSWORD"
```

**Rotation:** re-run the command with a new password + `kubectl rollout
restart statefulset/afie-postgres deploy/afie-feature-engineering`. Env
var-based Secrets only refresh on pod restart.

**Phase 8 replaces this** with Azure Key Vault via the Secrets Store
CSI driver — no `kubectl create secret` step, no manual rotation.

## 5. Testing

xUnit suite at [`tests/AFIE.FeatureEngineering.Tests`](../tests/AFIE.FeatureEngineering.Tests):

- `Services/CircularBufferTests`, `WindowStoreTests` — thread-safety
  and boundary invariants.
- `Features/CpuFeaturesTests`, `MemoryFeaturesTests`,
  `NodePressureFeaturesTests`, `ActionHistoryFeaturesTests`,
  `TemporalFeaturesTests`, `StateVectorBuilderTests` — per-group unit
  tests + composition tests (temporal cyclic adjacency,
  clamp/coercion behaviour, 47-dim length invariant).
- `Consumers/LocalJsonlTailConsumerTests` — temp-dir integration:
  fresh events land in the store, offset resumes across restart,
  malformed lines skipped.
- `Publishers/PostgresStateWriterTests` — Testcontainers spins up an
  ephemeral `postgres:16-alpine` per test class; verifies schema
  creation, `octet_length(vector) = 188` round-trip, health-state
  update, and unreachable-DB throws.
- `Endpoints/StateEndpointsTests` — `WebApplicationFactory<Program>`;
  404 for unknown workload, 200 with 47-length array for known.

Run:

```bash
dotnet test tests/AFIE.FeatureEngineering.Tests/
```

**Docker is required** for the Testcontainers-backed Postgres tests.

## 6. Failure modes and operational notes

| Failure | What happens | Signal |
| --- | --- | --- |
| Postgres down at boot | `EnsureReadyAsync` throws, pod exits — clear error in logs | pod status `CrashLoopBackOff`, log line "Postgres schema init failed" |
| Postgres down at runtime | Insert throws, `PostgresReachable=false`, `/health` reports Degraded | `/health` data + pod log |
| Source JSONL missing | Tailer marks `SourceFileReachable=false`, loop continues | `/health` Degraded |
| Malformed JSONL line | Line skipped, warning logged, loop continues | `LocalJsonlTailConsumer` log |
| Consumer heartbeat stale | Last event older than 45 s (`15s × 3`) → `/health` Degraded | `lastEventConsumedTime` in `/health` data |
| Daily UTC rollover | Tailer switches to `telemetry_{today}.jsonl` once the previous file has been idle >30 s | expected; no alert |
| Pod restart | In-memory `WindowStore` re-fills from consumer resume position | brief `Degraded` until first event lands |
| Feature group throws | `StateVectorEmitterService` catches per-workload; other workloads unaffected | emitter log line "Emit failed for {Workload}" |

## 7. What changes in Phase 8

- Set `FeatureEngineering__ConsumerMode=eventhub` and populate
  `EventHub__*` — the code path exists; the stub becomes the real
  `EventProcessorClient` implementation.
- Set `FeatureEngineering__PublisherMode=azureml` — the stub becomes a
  Feature Store REST client. Postgres becomes optional (still useful
  as a cache).
- Move the Postgres `Secret` to Azure Key Vault via the Secrets Store
  CSI driver — the manifest's `secretKeyRef` swaps for a
  `SecretProviderClass` mount; no code change.
- Consider a `NetworkPolicy` locking `afie-postgres` to the FE pod's
  ServiceAccount — currently open to any pod in `afie-system`.
- Cost feature dim 28 (7-day trend) becomes real once Cosmos DB
  provides a long-window store for the trend calculation.