# Development Guide

## 1. Prerequisites

Everything from Phase 1 of the workflow doc must be installed and on
`$PATH`:

| Tool | Min version | Check |
| --- | --- | --- |
| Docker Desktop | 4.x | `docker --version` |
| .NET SDK | 8.0 | `dotnet --version` |
| Node | 20 | `node --version` |
| pnpm | 9 | `pnpm --version` |
| Python | 3.11 | `python --version` |
| kubectl | 1.29+ | `kubectl version --client` |
| helm | 3.14+ | `helm version` |
| kind | 0.22+ | `kind version` |
| argocd (CLI) | 2.x | `argocd version --client` |
| flux (CLI, optional) | 2.x | `flux --version` |
| terraform | 1.7+ | `terraform --version` |
| Azure CLI | 2.60+ | `az --version` |

## 2. First-time setup

```bash
git clone https://github.com/<your-org>/afie.git
cd afie
scripts/setup-phase2.sh    # creates KIND, installs Prometheus/Grafana/ArgoCD
```

The script prints the Argo CD admin password at the end — copy it, then
delete the initial secret (see [doc.md](doc.md)).

Verify:

```bash
kubectl get nodes                          # 1 Ready node
kubectl get ns                             # 6 AFIE namespaces + argocd + monitoring
kubectl -n monitoring get pods             # kube-prometheus-stack running
kubectl -n argocd get pods                 # argocd running
```

## 3. Repository conventions

- **Branches.** `feat/phaseN-<slug>` for phase work,
  `fix/<short-slug>` for bug fixes. Never commit directly to `main`.
- **Commits.** Conventional Commits style (`feat:`, `fix:`, `chore:`,
  `docs:`). See `git log` for the tone.
- **Secrets.** Never commit real credentials. `appsettings.Development.json`
  is gitignored; use environment variables (`Section__Key=…`) or a local
  Kubernetes secret for anything sensitive.
- **Manifests.** Plain YAML under `infra/gitops/manifests/`. No Kustomize
  or Helm templating at the leaf (see
  [architecture.md §8](architecture.md#8-gitops-backend-abstraction)).

## 4. Building and testing (.NET)

The solution file is [`AFIE.slnx`](../AFIE.slnx) at the repo root.

```bash
dotnet build AFIE.slnx
dotnet test  AFIE.slnx
```

Per-project examples:

```bash
# Telemetry service
dotnet run --project src/api/telemetry/AFIE.Telemetry.csproj

# Telemetry tests only
dotnet test tests/AFIE.Telemetry.Tests/AFIE.Telemetry.Tests.csproj
```

For a running service you'll want Prometheus reachable — either
port-forward it (`kubectl -n monitoring port-forward svc/kube-prometheus-stack-prometheus 9090:9090`)
and set `Telemetry__PrometheusUrl=http://localhost:9090`, or run the
service in-cluster (§6).

## 5. Building and testing (Python — Phase 5)

Not yet implemented; the workflow doc's target layout:

```bash
cd src/ml
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
pytest
```

## 6. Deploying a service to KIND

Standard loop for any .NET service:

```bash
# 1. Build the image, tagged for local use
docker build -t afie-telemetry:dev -f src/api/telemetry/Dockerfile .

# 2. Side-load into KIND (no registry needed)
kind load docker-image afie-telemetry:dev --name afie-dev

# 3. Apply the manifest
kubectl apply -f infra/gitops/manifests/telemetry-deployment.yaml

# 4. Wait, then verify
kubectl -n afie-system rollout status deploy/afie-telemetry
kubectl -n afie-system logs -l app=afie-telemetry --tail=50
```

The `imagePullPolicy: IfNotPresent` on the manifest is what lets the
side-loaded image be picked up — do not change it to `Always` for local
work.

## 7. Common commands cheatsheet

```bash
# Recreate the cluster from scratch
kind delete cluster --name afie-dev && scripts/setup-phase2.sh

# Reload a rebuilt image and roll the pod
docker build -t afie-telemetry:dev -f src/api/telemetry/Dockerfile . \
  && kind load docker-image afie-telemetry:dev --name afie-dev \
  && kubectl -n afie-system rollout restart deploy/afie-telemetry

# Tail health across the loop
watch -n 2 'kubectl -n afie-system port-forward svc/afie-telemetry 8080:8080 & \
            sleep 1 && curl -s localhost:8080/health | jq'

# See what Prometheus is actually returning for a query
kubectl -n monitoring port-forward svc/kube-prometheus-stack-prometheus 9090:9090 &
open "http://localhost:9090/graph?g0.expr=up"
```

## 8. Debugging tips

- **No events on the sink file.** Check `/health` — `prometheusReachable`
  will tell you if the scraper can't reach Prometheus. If it can, check
  the pod log for the query name that returned zero results, then run
  the same PromQL in the Prometheus UI to see what labels Prometheus
  actually emits for that metric on your cluster.
- **`kind load` succeeds but pod still uses the old image.** KIND caches
  by image ID, not tag — a rebuild with the same tag but no content
  changes is a no-op. Confirm with `kubectl -n afie-system describe pod`
  and look at `Image ID`.
- **ArgoCD stuck `OutOfSync`.** Manual sync is on by design; hit "Sync"
  in the UI or `argocd app sync afie`. Auto-sync will land in Phase 8.
- **Tests pass locally but the pod crash-loops.** Almost always a path
  or config issue — the container runs as non-root and the working
  directory is `/app`; `OutputPath` in `appsettings.json` is relative
  and would resolve inside `/app`. Prefer overriding
  `Telemetry__OutputPath=/app/data` in the manifest (already done).

## 9. Where to add a new service

Match the telemetry service layout:

```
src/api/<name>/
  <Name>.csproj
  Dockerfile
  Program.cs
  appsettings.json
  Clients/  Models/  Services/  Health/  ...
infra/gitops/manifests/<name>-deployment.yaml
tests/AFIE.<Name>.Tests/
```

Register in the solution:

```bash
dotnet sln AFIE.slnx add src/api/<name>/AFIE.<Name>.csproj
dotnet sln AFIE.slnx add tests/AFIE.<Name>.Tests/AFIE.<Name>.Tests.csproj
```

Add a section to [architecture.md §3](architecture.md#3-components) and a
component doc under `docs/` if the surface is non-trivial.
