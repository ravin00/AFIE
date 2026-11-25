"""Scalar reward for the 7-dim AFIE action space (workflow §5.6 + region gap).

``R = 0.30 cost + 0.20 cloud_savings + 0.10 region_savings
      + 0.20 slo + 0.08 carbon − 0.05 egress − 10 policy_violations``

Max positive terms sum to 0.88, so a single violation (-10) still dominates
and the agent cannot trade a safety breach for aggregate savings.
"""

from typing import Final

COST_WEIGHT: Final[float] = 0.30
CLOUD_SAVINGS_WEIGHT: Final[float] = 0.20
REGION_SAVINGS_WEIGHT: Final[float] = 0.10
SLO_WEIGHT: Final[float] = 0.20
CARBON_WEIGHT: Final[float] = 0.08
EGRESS_PENALTY_WEIGHT: Final[float] = 0.05
POLICY_VIOLATION_PENALTY: Final[float] = 10.0


def compute_reward(
    cost_delta: float,
    cloud_savings_delta: float,
    region_savings_delta: float,
    slo_compliance: float,
    carbon_delta: float,
    egress_cost_delta: float,
    policy_violations: int,
) -> float:
    """Compute the single-step scalar reward.

    Args:
        cost_delta: Right-sizing cost change in ``[-1, 1]`` (positive = saving).
        cloud_savings_delta: Aggregate lifecycle/commitment/storage/scheduling
            discount in ``[0, 1]``.
        region_savings_delta: Region-migration discount in ``[0, 1]``.
        slo_compliance: SLO compliance score in ``[0, 1]``.
        carbon_delta: Normalised carbon-change signal in ``[-1, 1]``.
        egress_cost_delta: Normalised cross-region egress cost in ``[0, 1]``.
        policy_violations: Count of PCL / safety-policy breaches this step.
    """
    if policy_violations < 0:
        raise ValueError(
            f"policy_violations must be non-negative, got {policy_violations}"
        )
    if not 0.0 <= slo_compliance <= 1.0:
        raise ValueError(f"slo_compliance must be in [0, 1], got {slo_compliance}")
    if not -1.0 <= cost_delta <= 1.0:
        raise ValueError(f"cost_delta must be in [-1, 1], got {cost_delta}")
    if not -1.0 <= carbon_delta <= 1.0:
        raise ValueError(f"carbon_delta must be in [-1, 1], got {carbon_delta}")
    if not 0.0 <= cloud_savings_delta <= 1.0:
        raise ValueError(
            f"cloud_savings_delta must be in [0, 1], got {cloud_savings_delta}"
        )
    if not 0.0 <= region_savings_delta <= 1.0:
        raise ValueError(
            f"region_savings_delta must be in [0, 1], got {region_savings_delta}"
        )
    if not 0.0 <= egress_cost_delta <= 1.0:
        raise ValueError(
            f"egress_cost_delta must be in [0, 1], got {egress_cost_delta}"
        )

    return (
        COST_WEIGHT * cost_delta
        + CLOUD_SAVINGS_WEIGHT * cloud_savings_delta
        + REGION_SAVINGS_WEIGHT * region_savings_delta
        + SLO_WEIGHT * slo_compliance
        + CARBON_WEIGHT * carbon_delta
        - EGRESS_PENALTY_WEIGHT * egress_cost_delta
        - POLICY_VIOLATION_PENALTY * policy_violations
    )
