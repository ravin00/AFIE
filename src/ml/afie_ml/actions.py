"""Action-space contract for the AFIE RL agent.

The agent emits a ``MultiDiscrete`` 7-tuple of integers, one per lever:

    dim 0 cpu_adj          — resource request/limit tweak
    dim 1 mem_adj           — resource request/limit tweak
    dim 2 instance_lifecycle — on-demand / reserved-1yr / spot
    dim 3 commitment_plan    — none / savings-plan-1yr / savings-plan-3yr
    dim 4 storage_tier       — hot / cool / archive
    dim 5 scheduling         — always-on / off-hours-scale-down
    dim 6 region_action      — stay / cheaper-same-continent / cheapest-global

``decode_action`` is pure: policy safety (spot-for-stateful, global-move for
stateful) is enforced by the reward heuristic / PCL, not here. The decoder
only validates per-dim ranges.
"""

from typing import Final, NamedTuple

import numpy as np
import numpy.typing as npt

ADJUSTMENT_LEVELS: Final[tuple[int, ...]] = (-20, -10, 0, 10, 20)
"""Percent levels for the CPU and memory dims (indices 0 and 1)."""

ACTION_SPACE_NVEC: Final[tuple[int, ...]] = (5, 5, 3, 3, 3, 2, 3)
"""Per-dim bin counts fed to ``gym.spaces.MultiDiscrete``."""

# Dim 2 — instance lifecycle
LIFECYCLE_ON_DEMAND: Final[int] = 0
LIFECYCLE_RESERVED_1YR: Final[int] = 1
LIFECYCLE_SPOT: Final[int] = 2

# Dim 3 — commitment plan
COMMITMENT_NONE: Final[int] = 0
COMMITMENT_SAVINGS_1YR: Final[int] = 1
COMMITMENT_SAVINGS_3YR: Final[int] = 2

# Dim 4 — storage tier
STORAGE_HOT: Final[int] = 0
STORAGE_COOL: Final[int] = 1
STORAGE_ARCHIVE: Final[int] = 2

# Dim 5 — scheduling
SCHEDULE_ALWAYS_ON: Final[int] = 0
SCHEDULE_OFF_HOURS_SCALEDOWN: Final[int] = 1

# Dim 6 — region action
REGION_STAY: Final[int] = 0
REGION_CHEAPER_SAME_CONTINENT: Final[int] = 1
REGION_CHEAPEST_GLOBAL: Final[int] = 2


class DecodedAction(NamedTuple):
    cpu_adj_pct: int
    mem_adj_pct: int
    instance_lifecycle: int
    commitment_plan: int
    storage_tier: int
    scheduling: int
    region_action: int


def decode_action(action: npt.ArrayLike) -> DecodedAction:
    """Decode a ``MultiDiscrete`` sample into a :class:`DecodedAction`.

    Args:
        action: Length-7 integer sequence (list, tuple, or ``ndarray``).

    Raises:
        ValueError: If the shape is wrong or any dim is outside its range.
    """
    arr = np.asarray(action, dtype=np.int64).ravel()
    expected = len(ACTION_SPACE_NVEC)
    if arr.shape != (expected,):
        raise ValueError(f"action must have shape ({expected},), got {arr.shape}")
    for i, (val, nvec) in enumerate(zip(arr, ACTION_SPACE_NVEC, strict=True)):
        if not 0 <= int(val) < nvec:
            raise ValueError(f"action dim {i} must be in [0, {nvec}), got {val}")
    return DecodedAction(
        cpu_adj_pct=ADJUSTMENT_LEVELS[int(arr[0])],
        mem_adj_pct=ADJUSTMENT_LEVELS[int(arr[1])],
        instance_lifecycle=int(arr[2]),
        commitment_plan=int(arr[3]),
        storage_tier=int(arr[4]),
        scheduling=int(arr[5]),
        region_action=int(arr[6]),
    )
