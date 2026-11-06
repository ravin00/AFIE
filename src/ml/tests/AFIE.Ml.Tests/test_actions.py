import numpy as np
import pytest

from afie_ml.actions import (
    ACTION_SPACE_NVEC,
    ADJUSTMENT_LEVELS,
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
    decode_action,
)

NOOP = [2, 2, 0, 0, 0, 0, 0]


def test_action_space_is_seven_dim_multidiscrete() -> None:
    assert ACTION_SPACE_NVEC == (5, 5, 3, 3, 3, 2, 3)


def test_noop_decodes_to_zero_across_all_dims() -> None:
    assert decode_action(NOOP) == DecodedAction(
        cpu_adj_pct=0,
        mem_adj_pct=0,
        instance_lifecycle=LIFECYCLE_ON_DEMAND,
        commitment_plan=COMMITMENT_NONE,
        storage_tier=STORAGE_HOT,
        scheduling=SCHEDULE_ALWAYS_ON,
        region_action=REGION_STAY,
    )


def test_cpu_and_mem_dims_use_adjustment_levels() -> None:
    assert decode_action([0, 0, 0, 0, 0, 0, 0]).cpu_adj_pct == ADJUSTMENT_LEVELS[0]
    assert decode_action([4, 4, 0, 0, 0, 0, 0]).mem_adj_pct == ADJUSTMENT_LEVELS[4]


def test_maximum_savings_action() -> None:
    # all-greedy: shrink hardest, spot, 3yr commitment, archive, off-hours, global
    decoded = decode_action([0, 0, 2, 2, 2, 1, 2])
    assert decoded == DecodedAction(
        cpu_adj_pct=-20,
        mem_adj_pct=-20,
        instance_lifecycle=LIFECYCLE_SPOT,
        commitment_plan=COMMITMENT_SAVINGS_3YR,
        storage_tier=STORAGE_ARCHIVE,
        scheduling=SCHEDULE_OFF_HOURS_SCALEDOWN,
        region_action=REGION_CHEAPEST_GLOBAL,
    )


def test_mid_tier_lifecycle_and_region() -> None:
    decoded = decode_action([2, 2, 1, 1, 1, 0, 1])
    assert decoded.instance_lifecycle == LIFECYCLE_RESERVED_1YR
    assert decoded.commitment_plan == COMMITMENT_SAVINGS_1YR
    assert decoded.storage_tier == STORAGE_COOL
    assert decoded.region_action == REGION_CHEAPER_SAME_CONTINENT


def test_accepts_numpy_array() -> None:
    arr = np.array(NOOP, dtype=np.int64)
    assert decode_action(arr).cpu_adj_pct == 0


def test_wrong_shape_raises() -> None:
    with pytest.raises(ValueError, match="shape"):
        decode_action([0, 0, 0])


@pytest.mark.parametrize(
    "action",
    [
        [-1, 2, 0, 0, 0, 0, 0],
        [5, 2, 0, 0, 0, 0, 0],
        [2, 2, 3, 0, 0, 0, 0],
        [2, 2, 0, 3, 0, 0, 0],
        [2, 2, 0, 0, 3, 0, 0],
        [2, 2, 0, 0, 0, 2, 0],
        [2, 2, 0, 0, 0, 0, 3],
    ],
)
def test_out_of_range_dim_raises(action: list[int]) -> None:
    with pytest.raises(ValueError):
        decode_action(action)
