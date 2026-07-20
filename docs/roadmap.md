# Roadmap

Phases 1–3 are complete. Phases 4–9 are the remaining scope from
`AFIE_Workflow_updated.docx`. This doc summarises each phase, its
dependencies, and the acceptance criteria used to call it "done".

## Phase 4 — Feature Engineering Service (weeks 8–10)

**Goal.** Turn a stream of `MetricEvent`s into the 47-dim state vector
consumed by the RL agent, the operator, and the dashboard.

**Scope.**

- New service `src/api/feature-engineering/` (ASP.NET Core 8).
- Consumer: tails JSONL files today; switches to `EventProcessorClient`
  in Phase 8. Both code paths present, config-switched.
- `ConcurrentDictionary<workload, CircularBuffer>` — 240 samples per
  workload (1 hour at 15s cadence).
- 8 feature-group computations (see
  [architecture.md §5](architecture.md#5-the-47-dimensional-state-vector)).
- `StateVectorBuilder` asserting length 47, clamping NaN / out-of-range
  to safe values.
- Persistence: `LocalStateWriter` → SQLite at
  `experiments/state_vectors.db`; Azure ML Feature Store writer coded
  but disabled.
- HTTP: `GET /state/{workloadName}` returns the current `float[47]`.
- Tests per feature group with known input → known output; boundary
  cases on the temporal encoding are load-bearing (hour 0 == hour 24).

**Depends on.** Phase 3 output format (`MetricEvent`).
**Done when.** Service running in `afie-system`, `/state/{workload}`
returns 47 values all in `[-1, 1]`, and all 8 feature-group tests pass.

## Phase 5 — Reinforcement Learning Agent (weeks 11–15)

**Goal.** Train a PPO policy on the Azure Public Dataset 2020, add SHAP
explanations, and serve inference locally.

**Scope.**

- Python 3.11 venv under `src/ml/`.
- Gymnasium env `AFIEEnv` (live) and `AFIEOfflineEnv` (dataset replay).
- Action decoder — `Discrete(25)`.
- Reward function per
  [architecture.md §6](architecture.md#6-action-space-and-reward).
- Offline training script — SB3 PPO, 1M timesteps, 4 parallel envs.
- Online fine-tune script — 24-hour update cycles, 70/30 replay buffer.
- SHAP `KernelExplainer` on the policy net + English explanation
  templates.
- Local Flask server (`serve_local.py`) on `localhost:5001` exposing
  `POST /predict`.
- Azure ML `score.py` written, deployment deferred to Phase 8.

**Depends on.** Nothing runtime — training uses the offline dataset.
Inference depends on Phase 4 for live state vectors.
**Done when.** Trained model committed under `src/ml/models/` with a
model card, mean reward > 0.5 in TensorBoard, and the Flask endpoint
returns action + confidence + SHAP for a 47-dim POST body.

## Phase 6 — Kubernetes Operator + PCL (weeks 16–19)

**Goal.** Watch `ResourceRecommendation` CRDs, validate against the
policy engine, open GitHub PRs, and roll back on SLO regression.

**Scope.**

- .NET 8 operator using KubeOps + Octokit under `src/operator/`.
- `ResourceRecommendation` CRD — schema at
  `infra/gitops/crds/resource-recommendation.yaml`.
- `PolicyEngine` — 4 rules
  ([architecture.md §7](architecture.md#7-policy-constraint-language-pcl)).
  100% test coverage on rule boundaries.
- `WorkloadStateProvider` — reads Prometheus + Kubernetes API + audit
  SQLite.
- `RecommendationController` — reconcile loop: fetch state → call
  `localhost:5001/predict` → PCL → open PR → update CRD status.
- `HelmPatchGenerator` — writes plain manifests to
  `infra/gitops/manifests/{workload}.yaml`.
- `IGitOpsBackend` abstraction — `ArgoBackend`, `FluxBackend`,
  `DirectApplyBackend`.
- `HealthChecker` background service — waits for sync completion, samples
  P99 + error rate at T+60s, reverts commit if SLO regressed.
- Audit log — SQLite in dev, Cosmos DB in Phase 8. Every PCL decision
  captured with reason.

**Depends on.** Phase 4 for state, Phase 5 for `/predict`.
**Done when.** Operator running in `afie-system`, PCL rejecting invalid
recommendations under test, a real PR opened against the repo with a
Helm patch and SHAP-derived explanation in the body.

## Phase 7 — Dashboard (weeks 20–23)

**Goal.** Human-facing view of savings, action history, XAI, SLO health,
and manual override.

**Scope.**

- BFF API `src/api/bff/` (ASP.NET Core 8).
  - `GET /api/savings/summary`
  - `GET /api/actions` (paginated)
  - `POST /api/actions/{id}/override` (reverts via git commit)
- React + Vite + TypeScript app under `src/dashboard/`.
  - `CostDashboard` — Recharts area/bar charts, React Query polling.
  - `ActionHistory` — paginated table with click-through detail sheet.
  - `ActionDetail` — before/after resources, SHAP bar chart, XAI
    sentence, override button.
  - `SLO Compliance` — heatmap of workloads × hours.
  - `RL Training Monitor` — reward curve + model version.
  - `Settings` — per-namespace PCL sliders, human-approval toggle.
- Components: `MetricCard`, `ShapBarChart`, `WorkloadStatusBadge`.
- Auth deferred to Phase 8 (Azure AD).

**Depends on.** Phase 6 audit log for history and overrides.
**Done when.** All 5 pages render live data from a locally-running BFF
against the SQLite audit log and a live cluster.

## Phase 8 — Azure Services, CI/CD, GitOps hardening (weeks 24–25)

**Goal.** Flip config from local mode to cloud mode; add pipelines.

**Scope.**

- Terraform in `infra/terraform/` — Event Hub, Cosmos DB, Key Vault,
  optional Azure ML, optional ACR, Static Web Apps.
- GitHub Actions:
  - `.github/workflows/dotnet.yml` — build + test on PR.
  - `.github/workflows/docker.yml` — image build + push on merge to
    `main`.
  - `.github/workflows/dashboard.yml` — Vite build + Static Web App
    deploy.
- Config flips (no code change required for any of these):
  - Telemetry `OutputMode` → `eventhub`.
  - Feature engineering consumer → Event Hub.
  - Audit log → Cosmos DB.
  - Operator `/predict` URL → Azure ML endpoint (if provisioned;
    otherwise stay on local Flask).
- Managed identities + Key Vault references on every service.

**Depends on.** All prior phases running locally.
**Done when.** Same code path runs on Azure services end-to-end for at
least one workload, with green CI on `main`.

## Phase 9 — Experiments, IEEE paper, publication (weeks 26–36)

**Goal.** Produce the results and the paper.

**Scope.**

- Benchmark workloads under `infra/benchmarks/workloads/` — reproducible
  synthetic load per baseline namespace.
- Experiment runs:
  1. AFIE vs `baseline-manual` (human requests).
  2. AFIE vs `baseline-vpa` (Vertical Pod Autoscaler).
  3. AFIE vs `baseline-threshold` (naive utilisation threshold).
  4. AFIE self-comparison — with vs without SHAP-informed reward.
- Analysis notebooks under `experiments/analysis/` — cost delta, SLO
  compliance, action count, rollback rate.
- Human study under `experiments/human_study/` — SRE evaluators rank XAI
  explanations for actionability.
- Paper drafting under `paper/sections/`.
- arXiv preprint + venue submission.

**Depends on.** Phase 8 for cloud numbers, but every experiment can run
on KIND if Azure is unavailable — the paper explicitly frames results as
"on a Kubernetes cluster".

## Cross-phase backlog (not on the critical path)

- Replace `NodeCpuPressure` PromQL with a load-based signal (see
  [telemetry-service.md §3.3](telemetry-service.md#33-prometheusqueries)).
- Integration test suite that stands up a real Prometheus in Docker and
  runs the telemetry scraper against it end-to-end.
- Auto-sync policy for the ArgoCD app once the workflow is proven.
- Grafana dashboards specific to AFIE (savings, action rate, PCL
  rejections) — currently piggy-backing on the standard pod dashboards.
