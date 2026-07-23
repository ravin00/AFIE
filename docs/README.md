# AFIE Documentation

AFIE — Autonomous FinOps Intelligence Engine for Kubernetes. A closed-loop
system that scrapes cluster telemetry, learns a resource-adjustment policy
with reinforcement learning, validates each recommendation against a policy
engine, and applies changes through GitOps pull requests.

## Start here

| Doc | What it covers |
| --- | --- |
| [architecture.md](architecture.md) | System overview, components, data flow, tech stack |
| [telemetry-service.md](telemetry-service.md) | Phase 3 service — the only fully-built component today |
| [infrastructure.md](infrastructure.md) | KIND cluster, Prometheus/Grafana, ArgoCD |
| [development.md](development.md) | Build, test, and run locally |
| [roadmap.md](roadmap.md) | Planned scope for Phases 4–9 |
| [doc.md](doc.md) | Local dev credentials cheatsheet |

## Current status

- **Phase 1 (env + monorepo):** complete
- **Phase 2 (KIND + Prometheus/Grafana + ArgoCD):** complete
- **Phase 3 (telemetry pipeline):** complete — see [telemetry-service.md](telemetry-service.md)
- **Phases 4–9:** not started — see [roadmap.md](roadmap.md)

## Repository layout

```
src/
  api/
    telemetry/           Phase 3 — ASP.NET Core scraper (built)
    feature-engineering/ Phase 4 — scaffold only
    bff/                 Phase 7 — scaffold only
  operator/              Phase 6 — scaffold only
  ml/                    Phase 5 — scaffold only
  dashboard/             Phase 7 — scaffold only
infra/
  gitops/                ArgoCD apps, CRDs, plain manifests
  terraform/             Phase 8 — Azure IaC (scaffold)
  benchmarks/            Phase 9 — experiment workloads
tests/
  AFIE.Telemetry.Tests/  xUnit tests for Phase 3
  AFIE.Operator.Tests/   Phase 6 — scaffold only
experiments/             Local telemetry sinks + Phase 9 analysis
scripts/                 Phase 2 bootstrap
paper/                   IEEE draft
```
