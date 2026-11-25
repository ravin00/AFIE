# Infrastructure

Everything runs on a single local KIND cluster today. The Azure/Terraform
side (Phase 8) is scaffolded under `infra/terraform/` but not yet
provisioned.

## 1. Cluster — KIND

- Cluster name: `afie-dev`
- Single control-plane node, with host ports 3000, 9090 and 8080 mapped to
  NodePorts
- Config: [`scripts/kind-config.yaml`](../scripts/kind-config.yaml)
- Bootstrap: [`scripts/setup-phase2.sh`](../scripts/setup-phase2.sh) —
  creates the cluster and namespaces, installs kube-prometheus-stack and Argo
  CD, and applies `app-afie.yaml` and the nginx test workload.

Gaps in the script: it installs the Argo CD `stable` manifest (no pinned
version) and does not apply `appprojects.yaml` (which `app-afie.yaml` depends
on), `app-monitoring.yaml`, `app-opencost.yaml` or a sealed-secrets controller.
Apply the projects after it runs:

```bash
kubectl apply -f infra/gitops/argocd/appprojects.yaml
```

Reset the cluster:

```bash
kind delete cluster --name afie-dev
scripts/setup-phase2.sh
```

## 2. Namespaces

Defined in [`infra/gitops/namespaces.yaml`](../infra/gitops/namespaces.yaml):

| Namespace | Contents |
| --- | --- |
| `monitoring` | kube-prometheus-stack (Prometheus, Alertmanager, Grafana) |
| `afie-system` | AFIE control-plane pods — telemetry, feature-engineering, `afie-postgres` StatefulSet, operator (Phase 6), BFF (Phase 7) |
| `argocd` | Argo CD server + controllers (created by the setup script, not in this file) |
| `baseline-manual` | Baseline workload — human-tuned resource requests (Phase 9 experiment) |
| `baseline-vpa` | Baseline — Vertical Pod Autoscaler managing requests |
| `baseline-threshold` | Baseline — simple utilisation-threshold controller |
| `baseline-afie` | The AFIE-managed workload |

The four `baseline-*` namespaces exist so Phase 9 can run the same
synthetic workload under four regimes and compare cost + SLO outcomes.

## 3. Observability — kube-prometheus-stack

Installed via Helm in the `monitoring` namespace. Values overrides live at
[`infra/gitops/monitoring/kube-prometheus-stack.values.yaml`](../infra/gitops/monitoring/kube-prometheus-stack.values.yaml).

- **Prometheus.** 7-day retention. Scrapes all pods with the standard
  `ServiceMonitor` / `PodMonitor` discovery. Reachable in-cluster at
  `http://kube-prometheus-stack-prometheus.monitoring.svc:9090`.
- **Grafana.** Dashboards imported by ID: `6417` (cluster overview),
  `15757` (pods). Access notes in [doc.md](doc.md).
- **Alertmanager.** Default routing; not used by AFIE. The planned operator
  does its own SLO check for rollback decisions.

Local access is by NodePort (Grafana `http://localhost:3000`, Prometheus
`http://localhost:9090`).

## 4. GitOps — Argo CD

Argo CD is the default GitOps controller. Flux is a supported adapter
(see [architecture.md §8](architecture.md#8-gitops-backend-abstraction))
but is not installed on the dev cluster.

- Server exposed via NodePort at `http://localhost:8080`
  ([`argocd-server-nodeport.yaml`](../infra/gitops/argocd/argocd-server-nodeport.yaml));
  the setup script runs it in insecure (HTTP) mode.
- AppProjects ([`appprojects.yaml`](../infra/gitops/argocd/appprojects.yaml)):
  `afie` and `platform`.
- Applications: [`app-afie.yaml`](../infra/gitops/argocd/app-afie.yaml)
  (`infra/gitops/manifests` on `main`, automated sync with prune and
  self-heal),
  [`app-monitoring.yaml`](../infra/gitops/argocd/app-monitoring.yaml) and
  [`app-opencost.yaml`](../infra/gitops/argocd/app-opencost.yaml).

Admin password retrieval is in [doc.md](doc.md).

## 5. Manifests layout

```
infra/gitops/
  namespaces.yaml           Cluster namespaces
  argocd/                   AppProjects, Applications, NodePort service
  crds/                     Phase 6 — CRDs (scaffold)
  manifests/                afie-system manifests + kustomization.yaml:
                            telemetry, feature engineering, Postgres, PVCs,
                            sealed secret, NetworkPolicies, PDBs,
                            ServiceMonitor, resource defaults, nginx test
  monitoring/               kube-prometheus-stack Helm values
  overlays/dev/             Includes the base only; no image tags pinned yet
```

The operator (Phase 6) is designed to open PRs against
`infra/gitops/manifests/`; workload manifests stay plain so each PR diff is
easy to review.

## 6. Test workload

[`infra/gitops/manifests/nginx-test.yaml`](../infra/gitops/manifests/nginx-test.yaml)
deploys 2 nginx pods to `afie-system`. Purpose: end-to-end check that a
new pod appears in the pods dashboard (Grafana ID 15757) and that
Prometheus is producing the labels the telemetry service expects.

## 7. Local access cheatsheet

See [doc.md](doc.md) for credentials. Everything can run behind
`kubectl port-forward`; no ingress is provisioned on KIND.

| Service | Local URL |
| --- | --- |
| Argo CD | http://localhost:8080 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Telemetry service | port-forward to a free port, e.g. 8082 (8080 is Argo CD) |
| Feature engineering | port-forward to 8081 |

## 8. Phase 8 (planned) — Azure

Terraform modules live under [`infra/terraform/`](../infra/terraform/).
The Phase 8 provisioning surface is: Event Hub namespace + hub, Cosmos DB
(serverless), Key Vault, optional Azure ML workspace, optional ACR,
Static Web Apps for the dashboard. AKS is deliberately absent — the
student subscription blocks it, and every experiment runs against the
same manifests on KIND. See [roadmap.md](roadmap.md).

## 9. Secrets

The Postgres credentials are committed only as the encrypted
`afie-postgres.sealed.yaml`. One `Secret` supplies `POSTGRES_*` to the
StatefulSet and `CONNECTION_STRING` to feature engineering. Re-sealing for a new
cluster and rotation are in
[feature-engineering-service.md §4.4](feature-engineering-service.md#44-secret-bootstrap).
