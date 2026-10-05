import numpy as np
import pytest
import requests
from pytest_httpserver import HTTPServer

from afie_ml.env_live import AFIEEnv
from afie_ml.feature_names import STATE_VECTOR_DIM


def _serve(httpserver: HTTPServer, workload: str, vec: list[float]) -> AFIEEnv:
    httpserver.expect_request(f"/state/{workload}").respond_with_json(vec)
    return AFIEEnv(workload, fe_base_url=httpserver.url_for("/"))


def test_spaces(httpserver: HTTPServer) -> None:
    env = _serve(httpserver, "nginx", [0.1] * STATE_VECTOR_DIM)
    assert env.observation_space.shape == (STATE_VECTOR_DIM,)
    assert env.action_space.n == 25


def test_reset_fetches_state(httpserver: HTTPServer) -> None:
    env = _serve(httpserver, "nginx", [0.1] * STATE_VECTOR_DIM)
    obs, info = env.reset()
    assert obs.shape == (STATE_VECTOR_DIM,)
    assert obs.dtype == np.float32
    assert info == {}


def test_step_returns_gymnasium_5_tuple(httpserver: HTTPServer) -> None:
    env = _serve(httpserver, "nginx", [0.1] * STATE_VECTOR_DIM)
    env.reset()
    obs, reward, terminated, truncated, info = env.step(12)
    assert obs.shape == (STATE_VECTOR_DIM,)
    assert isinstance(reward, float)
    assert terminated is False and truncated is False
    assert info["cpu_adjustment_pct"] == 0


def test_live_vector_clipped_to_unit_range(httpserver: HTTPServer) -> None:
    env = _serve(httpserver, "hot", [2.0] * STATE_VECTOR_DIM)
    obs, _ = env.reset()
    assert np.all(obs <= 1.0) and np.all(obs >= -1.0)


def test_wrong_length_raises(httpserver: HTTPServer) -> None:
    env = _serve(httpserver, "bad", [0.0, 1.0, 2.0])
    with pytest.raises(ValueError):
        env.reset()


def test_http_error_propagates(httpserver: HTTPServer) -> None:
    httpserver.expect_request("/state/missing").respond_with_data("nope", status=404)
    env = AFIEEnv("missing", fe_base_url=httpserver.url_for("/"))
    with pytest.raises(requests.HTTPError):
        env.reset()