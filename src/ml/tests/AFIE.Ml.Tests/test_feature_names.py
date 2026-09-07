from afie_ml.feature_names import FEATURE_NAMES, STATE_VECTOR_DIM


def test_exactly_47_names() -> None:
    assert len(FEATURE_NAMES) == STATE_VECTOR_DIM == 47


def test_all_names_non_empty() -> None:
    assert all(name.strip() for name in FEATURE_NAMES)


def test_no_duplicate_names() -> None:
    assert len(set(FEATURE_NAMES)) == len(FEATURE_NAMES)


def test_group_boundary_labels() -> None:
    # Spot-check the first index of each feature group so a reordering
    # within the 47 is caught, not just a length drift.
    assert FEATURE_NAMES[0] == "CPU util P50 (5m)"
    assert FEATURE_NAMES[8] == "CPU util P99 (1h)"
    assert FEATURE_NAMES[9] == "Memory util P50 (5m)"
    assert FEATURE_NAMES[18] == "Request rate (req/s, 5m)"
    assert FEATURE_NAMES[23] == "App signal (reserved)"
    assert FEATURE_NAMES[24] == "Node CPU pressure"
    assert FEATURE_NAMES[27] == "Hourly cost estimate"
    assert FEATURE_NAMES[30] == "Hour of day (sin)"
    assert FEATURE_NAMES[35] == "Replica count (placeholder)"
    assert FEATURE_NAMES[38] == "Last action: cost delta"
    assert FEATURE_NAMES[46] == "3rd-last action: hours since"


def test_action_history_is_most_recent_first() -> None:
    # dims 38-46 must read newest -> oldest to match ActionHistoryFeatures.cs
    assert FEATURE_NAMES[38].startswith("Last action")
    assert FEATURE_NAMES[41].startswith("2nd-last action")
    assert FEATURE_NAMES[44].startswith("3rd-last action")
    