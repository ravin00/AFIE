from typing import Final

COST_WEIGHT: Final[float] = 0.5
"""Weight for the cost-delta term in the reward signal."""

SLO_WEIGHT: Final[float] = 0.3
"""Weight for the SLO-compliance term in the reward signal."""

CARBON_WEIGHT: Final[float] = 0.1
"""Weight for the carbon-delta term in the reward signal."""

POLICY_VIOLATION_PENALTY: Final[float] = 10.0
"""Per-violation penalty.  ~20x any positive term so the agent can never
learn to trade a safety breach for cost savings."""


def compute_reward(
    cost_delta: float,
    slo_compliance: float,
    carbon_delta: float,
    policy_violations: int,
) -> float:
    if policy_violations < 0:
        raise ValueError(
            f"policy_violations must be non-negative, got {policy_violations}"
        )

    return (
        COST_WEIGHT * cost_delta
        + SLO_WEIGHT * slo_compliance
        + CARBON_WEIGHT * carbon_delta
        - POLICY_VIOLATION_PENALTY * policy_violations
    )


"""Compute the single-step scalar reward for the RL agent.

    ``R = 0.5 * cost_delta + 0.3 * slo_compliance + 0.1 * carbon_delta
          - 10.0 * policy_violations``

    Args:
        cost_delta: Normalised cost change (positive = saving).
        slo_compliance: SLO compliance score in ``[0, 1]``.
        carbon_delta: Normalised carbon-change signal (positive = lower).
        policy_violations: Count of PCL / safety-policy breaches this step.

    Returns:
        The scalar reward as a ``float``.

    Raises:
        ValueError: If ``policy_violations`` is negative.
"""