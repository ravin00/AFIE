"""Offline dataset-replay environment for safe PPO pre-training.

`AFIEOfflineEnv` replays historical 47-dim observations from a parquet file
(produced by the dataset-ingestion step) so the agent can learn a rough
policy without ever touching a live cluster.

Because this is *replay*, the next observation is simply the next dataset
row -- it does not reflect the agent's action. The reward therefore comes
from a right-sizing heuristic over (current observation, decoded action):

  * reward shrinking over-provisioned resources (cost + carbon win),
  * keep SLO compliance high by preserving headroom,
  * flag a policy violation when the action would shrink a resource whose
    post-action utilisation breaches the 10% headroom rule (the PCL rule-2
    analogue) -- the -10 penalty in `compute_reward` then dominates.

This heuristic is a documented training-time approximation. Online
fine-tuning (Phase 6) replaces it with real cost/SLO deltas measured on the
cluster.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Final, cast

import gymnasium as gym
import numpy as np
import numpy.typing as npt
import pandas as pd
from gymnasium import spaces

from afie_ml.actions import ACTION_SPACE_SIZE, decode_action
from afie_ml.feature_names import FEATURE_NAMES, STATE_VECTOR_DIM
from afie_ml.reward import compute_reward

# Dimensions the heuristic reads. Guarded below against feature reordering.
CPU_UTIL_DIM: Final[int] = 7   # "CPU util P95 (1h)"
MEM_UTIL_DIM: Final[int] = 16  # "Memory util P95 (1h)"

# Utilisation above this after a shrink = <10% headroom = policy violation.
HEADROOM_LIMIT: Final[float] = 0.9
# Utilisation at or under this = full SLO compliance; degrades linearly to 1.0.
SLO_HEALTHY_UTIL: Final[float] = 0.7

if (
    FEATURE_NAMES[CPU_UTIL_DIM] != "CPU util P95 (1h)"
    or FEATURE_NAMES[MEM_UTIL_DIM] != "Memory util P95 (1h)"
):  # pragma: no cover - import guard
    raise AssertionError(
        "Feature ordering changed; update AFIEOfflineEnv dimension indices."
    )


class AFIEOfflineEnv(gym.Env[npt.NDArray[np.float32], int]):
    """Gymnasium env that replays a parquet of 47-dim observations."""

    metadata: dict[str, Any] = {"render_modes": []}

    def __init__(self, parquet_path: str | Path) -> None:
        super().__init__()
        frame = pd.read_parquet(parquet_path)
        if frame.shape[1] != STATE_VECTOR_DIM:
            raise ValueError(
                f"expected {STATE_VECTOR_DIM} columns, got {frame.shape[1]}"
            )
        if len(frame) == 0:
            raise ValueError("offline dataset is empty")

        self._frame = frame
        self._cursor = 0
        self.observation_space = spaces.Box(
            low=-1.0, high=1.0, shape=(STATE_VECTOR_DIM,), dtype=np.float32
        )
        self.action_space = spaces.Discrete(ACTION_SPACE_SIZE)

    def reset(
        self,
        *,
        seed: int | None = None,
        options: dict[str, Any] | None = None,
    ) -> tuple[npt.NDArray[np.float32], dict[str, Any]]:
        super().reset(seed=seed)
        self._cursor = 0
        return self._row(self._cursor), {}

    def step(
        self, action: int
    ) -> tuple[npt.NDArray[np.float32], float, bool, bool, dict[str, Any]]:
        if self._cursor >= len(self._frame):
            raise RuntimeError("step() called after episode end; call reset() first")

        current = self._row(self._cursor)
        cpu_adj, mem_adj = decode_action(int(action))
        reward = self._reward(current, cpu_adj, mem_adj)

        self._cursor += 1
        truncated = self._cursor >= len(self._frame)
        next_obs = current if truncated else self._row(self._cursor)
        info = {"cpu_adjustment_pct": cpu_adj, "mem_adjustment_pct": mem_adj}
        # Offline replay has no MDP-terminal state; end of data is a truncation.
        return next_obs, reward, False, truncated, info

    def _row(self, index: int) -> npt.NDArray[np.float32]:
        values = self._frame.iloc[index].to_numpy(dtype=np.float32)
        # cast: numpy/pandas lose the float32 dtype through mypy at this boundary.
        return cast("npt.NDArray[np.float32]", np.clip(values, -1.0, 1.0))

    @staticmethod
    def _reward(obs: npt.NDArray[np.float32], cpu_adj: int, mem_adj: int) -> float:
        cpu_util = float(np.clip(obs[CPU_UTIL_DIM], 0.0, 1.0))
        mem_util = float(np.clip(obs[MEM_UTIL_DIM], 0.0, 1.0))

        # Shrinking the limit raises utilisation; growing lowers it.
        new_cpu_util = cpu_util / (1.0 + cpu_adj / 100.0)
        new_mem_util = mem_util / (1.0 + mem_adj / 100.0)

        # Cost + carbon: reward shrinking (negative adjustments), in [-1, 1].
        cost_delta = -(cpu_adj + mem_adj) / 40.0
        carbon_delta = cost_delta

        # A violation only when we SHRINK a resource past the headroom limit.
        cpu_violation = cpu_adj < 0 and new_cpu_util > HEADROOM_LIMIT
        mem_violation = mem_adj < 0 and new_mem_util > HEADROOM_LIMIT
        policy_violations = int(cpu_violation) + int(mem_violation)

        # SLO compliance: full at/under the healthy band, linearly to 0 at 1.0.
        max_new_util = max(new_cpu_util, new_mem_util)
        slo_compliance = float(
            np.clip(
                1.0 - max(0.0, max_new_util - SLO_HEALTHY_UTIL) / (1.0 - SLO_HEALTHY_UTIL),
                0.0,
                1.0,
            )
        )

        return compute_reward(cost_delta, slo_compliance, carbon_delta, policy_violations)
