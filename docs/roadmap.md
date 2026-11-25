# Roadmap

Open work as of 25 November 2026, matching `AFIE_Workflow_v4.docx`. Step
numbers refer to that document.

## Open items from Phases 2–4

Several of these (2.6, 3.17 hardening, egress and `/metrics`) were done on
`main` at `527c6b6` and dropped by merge `2bc8f5c`; restore them from that
commit.

- **2.6** Pin the Argo CD version; the setup script installs `stable`.
- **2.12** Pin image tags in `infra/gitops/overlays/dev`.
- **3.17** Telemetry: non-root user and read-only root filesystem; egress
  NetworkPolicy to Prometheus; `/metrics` plus Service labels for the
  ServiceMonitor; node pressure queries; per-container requests and limits.
- **Health** `/health` stays healthy when Prometheus is down (see
  [telemetry-service.md §6](telemetry-service.md#6-failure-modes-and-operational-notes)).
- **4.16** C# ↔ Python schema contract test; live price source and stateful
  detection.
- **4.17** Forecast features: four extra dims (→ 59).

## Phase 5 — Reinforcement Learning Agent (weeks 11–15) — in progress

**Built.** `AFIEEnv`, `AFIEOfflineEnv`, 7-lever action decoder, reward,
offline PPO trainer.

**Remaining.**

- Azure Public Dataset V2 (VM traces 2019) download and Parquet conversion.
- Action masking with `sb3-contrib` MaskablePPO; Lagrangian SLO constraint.
- Full 1M-step training run; record the schema dimension in `metadata.json`.
- Online fine-tuning (70/30 live/offline) and training analysis notebook.
- SHAP explainer, explanation templates and counterfactuals.
- Heuristic fallback policy, FastAPI `POST /predict` on `localhost:5001`,
  model card, Azure ML `score.py`.

**Done when.** Zero masked-action leaks in eval; the API returns action, SHAP
and counterfactual for a 55-dim body; the model is versioned.

## Phase 6 — Operator + PCL + progressive autonomy (weeks 16–19)

- KubeOps operator, `ResourceRecommendation` and `AfiePolicy` CRDs.
- 9-rule PCL ([architecture.md §7](architecture.md#7-policy-constraint-language-pcl))
  with property-based tests; admission policy for R1, R2, R5.
- Kill switch, change budget, model drift guard.
- GitHub PR service, `IGitOpsBackend` (Argo, Flux, direct, in-place resize).
- Multi-window SLO burn-rate verification with rollback.
- Hash-chained audit log (Postgres in dev, Cosmos DB in prod).
- Autonomy levels L0 (shadow) to L3 (autonomous).

**Done when.** PCL and admission policy reject unsafe changes; the kill switch
halts within one loop; an injected latency regression is rolled back.

## Phase 7 — Dashboard and savings ledger (weeks 20–23)

- BFF API: savings, actions, override, SLO heatmap.
- Verified savings ledger with confidence intervals and FOCUS CSV export.
- What-if simulator and read-only MCP server.
- React app: cost, action history, SLO heatmap, autonomy and policy, what-if,
  training monitor; accessible and dark mode.

## Phase 8 — Azure, CI/CD, supply chain (weeks 24–25)

- Terraform: Event Hub, Cosmos DB, Key Vault, Static Web Apps; optional Azure
  ML and ACR. Flip services to cloud mode; workload identity.
- Existing CI: `ci.yaml` (.NET, Python, kubeconform) and `images.yml` (Trivy).
- Add dashboard CI, schema contract check, branch protection.
- SBOM, cosign signing, SLSA provenance, signed model.
- Carbon intensity provider, Helm chart, setup and reproduction scripts.

## Phase 9 — Experiments and paper (weeks 26–36)

- Four baselines (manual, VPA, threshold, AFIE) and four ablations; 3 seeds ×
  72 h each, with load spikes and fault injection.
- Mann-Whitney U with Bonferroni correction, effect sizes, bootstrap CIs.
- XAI study comparing SHAP with SHAP + counterfactual.
- IEEE paper, arXiv preprint, artifact DOI, open-source launch.

Every experiment can run on KIND if Azure is unavailable; the paper frames
results as "on a Kubernetes cluster".
