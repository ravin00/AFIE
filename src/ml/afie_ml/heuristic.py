"""Right-sizing reward heuristic shared by the offline and live environments.

Maps (observation, decoded action) to the four reward terms via compute_reward,
so offline pre-training and online fine-tuning optimise the same reward.
Training-time approximation; replaced by real cost/SLO deltas in Phase 6.
"""

from __future__ import annotations

from typing import Final

import numpy as np
import numpy.typing as npt

from afie_ml.feature_names import FEATURE_NAMES
from afie_ml.reward import compute_reward

CPU_UTIL_DIM: Final[int] = 7   # "CPU util P95 (1h)"
MEM_UTIL_DIM: Final[int] = 16  # "Memory util P95 (1h)"
HEADROOM_LIMIT: Final[float] = 0.9    # >this after a shrink = <10% headroom = violation
SLO_HEALTHY_UTIL: Final[float] = 0.7  # at/under this = full SLO compliance

if (
    FEATURE_NAMES[CPU_UTIL_DIM] != "CPU util P95 (1h)"
    or FEATURE_NAMES[MEM_UTIL_DIM] != "Memory util P95 (1h)"
):  # pragma: no cover - import guard
    raise AssertionError("Feature ordering changed; update heuristic dim indices.")


def right_sizing_reward(
    obs: npt.NDArray[np.float32], cpu_adj: int, mem_adj: int
) -> float:
    cpu_util = float(np.clip(obs[CPU_UTIL_DIM], 0.0, 1.0))
    mem_util = float(np.clip(obs[MEM_UTIL_DIM], 0.0, 1.0))
    new_cpu_util = cpu_util / (1.0 + cpu_adj / 100.0)
    new_mem_util = mem_util / (1.0 + mem_adj / 100.0)

    cost_delta = -(cpu_adj + mem_adj) / 40.0
    carbon_delta = cost_delta

    # Violation only on a shrink past headroom — a no-op or grow never violates.
    cpu_violation = cpu_adj < 0 and new_cpu_util > HEADROOM_LIMIT
    mem_violation = mem_adj < 0 and new_mem_util > HEADROOM_LIMIT
    policy_violations = int(cpu_violation) + int(mem_violation)

    max_new_util = max(new_cpu_util, new_mem_util)
    slo_compliance = float(
        np.clip(
            1.0 - max(0.0, max_new_util - SLO_HEALTHY_UTIL) / (1.0 - SLO_HEALTHY_UTIL),
            0.0,
            1.0,
        )
    )
    return compute_reward(cost_delta, slo_compliance, carbon_delta, policy_violations)