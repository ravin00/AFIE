# AFIE Documentation

AFIE — Autonomous FinOps Intelligence Engine for Kubernetes. A closed-loop
system that scrapes cluster telemetry, learns a resource-adjustment policy
with reinforcement learning, validates each recommendation against a policy
engine, and applies changes through GitOps pull requests.

This directory contains the engineering documentation. The IEEE paper drafts
live under `../paper/`; the phase-by-phase implementation workflow lives in
`AFIE_Workflow_updated.docx` at the repo owner's desktop.

## Start here

| Doc | What it covers |
| --- | --- |
| [architecture.md](architecture.md) | System overview, components, data flow, tech stack |
| [telemetry-service.md](telemetry-service.md) | Phase 3 service — Prometheus scraping, MetricEvent JSONL sink |
| [feature-engineering-service.md](feature-engineering-service.md) | Phase 4 service — sliding window, 47-dim state vector, `/state` endpoint, Postgres persistence |
| [infrastructure.md](infrastructure.md) | KIND cluster, Prometheus/Grafana, ArgoCD, Postgres |
| [development.md](development.md) | Build, test, and run locally |
| [roadmap.md](roadmap.md) | Planned scope for Phases 5–9 |
| [doc.md](doc.md) | Local dev credentials cheatsheet |

## Current status

- **Phase 1 (env + monorepo):** complete
- **Phase 2 (KIND + Prometheus/Grafana + ArgoCD):** complete
- **Phase 3 (telemetry pipeline):** complete — see [telemetry-service.md](telemetry-service.md)
- **Phase 4 (feature engineering + Postgres):** complete — see [feature-engineering-service.md](feature-engineering-service.md)
- **Phases 5–9:** not started — see [roadmap.md](roadmap.md)

## Repository layout

```
src/
  api/
    telemetry/           Phase 3 — ASP.NET Core scraper (built)
    feature-engineering/ Phase 4 — 47-dim state vector + Postgres (built)
    bff/                 Phase 7 — scaffold only
  operator/              Phase 6 — scaffold only
  ml/                    Phase 5 — scaffold only
  dashboard/             Phase 7 — scaffold only
  shared/
    AFIE.Contracts/      Shared DTOs (MetricEvent)
infra/
  gitops/                ArgoCD apps, CRDs, plain manifests
  terraform/             Phase 8 — Azure IaC (scaffold)
  benchmarks/            Phase 9 — experiment workloads
tests/
  AFIE.Telemetry.Tests/            xUnit tests for Phase 3
  AFIE.FeatureEngineering.Tests/   xUnit tests for Phase 4
  AFIE.Operator.Tests/             Phase 6 — scaffold only
experiments/             Local telemetry sinks + Phase 9 analysis
scripts/                 Phase 2 bootstrap
paper/                   IEEE draft
```