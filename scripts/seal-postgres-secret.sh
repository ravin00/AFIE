#!/usr/bin/env bash
# Re-seal the Postgres Secret against THIS cluster's sealed-secrets controller.
# SealedSecrets are cluster-key-specific: the committed ciphertext only decrypts
# on the controller that produced it, so a fresh cluster needs a fresh seal.
#
# Secrets come from the environment — never commit plaintext. Example:
#   POSTGRES_USER=afie POSTGRES_PASSWORD='s3cr3t' POSTGRES_DB=afie \
#     scripts/seal-postgres-secret.sh
#
# Requires: kubectl, kubeseal, a running sealed-secrets controller.
set -euo pipefail

: "${POSTGRES_USER:?set POSTGRES_USER}"
: "${POSTGRES_PASSWORD:?set POSTGRES_PASSWORD}"
: "${POSTGRES_DB:?set POSTGRES_DB}"
POSTGRES_HOST="${POSTGRES_HOST:-afie-postgres}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"
CONTROLLER_NS="${CONTROLLER_NS:-sealed-secrets}"
CONTROLLER_NAME="${CONTROLLER_NAME:-sealed-secrets}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${REPO_ROOT}/infra/gitops/manifests/afie-postgres.sealed.yaml"

CONNECTION_STRING="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

kubectl create secret generic afie-postgres \
  --namespace afie-system \
  --from-literal=POSTGRES_USER="${POSTGRES_USER}" \
  --from-literal=POSTGRES_PASSWORD="${POSTGRES_PASSWORD}" \
  --from-literal=POSTGRES_DB="${POSTGRES_DB}" \
  --from-literal=CONNECTION_STRING="${CONNECTION_STRING}" \
  --dry-run=client -o yaml \
| kubeseal \
    --controller-namespace "${CONTROLLER_NS}" \
    --controller-name "${CONTROLLER_NAME}" \
    --format yaml \
> "${OUT}"

echo "Sealed -> ${OUT}"
echo "Commit it, then let ArgoCD sync (or: kubectl apply -f ${OUT})."