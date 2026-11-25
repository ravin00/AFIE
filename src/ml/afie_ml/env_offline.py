"""Offline dataset-replay environment for safe PPO pre-training.

AFIEOfflineEnv replays historical 55-dim observations from a parquet file so
the agent can learn a rough policy without touching a live cluster. Because
this is replay, the next observation is just the next dataset row, not the
effect of the action; the reward comes from the shared right-sizing heuristic.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, cast

import gymnasium as gym
import numpy as np
import numpy.typing as npt
import pandas as pd
from gymnasium import spaces

from afie_ml.actions import ACTION_SPACE_NVEC, decode_action
from afie_ml.feature_names import FEATURE_NAMES, STATE_VECTOR_DIM
from afie_ml.heuristic import right_sizing_reward


class AFIEOfflineEnv(gym.Env[npt.NDArray[np.float32], npt.NDArray[np.int64]]):
    """Gymnasium env that replays a parquet of 55-dim observations."""

    metadata: dict[str, Any] = {"render_modes": []}

    def __init__(self, parquet_path: str | Path) -> None:
        super().__init__()
        frame = pd.read_parquet(parquet_path)
        missing = [n for n in FEATURE_NAMES if n not in frame.columns]
        if missing:
            raise ValueError(f"parquet missing expected feature columns: {missing[:5]}...")
        frame = frame.loc[:, list(FEATURE_NAMES)]
        if len(frame) == 0:
            raise ValueError("offline dataset is empty")

        data = frame.to_numpy(dtype=np.float32)
        if not np.isfinite(data).all():
            raise ValueError("offline dataset contains non-finite values")
        self._data: npt.NDArray[np.float32] = np.clip(data, -1.0, 1.0)
        self._cursor = 0
        self.observation_space = spaces.Box(
            low=-1.0, high=1.0, shape=(STATE_VECTOR_DIM,), dtype=np.float32
        )
        self.action_space = spaces.MultiDiscrete(np.array(ACTION_SPACE_NVEC))  # type: ignore[assignment]

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
        self, action: npt.NDArray[np.int64]
    ) -> tuple[npt.NDArray[np.float32], float, bool, bool, dict[str, Any]]:
        if self._cursor >= len(self._data):
            raise RuntimeError("step() called after episode end; call reset() first")

        current = self._row(self._cursor)
        decoded = decode_action(action)
        reward = right_sizing_reward(current, decoded)

        self._cursor += 1
        truncated = self._cursor >= len(self._data)
        next_obs = current if truncated else self._row(self._cursor)
        info = {
            "cpu_adjustment_pct": decoded.cpu_adj_pct,
            "mem_adjustment_pct": decoded.mem_adj_pct,
            "instance_lifecycle": decoded.instance_lifecycle,
            "commitment_plan": decoded.commitment_plan,
            "storage_tier": decoded.storage_tier,
            "scheduling": decoded.scheduling,
            "region_action": decoded.region_action,
        }
        # Offline replay has no MDP-terminal state; end of data is a truncation.
        return next_obs, reward, False, truncated, info

    def _row(self, index: int) -> npt.NDArray[np.float32]:
        return cast("npt.NDArray[np.float32]", self._data[index].copy())
