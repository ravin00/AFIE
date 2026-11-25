import numpy as np
import pandas as pd
import pytest

from afie_ml.actions import ACTION_SPACE_NVEC, LIFECYCLE_SPOT, REGION_CHEAPEST_GLOBAL
from afie_ml.env_offline import AFIEOfflineEnv
from afie_ml.feature_names import FEATURE_NAMES, STATE_VECTOR_DIM

NOOP = np.array([2, 2, 0, 0, 0, 0, 0], dtype=np.int64)


def _write_parquet(path, rows: list[list[float]]) -> str:
    df = pd.DataFrame(rows, columns=list(FEATURE_NAMES))
    df.to_parquet(path)
    return str(path)


@pytest.fixture
def dataset(tmp_path):
    rng = np.random.default_rng(0)
    rows = rng.uniform(-1.0, 1.0, size=(5, STATE_VECTOR_DIM)).tolist()
    return _write_parquet(tmp_path / "offline.parquet", rows)


def test_spaces(dataset) -> None:
    env = AFIEOfflineEnv(dataset)
    assert env.observation_space.shape == (STATE_VECTOR_DIM,)
    assert tuple(env.action_space.nvec.tolist()) == ACTION_SPACE_NVEC


def test_reset_returns_first_row_in_range(dataset) -> None:
    env = AFIEOfflineEnv(dataset)
    obs, info = env.reset()
    assert obs.shape == (STATE_VECTOR_DIM,)
    assert obs.dtype == np.float32
    assert np.all(obs >= -1.0) and np.all(obs <= 1.0)
    assert info == {}


def test_step_returns_gymnasium_5_tuple(dataset) -> None:
    env = AFIEOfflineEnv(dataset)
    env.reset()
    obs, reward, terminated, truncated, info = env.step(NOOP)
    assert obs.shape == (STATE_VECTOR_DIM,)
    assert isinstance(reward, float)
    assert terminated is False
    assert isinstance(truncated, bool)
    assert info["cpu_adjustment_pct"] == 0
    assert info["region_action"] == 0


def test_episode_truncates_only_at_end_of_data(dataset) -> None:
    env = AFIEOfflineEnv(dataset)
    env.reset()
    truncations = []
    for _ in range(5):
        _, _, terminated, truncated, _ = env.step(NOOP)
        assert terminated is False
        truncations.append(truncated)
    assert truncations == [False, False, False, False, True]


def test_step_after_end_raises(dataset) -> None:
    env = AFIEOfflineEnv(dataset)
    env.reset()
    for _ in range(5):
        env.step(NOOP)
    with pytest.raises(RuntimeError):
        env.step(NOOP)


def test_noop_never_violates(dataset) -> None:
    env = AFIEOfflineEnv(dataset)
    env.reset()
    _, reward, _, _, _ = env.step(NOOP)
    assert reward >= 0.0


def test_shrinking_hot_resource_is_penalised(tmp_path) -> None:
    row = [0.0] * STATE_VECTOR_DIM
    row[7] = 0.95  # CPU util P95 (1h) -> hot
    path = _write_parquet(tmp_path / "hot.parquet", [row, row])
    env = AFIEOfflineEnv(path)
    env.reset()
    # [0,0,...] = shrink CPU/mem -20%, 0.95/0.8 = 1.19 > 0.9 -> violation
    shrink_hard = np.array([0, 0, 0, 0, 0, 0, 0], dtype=np.int64)
    _, reward, _, _, _ = env.step(shrink_hard)
    assert reward < 0.0


def test_shrinking_cold_resource_is_rewarded(tmp_path) -> None:
    row = [0.0] * STATE_VECTOR_DIM
    row[7] = 0.1
    row[16] = 0.1
    path = _write_parquet(tmp_path / "cold.parquet", [row, row])
    env = AFIEOfflineEnv(path)
    env.reset()
    shrink_hard = np.array([0, 0, 0, 0, 0, 0, 0], dtype=np.int64)
    _, reward, _, _, _ = env.step(shrink_hard)
    assert reward > 0.0


def test_cloud_savings_lift_reward_on_cold_workload(tmp_path) -> None:
    row = [0.0] * STATE_VECTOR_DIM
    row[7] = 0.1
    row[16] = 0.1
    path = _write_parquet(tmp_path / "cold.parquet", [row, row])
    env = AFIEOfflineEnv(path)
    env.reset()
    # 3yr savings plan + archive storage + off-hours scale-down, no region move
    action = np.array([2, 2, 1, 2, 2, 1, 0], dtype=np.int64)
    _, reward_with_cloud_savings, _, _, _ = env.step(action)
    env.reset()
    _, reward_noop, _, _, _ = env.step(NOOP)
    assert reward_with_cloud_savings > reward_noop


def test_stateful_spot_is_a_violation(tmp_path) -> None:
    row = [0.0] * STATE_VECTOR_DIM
    row[52] = 1.0  # workload_is_stateful
    path = _write_parquet(tmp_path / "stateful.parquet", [row, row])
    env = AFIEOfflineEnv(path)
    env.reset()
    action = np.array([2, 2, LIFECYCLE_SPOT, 0, 0, 0, 0], dtype=np.int64)
    _, reward, _, _, _ = env.step(action)
    assert reward < -5.0  # one violation dominates


def test_stateful_global_region_move_is_a_violation(tmp_path) -> None:
    row = [0.0] * STATE_VECTOR_DIM
    row[52] = 1.0
    path = _write_parquet(tmp_path / "stateful.parquet", [row, row])
    env = AFIEOfflineEnv(path)
    env.reset()
    action = np.array([2, 2, 0, 0, 0, 0, REGION_CHEAPEST_GLOBAL], dtype=np.int64)
    _, reward, _, _, _ = env.step(action)
    assert reward < -5.0


def test_wrong_column_count_raises(tmp_path) -> None:
    df = pd.DataFrame([[0.0, 1.0, 2.0]], columns=["a", "b", "c"])
    path = tmp_path / "bad.parquet"
    df.to_parquet(path)
    with pytest.raises(ValueError):
        AFIEOfflineEnv(str(path))


def test_empty_dataset_raises(tmp_path) -> None:
    df = pd.DataFrame(columns=[f"f{i}" for i in range(STATE_VECTOR_DIM)])
    path = tmp_path / "empty.parquet"
    df.to_parquet(path)
    with pytest.raises(ValueError):
        AFIEOfflineEnv(str(path))
