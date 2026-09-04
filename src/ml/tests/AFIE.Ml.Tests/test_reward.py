import pytest

from afie_ml.reward import (
    CARBON_WEIGHT,
    COST_WEIGHT,
    POLICY_VIOLATION_PENALTY,
    SLO_WEIGHT,
    compute_reward,
)

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------


def test_cost_weight() -> None:
    assert COST_WEIGHT == pytest.approx(0.5)


def test_slo_weight() -> None:
    assert SLO_WEIGHT == pytest.approx(0.3)


def test_carbon_weight() -> None:
    assert CARBON_WEIGHT == pytest.approx(0.1)


def test_violation_penalty() -> None:
    assert POLICY_VIOLATION_PENALTY == pytest.approx(10.0)


# ---------------------------------------------------------------------------
# Known input → known output
# ---------------------------------------------------------------------------


def test_all_positive_inputs() -> None:
    assert compute_reward(1.0, 1.0, 1.0, 0) == pytest.approx(0.9)


def test_zero_inputs() -> None:
    assert compute_reward(0.0, 0.0, 0.0, 0) == pytest.approx(0.0)


def test_single_violation() -> None:
    assert compute_reward(1.0, 1.0, 1.0, 1) == pytest.approx(-9.1)


def test_two_violations() -> None:
    assert compute_reward(1.0, 1.0, 1.0, 2) == pytest.approx(-19.1)


# ---------------------------------------------------------------------------
# Dominance property — one violation always outweighs every positive term
# ---------------------------------------------------------------------------


def test_one_violation_outweighs_max_positive_reward() -> None:
    """A single violation (-10.0) must dominate the best possible positive
    reward from the other three terms combined (0.5 + 0.3 + 0.1 = 0.9)."""
    max_positive = (
        COST_WEIGHT * 1.0 + SLO_WEIGHT * 1.0 + CARBON_WEIGHT * 1.0
    )
    reward_with_one_violation = compute_reward(1.0, 1.0, 1.0, 1)
    assert reward_with_one_violation < -max_positive


# ---------------------------------------------------------------------------
# Edge cases
# ---------------------------------------------------------------------------


def test_negative_cost_delta() -> None:
    assert compute_reward(-1.0, 0.0, 0.0, 0) == pytest.approx(-0.5)


def test_negative_carbon_delta() -> None:
    assert compute_reward(0.0, 0.0, -1.0, 0) == pytest.approx(-0.1)


def test_partial_inputs() -> None:
    assert compute_reward(0.5, 0.0, 0.0, 0) == pytest.approx(0.25)
    assert compute_reward(0.0, 0.8, 0.0, 0) == pytest.approx(0.24)
    assert compute_reward(0.0, 0.0, 0.6, 0) == pytest.approx(0.06)


def test_slo_boundaries_are_inclusive() -> None:
    # 0 and 1 are the documented inclusive boundaries and must remain valid.
    assert compute_reward(0.0, 0.0, 0.0, 0) == pytest.approx(0.0)
    assert compute_reward(0.0, 1.0, 0.0, 0) == pytest.approx(0.3)


# ---------------------------------------------------------------------------
# Validation
# ---------------------------------------------------------------------------


def test_negative_violations_raises() -> None:
    with pytest.raises(ValueError, match="non-negative"):
        compute_reward(0.0, 0.0, 0.0, -1)


def test_negative_violations_raises_large_negative() -> None:
    with pytest.raises(ValueError):
        compute_reward(0.0, 0.0, 0.0, -100)


def test_slo_below_zero_raises() -> None:
    with pytest.raises(ValueError, match="in \\[0, 1\\]"):
        compute_reward(0.0, -0.1, 0.0, 0)


def test_slo_above_one_raises() -> None:
    with pytest.raises(ValueError, match="in \\[0, 1\\]"):
        compute_reward(0.0, 1.1, 0.0, 0)
