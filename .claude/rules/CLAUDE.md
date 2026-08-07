# AFIE — Project Rules

Applies to every file in this repo unless a more specific rule file overrides.

# Project shape

## Identify the correct service
- Multi-service monorepo. Before generating code, confirm which service folder under `src/api/<service>/`, `src/ml/`, `src/operator/`, `src/dashboard/`, or `src/shared/` you're targeting.
- Tests live in `tests/AFIE.<Service>.Tests/`, mirroring the source folder tree.
- Runtime artifacts (JSONL streams, offset state, DBs) go under `experiments/` and are gitignored — never commit contents.
- Infra changes go under `infra/terraform/` or `infra/gitops/`. Nothing else touches those folders.

## Style of coding
- Prefer simple, readable code over compact, dense code.
- Reuse existing helpers before writing new ones — search first.
- Follow the patterns already in the codebase (`Options` POCOs, `BackgroundService` consumers, health-check contract) instead of inventing new ones.
- Use meaningful names. No single-letter vars outside tight loops.
- Functions ≤ 50 lines. Classes ≤ 300 lines. Split if larger.
- No comments unless the *why* is non-obvious. Names carry the *what*.
- No emojis in code or commits.

## Error handling
- Handle errors and edge cases. Don't `catch { }` silently — at least log.
- Validate inputs at construction (`ArgumentNullException.ThrowIfNull`, range checks). Fail fast.
- Every `CancellationToken` flow persists critical state (offsets, cursors) in a `finally` block.
- Trust internal code; validate only at system boundaries (user input, external APIs, config binding).

# .NET / C#

## Project setup
- `.net8.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` on every csproj.
- `sealed` by default for concrete classes.
- No `!` to silence nullability — fix the type or add a guard.
- No `ConfigureAwait(false)` — this is ASP.NET Core, no captured context.

## Configuration
- Config binds to a POCO under `Models/<X>Options.cs`. Inject `IOptions<T>` — never `IConfiguration`.
- POCO property names must match `appsettings.json` keys exactly (binding is case-insensitive, but drift causes silent nulls).
- Every options field with an invariant gets constructor validation and a rejection test.
- Environment overrides use `.NET` double-underscore convention: `FeatureEngineering__WindowCapacity=100`.

## Records & DTOs
- Records for DTOs. Classes for services with dependencies.
- Records with invariants use backing field + validating `init` so `with` and object-initializer paths validate too (see `StateVector`).
- Shared DTOs go in `src/shared/AFIE.Contracts/`. Nothing service-specific there.

## Health checks
- Every service exposes `/health` via `MapHealthChecks`.
- Contract (mirror `TelemetryHealthCheck`):
  1. Build a `Dictionary<string, object>` of state signals.
  2. Last-activity timestamp `null` → `Degraded("No X yet")`.
  3. Stale beyond configured threshold OR dependency flag false → `Degraded("stale or dependency unreachable")`.
  4. Else → `Healthy("OK", data)`.
- Thresholds come from `IOptions<T>`. No magic constants.

# Testing
- xUnit, one test project per service.
- Naming: `Method_State_ExpectedResult` (e.g. `Ctor_InvalidCapacity_Throws`).
- Deterministic and parallel-safe. Inject a clock if you need fixed time.
- No mocked databases — Testcontainers Postgres for anything persistent.
- New options invariant → constructor rejection test.
- New consumer/reader path → cancellation test asserting state persisted in `finally`.

# Docker
- Multi-stage: `mcr.microsoft.com/dotnet/sdk:8.0` build, `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` runtime.
- End runtime stage with `USER app` **before** `ENV` / `EXPOSE` / `ENTRYPOINT`.
- Any dir the app writes to must be `chown app:app` in the same `RUN` layer that created it.
- No extra packages in the runtime image.
- No `latest` tags in prod. Pin to `sha-<short>`.

# Git
- Feature branches: `feat/<phase>-<scope>`.
- Commit prefixes: `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`. Imperative, lowercase after prefix.
- Rebase on pull. If a rebase rewrites pushed commits, `git push --force-with-lease` (never plain `--force`).
- Never `--no-verify`. Fix the hook.
- Never commit `bin/`, `obj/`, `.terraform/`, `*.tfstate*`, secrets, or `experiments/` contents.
- Before `git add .`, review with `git status` — filenames that look innocuous can hide secrets.

# CI/CD (target)
- GitHub Actions, one workflow per service, path-filtered on `src/api/<service>/**` + `src/shared/**`.
- Steps: restore → build → test → `dotnet list package --vulnerable` → Trivy on image → build multi-arch image → push `sha-<short>` to registry.
- On `main`, CI bumps image tag in `infra/gitops/**/values.yaml`. Argo reconciles.
- Terraform: `plan` on PR, `apply` on `main` with environment approval. Never apply from a workstation.
- Promotion dev → staging → prod via PR moving image digest up. No auto-promote to prod.

# Terraform / GitOps
- State is remote. Never commit `*.tfstate`.
- Modules: `infra/terraform/modules/`. Root configs: `infra/terraform/envs/<env>/`.
- Argo Applications live in `infra/gitops/argocd/`. Nowhere else.
- Namespaces defined once, in `infra/gitops/namespaces.yaml`.
- Prod manifests pin images by digest. Dev may use tag.

# Python (when it lands)
- One dependency manager project-wide (`uv` or Poetry). Don't mix.
- `ruff` + `mypy --strict`. `pytest` for tests.
- Same Docker rules: Alpine or `python:3.12-slim`, non-root, pinned versions.

# Ask before doing
- Adding a top-level folder.
- New runtime dependency (broker, cache, external service).
- Changing the health-check contract.
- Touching `infra/terraform/` or `infra/gitops/argocd/`.
- Force-pushing a branch anyone else has fetched.
- Committing anything under `experiments/` beyond `.gitkeep`.
