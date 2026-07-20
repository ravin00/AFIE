# AFIE — local dev access notes

> Do **not** commit real passwords to this repo. Retrieve/set credentials at
> runtime using the commands below.

## ArgoCD (http://localhost:8080, user: `admin`)

Get the auto-generated initial admin password:

```bash
kubectl -n argocd get secret argocd-initial-admin-secret \
  -o jsonpath="{.data.password}" | base64 -d
```

Change it after first login (`argocd account update-password`) and delete the
initial secret.

## Grafana (http://localhost:3000)

Default login is `admin` / `prom-operator`. Change the password on first login
via the Grafana UI (Profile → Change password).

## Setup

`scripts/setup-phase2.sh` stands up the full Phase 2 environment and prints the
ArgoCD admin password at the end.
