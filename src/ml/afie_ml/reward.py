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
    """Compute the single-step scalar reward for the RL agent.

    ``R = 0.5 * cost_delta + 0.3 * slo_compliance + 0.1 * carbon_delta
          - 10.0 * policy_violations``

    Args:
        cost_delta: Normalised cost change in ``[-1, 1]`` (positive = saving).
        slo_compliance: SLO compliance score in ``[0, 1]``.
        carbon_delta: Normalised carbon-change signal in ``[-1, 1]``
            (positive = lower).
        policy_violations: Count of PCL / safety-policy breaches this step.

    Returns:
        The scalar reward as a ``float``.

    Raises:
        ValueError: If ``slo_compliance`` is outside ``[0, 1]``.
        ValueError: If ``cost_delta`` or ``carbon_delta`` is outside ``[-1, 1]``.
        ValueError: If ``policy_violations`` is negative.
    """
    if policy_violations < 0:
        raise ValueError(
            f"policy_violations must be non-negative, got {policy_violations}"
        )

    if not 0.0 <= slo_compliance <= 1.0:
        raise ValueError(
            f"slo_compliance must be in [0, 1], got {slo_compliance}"
        )

    if not -1.0 <= cost_delta <= 1.0:
        raise ValueError(
            f"cost_delta must be in [-1, 1], got {cost_delta}"
        )

    if not -1.0 <= carbon_delta <= 1.0:
        raise ValueError(
            f"carbon_delta must be in [-1, 1], got {carbon_delta}"
        )

    return (
        COST_WEIGHT * cost_delta
        + SLO_WEIGHT * slo_compliance
        + CARBON_WEIGHT * carbon_delta
        - POLICY_VIOLATION_PENALTY * policy_violations
    )