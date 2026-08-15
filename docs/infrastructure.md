# Infrastructure

Everything runs on a single local KIND cluster today. The Azure/Terraform
side (Phase 8) is scaffolded under `infra/terraform/` but not yet
provisioned.

## 1. Cluster — KIND

- Cluster name: `afie-dev`
- Single control-plane node, one worker (default KIND topology)
- Config: [`scripts/kind-config.yaml`](../scripts/kind-config.yaml)
- Bootstrap: [`scripts/setup-phase2.sh`](../scripts/setup-phase2.sh) —
  creates the cluster, installs kube-prometheus-stack, installs Argo CD,
  applies namespaces, and prints the ArgoCD admin password.

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
| `argocd` | Argo CD server + controllers |
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
- **Alertmanager.** Default routing; not used by AFIE — the operator has
  its own SLO check for rollback decisions.

Port-forwards:

```bash
kubectl -n monitoring port-forward svc/kube-prometheus-stack-grafana 3000:80
kubectl -n monitoring port-forward svc/kube-prometheus-stack-prometheus 9090:9090
```

## 4. GitOps — Argo CD

Argo CD is the default GitOps controller. Flux is a supported adapter
(see [architecture.md §8](architecture.md#8-gitops-backend-abstraction))
but is not installed on the dev cluster.

- Server exposed via NodePort:
  [`argocd-server-nodeport.yaml`](../infra/gitops/argocd/argocd-server-nodeport.yaml).
- Root application:
  [`app-afie.yaml`](../infra/gitops/argocd/app-afie.yaml) — points at
  `infra/gitops/manifests` in this repo; sync policy is manual for now
  so demos are deterministic.

Access:

```bash
kubectl -n argocd port-forward svc/argocd-server 8080:443
# then open https://localhost:8080 — accept the self-signed cert
```

Admin password retrieval is in [doc.md](doc.md).

## 5. Manifests layout

```
infra/gitops/
  namespaces.yaml           Cluster namespaces
  crds/                     Phase 6 — ResourceRecommendation CRD (scaffold)
  manifests/                Plain rendered manifests — the operator (Phase 6) writes here
    telemetry-deployment.yaml
    nginx-test.yaml
    afie-telemetry-data-pvc.yaml          # shared PVC (telemetry RW, FE RO)
    afie-fe-state-pvc.yaml                # FE consumer offset
    postgres-service.yaml
    postgres-statefulset.yaml             # afie-postgres, 5Gi PVC template
    feature-engineering-deployment.yaml   # requires afie-postgres Secret
    feature-engineering-secret.example    # template only — see
                                          # feature-engineering-service.md §4.4
                                          # for the authoritative bootstrap
  monitoring/               kube-prometheus-stack Helm values
  argocd/                   ArgoCD Application + NodePort service
```

The operator (Phase 6) is designed to open PRs against
`infra/gitops/manifests/`; keeping manifests plain (no Kustomize / Helm
templating at the leaf) makes the diff in each PR trivially reviewable.

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
| Argo CD | https://localhost:8080 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Telemetry service | http://localhost:8080/health (mutually exclusive with Argo CD — use different port) |

## 8. Phase 8 (planned) — Azure

Terraform modules live under [`infra/terraform/`](../infra/terraform/).
The Phase 8 provisioning surface is: Event Hub namespace + hub, Cosmos DB
(serverless), Key Vault, optional Azure ML workspace, optional ACR,
Static Web Apps for the dashboard. AKS is deliberately absent — the
student subscription blocks it, and every experiment runs against the
same manifests on KIND. See [roadmap.md](roadmap.md).

## 9. Secrets

Phase 4's feature-engineering service reads its Postgres connection
string from a Kubernetes `Secret`. There is no committed secret file —
the bootstrap is a single command run once per cluster:

```bash
PG_PASSWORD="$(openssl rand -hex 12)"
kubectl -n afie-system create secret generic afie-postgres \
  --from-literal=POSTGRES_DB=afie \
  --from-literal=POSTGRES_USER=afie \
  --from-literal=POSTGRES_PASSWORD="$PG_PASSWORD" \
  --from-literal=CONNECTION_STRING="Host=afie-postgres;Port=5432;Database=afie;Username=afie;Password=$PG_PASSWORD"
```

The same `Secret` supplies `POSTGRES_*` to the StatefulSet via `envFrom`
and `CONNECTION_STRING` to the FE deployment via `secretKeyRef` — one
command, one rotation surface. See
[feature-engineering-service.md §4.4](feature-engineering-service.md#44-secret-bootstrap)
for rotation and the Phase 8 Azure Key Vault CSI plan.
