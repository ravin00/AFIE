"""Action decoding for the AFIE RL agent.

The agent's action space is ``Discrete(25)`` — a flat 5x5 grid of CPU and
memory adjustment levels. The centre action (12) is the no-op. This module
is the single source of truth for turning an action integer into a concrete
``(cpu_adjustment_pct, mem_adjustment_pct)`` pair; offline training, the live
environment, and the Flask inference server all decode through it so the
mapping can never drift between training and serving.

Canonical spec — architecture.md section 6:
    Discrete(25). levels = [-20, -10, 0, +10, +20] percent.
    cpu_adj = levels[a // 5], mem_adj = levels[a % 5].
"""

from typing import Final

ADJUSTMENT_LEVELS: Final[tuple[int, ...]] = (-20, -10, 0, 10, 20)
"""Percent adjustment levels, ordered. Index maps to the agent's grid axis."""

ACTION_SPACE_SIZE: Final[int] = len(ADJUSTMENT_LEVELS) ** 2  # 25
"""Total number of discrete actions (5 CPU levels x 5 memory levels)."""


def decode_action(action: int) -> tuple[int, int]:
    """Map a ``Discrete(25)`` action to CPU and memory adjustment percentages.

    Args:
        action: An integer in ``[0, 25)``. The CPU level is selected by
            ``action // 5`` and the memory level by ``action % 5``.

    Returns:
        A ``(cpu_adjustment_pct, mem_adjustment_pct)`` tuple, each drawn from
        :data:`ADJUSTMENT_LEVELS`.

    Raises:
        ValueError: If ``action`` is outside ``[0, 25)``.
    """
    if not 0 <= action < ACTION_SPACE_SIZE:
        raise ValueError(
            f"action must be in [0, {ACTION_SPACE_SIZE}), got {action}"
        )

    axis_size = len(ADJUSTMENT_LEVELS)
    cpu_adjustment_pct = ADJUSTMENT_LEVELS[action // axis_size]
    mem_adjustment_pct = ADJUSTMENT_LEVELS[action % axis_size]
    return cpu_adjustment_pct, mem_adjustment_pct