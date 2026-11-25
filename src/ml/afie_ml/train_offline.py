"""Offline PPO training entry point (workflow §5.8–§5.9).

Trains a Stable-Baselines3 PPO policy against ``AFIEOfflineEnv`` and writes a
loadable checkpoint plus a ``metadata.json`` sidecar the model card and the
SHAP / serving slices read. The 1M-timestep run is a manual/offline
operation; the smoke test exercises the same path with a tiny budget.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import time
from collections.abc import Callable
from pathlib import Path

import gymnasium as gym
import numpy as np
import numpy.typing as npt
from stable_baselines3 import PPO
from stable_baselines3.common.callbacks import (
    BaseCallback,
    CheckpointCallback,
    EvalCallback,
    StopTrainingOnRewardThreshold,
)
from stable_baselines3.common.utils import set_random_seed
from stable_baselines3.common.vec_env import DummyVecEnv, SubprocVecEnv

from afie_ml.env_offline import AFIEOfflineEnv

_Env = gym.Env[npt.NDArray[np.float32], npt.NDArray[np.int64]]


def _make_env(parquet_path: str, seed: int, rank: int) -> Callable[[], _Env]:
    def _init() -> _Env:
        env = AFIEOfflineEnv(parquet_path)
        env.reset(seed=seed + rank)
        return env

    return _init


def _eval_mean_reward(model: PPO, parquet_path: str, episodes: int = 10) -> float:
    env = AFIEOfflineEnv(parquet_path)
    totals: list[float] = []
    for _ in range(episodes):
        obs, _ = env.reset()
        done = False
        total = 0.0
        while not done:
            action, _ = model.predict(obs, deterministic=True)
            obs, reward, terminated, truncated, _ = env.step(action)
            total += reward
            done = terminated or truncated
        totals.append(total)
    return float(np.mean(totals))


def _git_sha() -> str:
    try:
        return subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    except Exception:
        return "unknown"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Offline PPO trainer for AFIE.")
    parser.add_argument("--data", required=True, type=Path, help="Parquet of 55-dim rows.")
    parser.add_argument("--out", type=Path, default=Path("src/ml/models/afie_ppo_v1"))
    parser.add_argument("--timesteps", type=int, default=1_000_000)
    parser.add_argument("--n-envs", type=int, default=4)
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--tb-log", type=Path, default=None)
    parser.add_argument(
        "--stop-reward",
        type=float,
        default=None,
        help="StopTrainingOnRewardThreshold target; omit to disable.",
    )
    args = parser.parse_args(argv)

    set_random_seed(args.seed)
    data = str(args.data)
    vec_cls = SubprocVecEnv if args.n_envs > 1 else DummyVecEnv
    vec_env = vec_cls([_make_env(data, args.seed, i) for i in range(args.n_envs)])

    model = PPO(
        "MlpPolicy",
        vec_env,
        seed=args.seed,
        tensorboard_log=str(args.tb_log) if args.tb_log else None,
        verbose=1,
    )

    callbacks: list[BaseCallback] = [
        CheckpointCallback(save_freq=50_000, save_path=str(args.out / "checkpoints"))
    ]
    if args.stop_reward is not None:
        eval_env = DummyVecEnv([_make_env(data, args.seed, 999)])
        callbacks.append(
            EvalCallback(
                eval_env,
                eval_freq=10_000,
                callback_after_eval=StopTrainingOnRewardThreshold(
                    args.stop_reward, verbose=1
                ),
            )
        )

    started = time.time()
    model.learn(total_timesteps=args.timesteps, progress_bar=True, callback=callbacks)
    elapsed = time.time() - started

    args.out.mkdir(parents=True, exist_ok=True)
    model.save(args.out / "policy.zip")
    (args.out / "metadata.json").write_text(
        json.dumps(
            {
                "git_sha": _git_sha(),
                "timesteps": args.timesteps,
                "n_envs": args.n_envs,
                "seed": args.seed,
                "data": data,
                "wall_clock_s": round(elapsed, 1),
                "mean_reward_10ep": _eval_mean_reward(model, data),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
