import pytest

from afie_ml.actions import (
    ACTION_SPACE_SIZE,
    ADJUSTMENT_LEVELS,
    decode_action,
)


def test_action_space_size_is_25() -> None:
    assert ACTION_SPACE_SIZE == 25


def test_noop_action_is_zero_zero() -> None:
    # action 12 -> 12//5=2 (level 0), 12%5=2 (level 0)
    assert decode_action(12) == (0, 0)


def test_first_action_is_most_negative() -> None:
    assert decode_action(0) == (-20, -20)


def test_last_action_is_most_positive() -> None:
    assert decode_action(24) == (20, 20)


def test_cpu_is_row_memory_is_column() -> None:
    # action 7 -> 7//5=1 (cpu -10), 7%5=2 (mem 0)
    assert decode_action(7) == (-10, 0)
    # action 3 -> 3//5=0 (cpu -20), 3%5=3 (mem +10)
    assert decode_action(3) == (-20, 10)


def test_all_actions_decode_within_levels() -> None:
    for action in range(ACTION_SPACE_SIZE):
        cpu, mem = decode_action(action)
        assert cpu in ADJUSTMENT_LEVELS
        assert mem in ADJUSTMENT_LEVELS


def test_decode_is_deterministic() -> None:
    assert decode_action(17) == decode_action(17)


@pytest.mark.parametrize("action", [-1, 25, 100, -100])
def test_out_of_range_raises(action: int) -> None:
    with pytest.raises(ValueError):
        decode_action(action)