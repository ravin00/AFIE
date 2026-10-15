"""Reward heuristic shared by the offline and live environments.

Maps (observation, decoded action) to the seven reward terms via
:func:`compute_reward`. Training-time approximation of real cloud economics;
replaced by measured cost/SLO/egress deltas when the operator is live.

Policy-safety is enforced here (not in the pure decoder):
- ``spot`` lifecycle on a stateful workload                        → 1 violation
- ``cheapest_global`` region move on a stateful workload           → 1 violation
- Shrinking CPU or memory past the headroom limit                  → 1 violation each
"""

from __future__ import annotations

from typing import Final

import numpy as np
import numpy.typing as npt

from afie_ml.actions import (
    COMMITMENT_NONE,
    COMMITMENT_SAVINGS_1YR,
    COMMITMENT_SAVINGS_3YR,
    LIFECYCLE_ON_DEMAND,
    LIFECYCLE_RESERVED_1YR,
    LIFECYCLE_SPOT,
    REGION_CHEAPER_SAME_CONTINENT,
    REGION_CHEAPEST_GLOBAL,
    REGION_STAY,
    SCHEDULE_ALWAYS_ON,
    SCHEDULE_OFF_HOURS_SCALEDOWN,
    STORAGE_ARCHIVE,
    STORAGE_COOL,
    STORAGE_HOT,
    DecodedAction,
)
from afie_ml.feature_names import FEATURE_NAMES
from afie_ml.reward import compute_reward

CPU_UTIL_DIM: Final[int] = 7       # "CPU util P95 (1h)"
MEM_UTIL_DIM: Final[int] = 16      # "Memory util P95 (1h)"
STATEFUL_DIM: Final[int] = 52      # "Workload is stateful"
HEADROOM_LIMIT: Final[float] = 0.9    # >this after a shrink = <10% headroom
SLO_HEALTHY_UTIL: Final[float] = 0.7  # at/under this = full SLO compliance
STATEFUL_THRESHOLD: Final[float] = 0.5

if (
    FEATURE_NAMES[CPU_UTIL_DIM] != "CPU util P95 (1h)"
    or FEATURE_NAMES[MEM_UTIL_DIM] != "Memory util P95 (1h)"
    or FEATURE_NAMES[STATEFUL_DIM] != "Workload is stateful"
):  # pragma: no cover - import guard
    raise AssertionError("Feature ordering changed; update heuristic dim indices.")

LIFECYCLE_SAVINGS: Final[dict[int, float]] = {
    LIFECYCLE_ON_DEMAND: 0.0,
    LIFECYCLE_RESERVED_1YR: 0.40,
    LIFECYCLE_SPOT: 0.70,
}
COMMITMENT_DISCOUNT: Final[dict[int, float]] = {
    COMMITMENT_NONE: 0.0,
    COMMITMENT_SAVINGS_1YR: 0.30,
    COMMITMENT_SAVINGS_3YR: 0.45,
}
STORAGE_SAVINGS: Final[dict[int, float]] = {
    STORAGE_HOT: 0.0,
    STORAGE_COOL: 0.50,
    STORAGE_ARCHIVE: 0.70,
}
SCHEDULING_SAVINGS: Final[dict[int, float]] = {
    SCHEDULE_ALWAYS_ON: 0.0,
    SCHEDULE_OFF_HOURS_SCALEDOWN: 0.30,
}
REGION_SAVINGS: Final[dict[int, float]] = {
    REGION_STAY: 0.0,
    REGION_CHEAPER_SAME_CONTINENT: 0.20,
    REGION_CHEAPEST_GLOBAL: 0.40,
}
REGION_EGRESS_COST: Final[dict[int, float]] = {
    REGION_STAY: 0.0,
    REGION_CHEAPER_SAME_CONTINENT: 0.05,
    REGION_CHEAPEST_GLOBAL: 0.15,
}


def right_sizing_reward(
    obs: npt.NDArray[np.float32], action: DecodedAction
) -> float:
    cpu_util = float(np.clip(obs[CPU_UTIL_DIM], 0.0, 1.0))
    mem_util = float(np.clip(obs[MEM_UTIL_DIM], 0.0, 1.0))
    new_cpu_util = cpu_util / (1.0 + action.cpu_adj_pct / 100.0)
    new_mem_util = mem_util / (1.0 + action.mem_adj_pct / 100.0)

    cost_delta = -(action.cpu_adj_pct + action.mem_adj_pct) / 40.0
    carbon_delta = cost_delta

    cpu_violation = action.cpu_adj_pct < 0 and new_cpu_util > HEADROOM_LIMIT
    mem_violation = action.mem_adj_pct < 0 and new_mem_util > HEADROOM_LIMIT

    is_stateful = float(obs[STATEFUL_DIM]) > STATEFUL_THRESHOLD
    stateful_spot_violation = (
        is_stateful and action.instance_lifecycle == LIFECYCLE_SPOT
    )
    stateful_global_region_violation = (
        is_stateful and action.region_action == REGION_CHEAPEST_GLOBAL
    )

    policy_violations = (
        int(cpu_violation)
        + int(mem_violation)
        + int(stateful_spot_violation)
        + int(stateful_global_region_violation)
    )

    max_new_util = max(new_cpu_util, new_mem_util)
    slo_compliance = float(
        np.clip(
            1.0 - max(0.0, max_new_util - SLO_HEALTHY_UTIL) / (1.0 - SLO_HEALTHY_UTIL),
            0.0,
            1.0,
        )
    )

    # 4 levers, each max ~0.70 → divide by 4 keeps aggregate in [0, 1].
    cloud_savings_raw = (
        LIFECYCLE_SAVINGS[action.instance_lifecycle]
        + COMMITMENT_DISCOUNT[action.commitment_plan]
        + STORAGE_SAVINGS[action.storage_tier]
        + SCHEDULING_SAVINGS[action.scheduling]
    )
    cloud_savings_delta = float(np.clip(cloud_savings_raw / 4.0, 0.0, 1.0))
    region_savings_delta = REGION_SAVINGS[action.region_action]
    egress_cost_delta = REGION_EGRESS_COST[action.region_action]

    return compute_reward(
        cost_delta,
        cloud_savings_delta,
        region_savings_delta,
        slo_compliance,
        carbon_delta,
        egress_cost_delta,
        policy_violations,
    )
