import afie_ml


def test_package_importable() -> None:
    assert afie_ml.__version__ == "0.1.0"