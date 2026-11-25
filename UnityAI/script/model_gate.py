"""Serialize access to the shared ONNX model."""
import threading
from contextlib import contextmanager


class ModelGate:
    def __init__(self):
        self._lock = threading.Lock()

    @contextmanager
    def use(self, timeout=30):
        got = self._lock.acquire(timeout=timeout)
        try:
            yield got
        finally:
            if got:
                self._lock.release()
