# AFIE Documentation

AFIE — Autonomous FinOps Intelligence Engine for Kubernetes. A closed-loop
system that scrapes cluster telemetry, learns a resource-adjustment policy
with reinforcement learning, validates each recommendation against a policy
engine, and applies changes through GitOps pull requests.

This directory contains the engineering documentation. The phase-by-phase
plan is `AFIE_Workflow_v4.docx`, kept outside the repository by its owner.

## Start here

| Doc | What it covers |
| --- | --- |
| [architecture.md](architecture.md) | System overview, components, data flow, tech stack |
| [telemetry-service.md](telemetry-service.md) | Phase 3 service — Prometheus scraping, MetricEvent JSONL sink |
| [feature-engineering-service.md](feature-engineering-service.md) | Phase 4 service — sliding window, 55-dim state vector, `/state` endpoint, Postgres persistence |
| [infrastructure.md](infrastructure.md) | KIND cluster, Prometheus/Grafana, ArgoCD, Postgres |
| [development.md](development.md) | Build, test, and run locally |
| [roadmap.md](roadmap.md) | Open work and Phases 5–9 |
| [doc.md](doc.md) | Local dev credentials cheatsheet |

## Current status

As of 25 November 2026, on `main`.

- **Phase 1 (env + monorepo):** complete
- **Phase 2 (KIND + Prometheus/Grafana + Argo CD):** built; Argo CD version pin and image tags open
- **Phase 3 (telemetry pipeline):** built; hardening and fixes open — see [telemetry-service.md](telemetry-service.md)
- **Phase 4 (feature engineering + Postgres):** built, 55 dims; contract test and forecast features open — see [feature-engineering-service.md](feature-engineering-service.md)
- **Phase 5 (RL agent):** in progress — environments, action decoder, reward, offline PPO trainer
- **Phases 6–9:** not started — see [roadmap.md](roadmap.md)

## Repository layout

```
src/
  api/
    telemetry/           Phase 3 — ASP.NET Core scraper (built)
    feature-engineering/ Phase 4 — 55-dim state vector + Postgres (built)
    bff/                 Phase 7 — scaffold only
  ml/                    Phase 5 — Python package `afie_ml` (in progress)
  operator/              Phase 6 — scaffold only
  dashboard/             Phase 7 — scaffold only
  shared/
    AFIE.Contracts/      Shared DTOs (MetricEvent)
infra/
  gitops/                Argo CD apps, manifests, overlays, Helm values
  terraform/             Phase 8 — Azure IaC (scaffold)
  benchmarks/            Phase 9 — experiment workloads (scaffold)
tests/
  AFIE.Telemetry.Tests/            xUnit tests for Phase 3
  AFIE.FeatureEngineering.Tests/   xUnit tests for Phase 4
  AFIE.Operator.Tests/             Phase 6 — scaffold only
experiments/             Local telemetry sinks + Phase 9 analysis
scripts/                 Cluster bootstrap, secret sealing
```
