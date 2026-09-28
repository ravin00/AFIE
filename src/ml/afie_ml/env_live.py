"""Live environment: observations come from the running feature-engineering
service (GET /state/{workload}). Used for online fine-tuning (Phase 6+);
action submission to the cluster is the operator's job, so step() re-observes
without applying the action until then.
"""

from __future__ import annotations

from typing import Any 

import gymnasium as gym
import numpy as np
import numpy.typing as npt
import requests
from gymnasium import spaces

from afie_ml.actions import ACTION_SPACE_SIZE, decode_action
from afie_ml.feature_names import STATE_VECTOR_DIM
from afie_ml.heuristic import right_sizing_reward


class AFIEEnv(gym.Env[npt.NDArray[np.float32], int]):
    """Gymnasium env backed by the live feature-engineering /state endpoint."""

    metadata: dict[str, Any] = {"render_modes": []}

    def __init__(
        self,
        workload: str,
        fe_base_url: str = "http://localhost:8080",
        timeout: float = 5.0,
    ) -> None:
        super().__init__()
        self._url = f"{fe_base_url.rstrip('/')}/state/{workload}"
        self._timeout = timeout
        self.observation_space = spaces.Box(
            low=-1.0, high=1.0, shape=(STATE_VECTOR_DIM,), dtype=np.float32
        )
        self.action_space = spaces.Discrete(ACTION_SPACE_SIZE)

    def _fetch_state(self) -> npt.NDArray[np.float32]:
        resp = requests.get(self._url, timeout=self._timeout)
        resp.raise_for_status()
        values = np.asarray(resp.json(), dtype=np.float32)
        if values.shape != (STATE_VECTOR_DIM,):
            raise ValueError(
                f"expected {STATE_VECTOR_DIM} values from {self._url}, got {values.shape}"
            )
        return np.clip(values, -1.0, 1.0)

    def reset(
        self,
        *,
        seed: int | None = None,
        options: dict[str, Any] | None = None,
    ) -> tuple[npt.NDArray[np.float32], dict[str, Any]]:
        super().reset(seed=seed)
        return self._fetch_state(), {}

    def step(
        self, action: int
    ) -> tuple[npt.NDArray[np.float32], float, bool, bool, dict[str, Any]]:
        cpu_adj, mem_adj = decode_action(int(action))
        current = self._fetch_state()
        reward = right_sizing_reward(current, cpu_adj, mem_adj)
        next_obs = self._fetch_state()
        info = {"cpu_adjustment_pct": cpu_adj, "mem_adjustment_pct": mem_adj}
        return next_obs, reward, False, False, info