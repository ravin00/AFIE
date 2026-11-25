import pytest

from afie_ml.reward import (
    CARBON_WEIGHT,
    CLOUD_SAVINGS_WEIGHT,
    COST_WEIGHT,
    EGRESS_PENALTY_WEIGHT,
    POLICY_VIOLATION_PENALTY,
    REGION_SAVINGS_WEIGHT,
    SLO_WEIGHT,
    compute_reward,
)

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------


def test_weights() -> None:
    assert COST_WEIGHT == pytest.approx(0.30)
    assert CLOUD_SAVINGS_WEIGHT == pytest.approx(0.20)
    assert REGION_SAVINGS_WEIGHT == pytest.approx(0.10)
    assert SLO_WEIGHT == pytest.approx(0.20)
    assert CARBON_WEIGHT == pytest.approx(0.08)
    assert EGRESS_PENALTY_WEIGHT == pytest.approx(0.05)
    assert POLICY_VIOLATION_PENALTY == pytest.approx(10.0)


# ---------------------------------------------------------------------------
# Known input → known output
# ---------------------------------------------------------------------------


def test_all_zero_inputs() -> None:
    assert compute_reward(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0) == pytest.approx(0.0)


def test_all_positive_inputs_no_egress() -> None:
    # 0.30 + 0.20 + 0.10 + 0.20 + 0.08 = 0.88
    assert compute_reward(1.0, 1.0, 1.0, 1.0, 1.0, 0.0, 0) == pytest.approx(0.88)


def test_max_savings_with_max_egress() -> None:
    # 0.88 - 0.05 = 0.83
    assert compute_reward(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0) == pytest.approx(0.83)


def test_single_violation_dominates_everything() -> None:
    # 0.83 - 10.0 = -9.17
    assert compute_reward(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1) == pytest.approx(-9.17)


def test_two_violations() -> None:
    assert compute_reward(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 2) == pytest.approx(-19.17)


# ---------------------------------------------------------------------------
# Dominance invariant
# ---------------------------------------------------------------------------


def test_one_violation_outweighs_max_positive_reward() -> None:
    max_positive = (
        COST_WEIGHT * 1.0
        + CLOUD_SAVINGS_WEIGHT * 1.0
        + REGION_SAVINGS_WEIGHT * 1.0
        + SLO_WEIGHT * 1.0
        + CARBON_WEIGHT * 1.0
    )
    reward = compute_reward(1.0, 1.0, 1.0, 1.0, 1.0, 0.0, 1)
    assert reward < -max_positive


# ---------------------------------------------------------------------------
# Term isolation
# ---------------------------------------------------------------------------


def test_cost_only() -> None:
    assert compute_reward(1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0) == pytest.approx(0.30)


def test_cloud_savings_only() -> None:
    assert compute_reward(0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 0) == pytest.approx(0.20)


def test_region_savings_only() -> None:
    assert compute_reward(0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0) == pytest.approx(0.10)


def test_slo_only() -> None:
    assert compute_reward(0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0) == pytest.approx(0.20)


def test_carbon_only() -> None:
    assert compute_reward(0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0) == pytest.approx(0.08)


def test_egress_penalty_subtracted() -> None:
    assert compute_reward(0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0) == pytest.approx(-0.05)


def test_negative_cost_delta() -> None:
    assert compute_reward(-1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0) == pytest.approx(-0.30)


# ---------------------------------------------------------------------------
# Validation
# ---------------------------------------------------------------------------


def test_negative_violations_raises() -> None:
    with pytest.raises(ValueError, match="non-negative"):
        compute_reward(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, -1)


def test_slo_out_of_range_raises() -> None:
    with pytest.raises(ValueError, match="in \\[0, 1\\]"):
        compute_reward(0.0, 0.0, 0.0, -0.1, 0.0, 0.0, 0)
    with pytest.raises(ValueError, match="in \\[0, 1\\]"):
        compute_reward(0.0, 0.0, 0.0, 1.1, 0.0, 0.0, 0)


def test_cost_delta_out_of_range_raises() -> None:
    with pytest.raises(ValueError, match="in \\[-1, 1\\]"):
        compute_reward(1.1, 0.0, 0.0, 0.0, 0.0, 0.0, 0)
    with pytest.raises(ValueError, match="in \\[-1, 1\\]"):
        compute_reward(-1.1, 0.0, 0.0, 0.0, 0.0, 0.0, 0)


def test_cloud_savings_out_of_range_raises() -> None:
    with pytest.raises(ValueError, match="cloud_savings"):
        compute_reward(0.0, -0.1, 0.0, 0.0, 0.0, 0.0, 0)
    with pytest.raises(ValueError, match="cloud_savings"):
        compute_reward(0.0, 1.1, 0.0, 0.0, 0.0, 0.0, 0)


def test_region_savings_out_of_range_raises() -> None:
    with pytest.raises(ValueError, match="region_savings"):
        compute_reward(0.0, 0.0, -0.1, 0.0, 0.0, 0.0, 0)


def test_egress_cost_out_of_range_raises() -> None:
    with pytest.raises(ValueError, match="egress_cost"):
        compute_reward(0.0, 0.0, 0.0, 0.0, 0.0, 1.1, 0)


def test_carbon_delta_out_of_range_raises() -> None:
    with pytest.raises(ValueError, match="carbon_delta"):
        compute_reward(0.0, 0.0, 0.0, 0.0, 1.1, 0.0, 0)
