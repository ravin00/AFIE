import numpy as np
import pandas as pd
from stable_baselines3 import PPO

from afie_ml import train_offline
from afie_ml.actions import ACTION_SPACE_NVEC
from afie_ml.feature_names import FEATURE_NAMES, STATE_VECTOR_DIM


def _write_parquet(path, n_rows: int = 64) -> str:
    rng = np.random.default_rng(0)
    rows = rng.uniform(-1.0, 1.0, size=(n_rows, STATE_VECTOR_DIM))
    pd.DataFrame(rows, columns=list(FEATURE_NAMES)).to_parquet(path)
    return str(path)


def test_train_offline_produces_loadable_checkpoint(tmp_path) -> None:
    data = _write_parquet(tmp_path / "synth.parquet")
    out = tmp_path / "model"

    rc = train_offline.main(
        ["--data", data, "--out", str(out), "--timesteps", "512", "--n-envs", "1"]
    )

    assert rc == 0
    assert (out / "policy.zip").exists()
    assert (out / "metadata.json").exists()

    model = PPO.load(out / "policy.zip")
    action, _ = model.predict(np.zeros(STATE_VECTOR_DIM, np.float32), deterministic=True)
    assert action.shape == (len(ACTION_SPACE_NVEC),)
