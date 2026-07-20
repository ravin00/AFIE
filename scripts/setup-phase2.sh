#!/usr/bin/env bash
#
# setup-phase2.sh — reproduce the AFIE Phase 2 environment from scratch.
#
# Phase 2 = Local Kubernetes (KIND) + Prometheus/Grafana + GitOps (ArgoCD).
# Running this on a clean machine gives you the exact Phase 2 deliverables:
#   - KIND cluster "afie-dev" (1 node Ready)
#   - 6 project namespaces
#   - kube-prometheus-stack (Prometheus + Grafana + Alertmanager) on NodePorts
#   - ArgoCD installed and reconciling infra/gitops/manifests
#   - nginx smoke-test workload
#
# Endpoints (via scripts/kind-config.yaml host port mappings):
#   Grafana     http://localhost:3000   (admin / prom-operator)
#   Prometheus  http://localhost:9090
#   ArgoCD      http://localhost:8080   (admin / see command printed at the end)
#
# Requires: docker, kind, kubectl, helm (all installed in Phase 1).
set -euo pipefail

CLUSTER_NAME="afie-dev"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GITOPS="${REPO_ROOT}/infra/gitops"
ARGOCD_VERSION="stable"

echo "==> [1/6] KIND cluster"
if kind get clusters | grep -qx "${CLUSTER_NAME}"; then
  echo "    cluster '${CLUSTER_NAME}' already exists — skipping create"
else
  kind create cluster --name "${CLUSTER_NAME}" --config "${REPO_ROOT}/scripts/kind-config.yaml"
fi
kubectl cluster-info --context "kind-${CLUSTER_NAME}"
kubectl get nodes

echo "==> [2/6] Namespaces"
kubectl apply -f "${GITOPS}/namespaces.yaml"

echo "==> [3/6] Prometheus + Grafana (kube-prometheus-stack via Helm)"
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts >/dev/null 2>&1 || true
helm repo update >/dev/null
helm upgrade --install kube-prometheus-stack prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  -f "${GITOPS}/monitoring/kube-prometheus-stack.values.yaml" \
  --wait --timeout 10m

echo "==> [4/6] ArgoCD"
kubectl create namespace argocd --dry-run=client -o yaml | kubectl apply -f -
kubectl apply -n argocd \
  -f "https://raw.githubusercontent.com/argoproj/argo-cd/${ARGOCD_VERSION}/manifests/install.yaml"
echo "    waiting for ArgoCD pods..."
kubectl wait --for=condition=ready pod --all -n argocd --timeout=180s
# Serve the UI over plain HTTP so the NodePort works, then expose via NodePort.
kubectl -n argocd patch configmap argocd-cmd-params-cm --type merge \
  -p '{"data":{"server.insecure":"true"}}'
kubectl -n argocd rollout restart deployment argocd-server
kubectl -n argocd rollout status deployment argocd-server --timeout=120s
kubectl apply -f "${GITOPS}/argocd/argocd-server-nodeport.yaml"

echo "==> [5/6] Wire GitOps (ArgoCD Application) + deploy test workload"
kubectl apply -f "${GITOPS}/argocd/app-afie.yaml"
# Also apply the smoke-test directly so it's up even before the first ArgoCD sync.
kubectl apply -f "${GITOPS}/manifests/nginx-test.yaml"

echo "==> [6/6] Verify"
kubectl -n afie-system rollout status deployment/nginx --timeout=120s
kubectl get pods -n monitoring
kubectl get pods -n afie-system

echo
echo "Phase 2 environment is up."
echo "  Grafana     http://localhost:3000   (admin / prom-operator — change on first login)"
echo "  Prometheus  http://localhost:9090"
echo "  ArgoCD      http://localhost:8080   (user: admin)"
echo -n "  ArgoCD admin password: "
kubectl -n argocd get secret argocd-initial-admin-secret \
  -o jsonpath="{.data.password}" | base64 -d; echo
echo "  (In Grafana, import dashboards 6417 and 15757 to see cluster + pod metrics.)"
