from typing import Final

STATE_VECTOR_DIM: Final[int] = 47

FEATURE_NAMES: Final[tuple[str, ...]] = (
    # 0-8 — CPU utilisation (CpuUsageRate / CpuLimit), {5m,15m,1h} x {P50,P95,P99}
    "CPU util P50 (5m)",
    "CPU util P95 (5m)",
    "CPU util P99 (5m)",
    "CPU util P50 (15m)",
    "CPU util P95 (15m)",
    "CPU util P99 (15m)",
    "CPU util P50 (1h)",
    "CPU util P95 (1h)",
    "CPU util P99 (1h)",
    # 9-17 — Memory utilisation (MemoryBytes / MemLimit), same layout
    "Memory util P50 (5m)",
    "Memory util P95 (5m)",
    "Memory util P99 (5m)",
    "Memory util P50 (15m)",
    "Memory util P95 (15m)",
    "Memory util P99 (15m)",
    "Memory util P50 (1h)",
    "Memory util P95 (1h)",
    "Memory util P99 (1h)",
    # 18-23 — Application signals (averaged over last 5m of samples)
    "Request rate (req/s, 5m)",
    "Error rate (5m)",
    "Latency P50 (5m)",
    "Latency P95 (5m)",
    "Latency P99 (5m)",
    "App signal (reserved)",
    # 24-26 — Node pressure
    "Node CPU pressure",
    "Node memory pressure",
    "Eviction proximity",
    # 27-29 — Cost
    "Hourly cost estimate",
    "Cost 7-day trend (placeholder)",
    "Budget fraction",
    # 30-34 — Temporal (cyclic encoding)
    "Hour of day (sin)",
    "Hour of day (cos)",
    "Day of week (sin)",
    "Day of week (cos)",
    "Days since first sample",
    # 35-37 — Deployment context (all placeholders until Phase 6)
    "Replica count (placeholder)",
    "HPA target utilisation (placeholder)",
    "Rolling update in progress (placeholder)",
    # 38-46 — Action history, most-recent-first
    "Last action: cost delta",
    "Last action: SLO delta",
    "Last action: minutes since",
    "2nd-last action: cost delta",
    "2nd-last action: SLO delta",
    "2nd-last action: minutes since",
    "3rd-last action: cost delta",
    "3rd-last action: SLO delta",
    "3rd-last action: minutes since",
)

if len(FEATURE_NAMES) != STATE_VECTOR_DIM:
    raise AssertionError(
        f"FEATURE_NAMES has {len(FEATURE_NAMES)} entries, "
        f"expected {STATE_VECTOR_DIM}"
    )
    
    
"""Human-readable labels for the 47-dimensional AFIE state vector.

`FEATURE_NAMES[i]` describes exactly what the C# feature-engineering service
writes into dimension `i` of the vector returned by `GET /state/{workload}`.
The SHAP explainer uses these labels to turn a raw attribution
("dimension 7 drove this action") into a sentence an SRE can read
("CPU util P95 (1h) drove this action").

Ordering is verified against the merged feature-group implementations under
src/api/feature-engineering/Features/, NOT just the architecture docs. Every
group fills its slice window-outer / percentile-inner. If the C# ordering
ever changes, these labels must change in lockstep or SHAP output silently
mislabels — the import-time guard below catches a length drift, but a
reordering within the 47 is a semantic change no assertion can catch, so
treat this file as coupled to the feature groups.
"""