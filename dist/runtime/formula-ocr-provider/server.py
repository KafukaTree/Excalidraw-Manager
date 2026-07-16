#!/usr/bin/env python3
"""Loopback-only Formula OCR Provider for RapidLaTeXOCR.

The service intentionally receives explicit model paths. It never downloads a
checkpoint and never accepts a local path or URL from an API request.
"""

from __future__ import annotations

import argparse
import base64
import binascii
import ctypes
import hashlib
import hmac
import importlib.util
import io
import json
import os
import signal
import sys
import threading
import time
import uuid
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any


API_VERSION = "1.0"
PROVIDER_VERSION = "0.1.0"
MODEL_ID = "rapid-latex-ocr-onnx"
MODEL_VERSION = "v0.0.0"
ENGINE_VERSION = "0.0.9"
MAX_BODY_BYTES = 20 * 1024 * 1024
MAX_IMAGE_BYTES = 12 * 1024 * 1024
MAX_IMAGE_WIDTH = 8192
MAX_IMAGE_HEIGHT = 8192
MAX_IMAGE_PIXELS = 40_000_000
SUPPORTED_MEDIA = {"image/png", "image/jpeg", "image/webp"}
MODEL_FILES = ("image_resizer.onnx", "encoder.onnx", "decoder.onnx", "tokenizer.json")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


class ApiError(Exception):
    def __init__(self, status: int, code: str, message: str, retryable: bool = False, details: Any = None):
        super().__init__(message)
        self.status = status
        self.code = code
        self.message = message
        self.retryable = retryable
        self.details = details


class ModelState:
    def __init__(self, model_root: Path):
        self.model_root = model_root.resolve()
        self.paths = {name: self.model_root / name for name in MODEL_FILES}
        self.engine = None
        self.load_lock = threading.Lock()
        self.inference_lock = threading.Lock()
        self.loaded_at = None
        self.revision = self._revision()
        self.integrity_issues = self.integrity_errors()

    def missing_files(self) -> list[str]:
        return [name for name, path in self.paths.items() if not path.is_file()]

    @staticmethod
    def missing_runtime() -> list[str]:
        return [name for name in ("rapid_latex_ocr", "onnxruntime", "PIL") if importlib.util.find_spec(name) is None]

    def _revision(self) -> str:
        manifest_path = self.model_root / "manifest.json"
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            value = manifest.get("revision")
            if isinstance(value, str) and value.startswith("sha256:") and len(value) == 71:
                return value
        except (OSError, ValueError, TypeError):
            pass
        digest = hashlib.sha256()
        for name in MODEL_FILES:
            path = self.paths[name]
            digest.update(name.encode("utf-8"))
            try:
                stat = path.stat()
                digest.update(str(stat.st_size).encode("ascii"))
            except OSError:
                digest.update(b"missing")
        return f"sha256:{digest.hexdigest()}"

    @staticmethod
    def _file_sha256(path: Path) -> str:
        digest = hashlib.sha256()
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
        return digest.hexdigest()

    def integrity_errors(self) -> list[dict[str, str]]:
        manifest_path = self.model_root / "manifest.json"
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            entries = manifest.get("files")
            if not isinstance(entries, list):
                raise ValueError("files must be an array")
            expected = {
                item.get("name"): item
                for item in entries
                if isinstance(item, dict) and isinstance(item.get("name"), str)
            }
        except (OSError, ValueError, TypeError, json.JSONDecodeError):
            return [{"file": "manifest.json", "reason": "missing or invalid"}]

        errors = []
        revision_rows = []
        for name in sorted(MODEL_FILES):
            entry = expected.get(name)
            path = self.paths[name]
            if not entry:
                errors.append({"file": name, "reason": "not listed in manifest"})
                continue
            expected_hash = entry.get("sha256")
            expected_size = entry.get("size")
            if not isinstance(expected_hash, str) or len(expected_hash) != 64:
                errors.append({"file": name, "reason": "invalid manifest hash"})
                continue
            try:
                if path.stat().st_size != int(expected_size):
                    errors.append({"file": name, "reason": "size mismatch"})
                    continue
                actual_hash = self._file_sha256(path)
            except (OSError, TypeError, ValueError):
                errors.append({"file": name, "reason": "cannot be verified"})
                continue
            if not hmac.compare_digest(actual_hash, expected_hash.lower()):
                errors.append({"file": name, "reason": "SHA-256 mismatch"})
                continue
            revision_rows.append(f"{name}:{actual_hash}")

        if not errors:
            actual_revision = "sha256:" + hashlib.sha256("\n".join(revision_rows).encode("utf-8")).hexdigest()
            if not hmac.compare_digest(actual_revision, str(manifest.get("revision", "")).lower()):
                errors.append({"file": "manifest.json", "reason": "revision mismatch"})
        return errors

    def load(self):
        if self.engine is not None:
            return self.engine
        with self.load_lock:
            if self.engine is not None:
                return self.engine
            missing = self.missing_files()
            if missing:
                raise ApiError(503, "MODEL_UNAVAILABLE", "The local formula model is not installed.", False, {"missing": missing})
            integrity_errors = self.integrity_issues
            if integrity_errors:
                raise ApiError(
                    503,
                    "MODEL_UNAVAILABLE",
                    "The local formula model failed its integrity check.",
                    False,
                    {"integrityErrors": integrity_errors},
                )
            started = time.perf_counter()
            try:
                from rapid_latex_ocr import LaTeXOCR

                self.engine = LaTeXOCR(
                    image_resizer_path=self.paths["image_resizer.onnx"],
                    encoder_path=self.paths["encoder.onnx"],
                    decoder_path=self.paths["decoder.onnx"],
                    tokenizer_json=self.paths["tokenizer.json"],
                )
            except ApiError:
                raise
            except Exception as exc:
                raise ApiError(503, "MODEL_UNAVAILABLE", "The local formula model could not be loaded.", True) from exc
            self.loaded_at = utc_now()
            self.load_elapsed_ms = round((time.perf_counter() - started) * 1000, 2)
            return self.engine


def image_magic_matches(data: bytes, media_type: str) -> bool:
    if media_type == "image/png":
        return data.startswith(b"\x89PNG\r\n\x1a\n")
    if media_type == "image/jpeg":
        return data.startswith(b"\xff\xd8\xff")
    if media_type == "image/webp":
        return len(data) >= 12 and data[:4] == b"RIFF" and data[8:12] == b"WEBP"
    return False


def validate_image(data: bytes, media_type: str) -> None:
    if not image_magic_matches(data, media_type):
        raise ApiError(415, "UNSUPPORTED_MEDIA_TYPE", "The declared image type does not match the image bytes.")
    try:
        from PIL import Image

        with Image.open(io.BytesIO(data)) as image:
            width, height = image.size
            image.verify()
    except Exception as exc:
        raise ApiError(400, "INVALID_REQUEST", "The image cannot be decoded.") from exc
    if width < 1 or height < 1 or width > MAX_IMAGE_WIDTH or height > MAX_IMAGE_HEIGHT or width * height > MAX_IMAGE_PIXELS:
        raise ApiError(413, "INPUT_TOO_LARGE", "The image dimensions exceed the provider limit.")


class ProviderServer(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = False

    def __init__(self, address, handler, state: ModelState, token: str):
        super().__init__(address, handler)
        self.state = state
        self.token = token


class Handler(BaseHTTPRequestHandler):
    server_version = "ExcalidrawManagerFormulaOCR/0.1"
    sys_version = ""

    def log_message(self, _format: str, *_args: Any) -> None:
        return

    def _request_id(self, payload: Any = None) -> str:
        if isinstance(payload, dict) and isinstance(payload.get("requestId"), str) and payload["requestId"]:
            return payload["requestId"][:128]
        return f"local-{uuid.uuid4().hex}"

    def _authorized(self) -> bool:
        expected = self.server.token
        supplied = self.headers.get("Authorization", "")
        prefix = "Bearer "
        return bool(expected) and supplied.startswith(prefix) and hmac.compare_digest(supplied[len(prefix):], expected)

    def _check_request(self) -> None:
        host = self.headers.get("Host", "")
        host_name = host.rsplit(":", 1)[0].strip("[]").lower()
        if host_name not in {"127.0.0.1", "localhost", "::1"}:
            raise ApiError(421, "HOST_NOT_ALLOWED", "Only loopback Host headers are accepted.")
        if self.headers.get("Origin"):
            raise ApiError(403, "ORIGIN_NOT_ALLOWED", "Browser cross-origin requests are not accepted.")
        if not self._authorized():
            raise ApiError(401, "UNAUTHORIZED", "A valid local provider token is required.")

    def _send_json(self, status: int, payload: Any) -> None:
        data = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.end_headers()
        self.wfile.write(data)

    def _send_error(self, error: ApiError, request_id: str | None = None) -> None:
        self._send_json(error.status, {
            "requestId": request_id or self._request_id(),
            "error": {
                "code": error.code,
                "message": error.message,
                "retryable": error.retryable,
                "details": error.details,
            },
        })

    def _read_json(self) -> dict[str, Any]:
        content_type = self.headers.get("Content-Type", "").lower()
        if not content_type.startswith("application/json"):
            raise ApiError(415, "UNSUPPORTED_MEDIA_TYPE", "Requests must use application/json.")
        try:
            length = int(self.headers.get("Content-Length", ""))
        except ValueError as exc:
            raise ApiError(400, "INVALID_REQUEST", "A valid Content-Length is required.") from exc
        if length < 0 or length > MAX_BODY_BYTES:
            raise ApiError(413, "INPUT_TOO_LARGE", "The request body exceeds 20 MiB.")
        try:
            value = json.loads(self.rfile.read(length).decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise ApiError(400, "INVALID_REQUEST", "The request body is not valid UTF-8 JSON.") from exc
        if not isinstance(value, dict):
            raise ApiError(400, "INVALID_REQUEST", "The request body must be a JSON object.")
        return value

    def do_GET(self) -> None:
        try:
            self._check_request()
            if self.path == "/v1/info":
                self._send_json(200, self._info())
                return
            if self.path == "/v1/health":
                loaded = self.server.state.engine is not None
                missing = self.server.state.missing_files()
                runtime_missing = self.server.state.missing_runtime()
                integrity_errors = self.server.state.integrity_issues
                self._send_json(200, {
                    "requestId": self._request_id(),
                    "status": "ok" if not missing and not runtime_missing and not integrity_errors else "unavailable",
                    "ready": loaded,
                    "loadedModels": [MODEL_ID] if loaded else [],
                    "device": {"type": "cpu", "name": "ONNX Runtime CPU", "precision": "fp32"},
                    "timestamp": utc_now(),
                    "warnings": ((["Model files are not installed."] if missing else []) +
                                 (["Python runtime packages are not installed."] if runtime_missing else []) +
                                 (["Model integrity verification failed."] if integrity_errors else [])),
                    "integrityErrors": integrity_errors,
                })
                return
            raise ApiError(404, "NOT_FOUND", "Unknown provider endpoint.")
        except ApiError as error:
            self._send_error(error)
        except Exception:
            self._send_error(ApiError(500, "INTERNAL_ERROR", "The provider encountered an internal error.", True))

    def do_POST(self) -> None:
        request_id = None
        try:
            self._check_request()
            payload = self._read_json()
            request_id = self._request_id(payload)
            if self.path == "/v1/warmup":
                self._warmup(payload, request_id)
                return
            if self.path == "/v1/recognize":
                self._recognize(payload, request_id)
                return
            raise ApiError(404, "NOT_FOUND", "Unknown provider endpoint.")
        except ApiError as error:
            self._send_error(error, request_id)
        except Exception:
            self._send_error(ApiError(500, "INTERNAL_ERROR", "The provider encountered an internal error.", True), request_id)

    def _info(self) -> dict[str, Any]:
        return {
            "apiVersion": API_VERSION,
            "provider": {
                "id": "org.excalidraw-manager.rapid-latex-ocr",
                "name": "RapidLaTeXOCR Local",
                "version": PROVIDER_VERSION,
                "license": "MIT",
                "homepage": "https://github.com/RapidAI/RapidLaTeXOCR",
                "engine": {"package": "rapid-latex-ocr", "version": ENGINE_VERSION},
            },
            "models": [{
                "id": MODEL_ID,
                "name": "RapidLaTeXOCR ONNX",
                "version": MODEL_VERSION,
                "revision": self.server.state.revision,
                "license": "CC-BY-NC-SA (upstream pix2tex weights)",
                "modes": ["formula"],
                "devices": ["cpu"],
            }],
            "defaults": {"formulaModel": MODEL_ID, "documentModel": None, "device": "cpu"},
            "capabilities": {
                "modes": ["formula"],
                "inputMediaTypes": sorted(SUPPORTED_MEDIA),
                "outputFormats": ["latex"],
                "languageHints": False,
                "regions": False,
                "multipleCandidates": False,
                "confidence": False,
                "debugMetadata": False,
                "warmup": True,
            },
            "limits": {
                "maxInputBytes": MAX_IMAGE_BYTES,
                "maxWidth": MAX_IMAGE_WIDTH,
                "maxHeight": MAX_IMAGE_HEIGHT,
                "maxCandidates": 1,
                "maxConcurrentRequests": 1,
            },
        }

    def _validate_model_request(self, payload: dict[str, Any]) -> None:
        if payload.get("modelId") not in (None, "", MODEL_ID):
            raise ApiError(404, "MODEL_NOT_FOUND", "The requested model is not installed.")
        if payload.get("device") not in (None, "", "auto", "cpu"):
            raise ApiError(422, "DEVICE_UNSUPPORTED", "RapidLaTeXOCR 0.0.9 supports CPU inference in this provider.")

    def _warmup(self, payload: dict[str, Any], request_id: str) -> None:
        self._validate_model_request(payload)
        started = time.perf_counter()
        self.server.state.load()
        self._send_json(200, {
            "requestId": request_id,
            "status": "ready",
            "model": {"id": MODEL_ID, "version": MODEL_VERSION, "revision": self.server.state.revision},
            "device": {"type": "cpu", "precision": "fp32"},
            "elapsedMs": round((time.perf_counter() - started) * 1000, 2),
            "warnings": [],
        })

    def _recognize(self, payload: dict[str, Any], request_id: str) -> None:
        self._validate_model_request(payload)
        if payload.get("mode") != "formula":
            raise ApiError(422, "MODE_UNSUPPORTED", "This provider recognizes one formula image at a time.")
        input_value = payload.get("input")
        if not isinstance(input_value, dict) or input_value.get("kind") != "image":
            raise ApiError(400, "INVALID_REQUEST", "input.kind must be image.")
        media_type = input_value.get("mediaType")
        if media_type not in SUPPORTED_MEDIA:
            raise ApiError(415, "UNSUPPORTED_MEDIA_TYPE", "Only PNG, JPEG and WebP images are supported.")
        encoded = input_value.get("dataBase64")
        if not isinstance(encoded, str) or not encoded:
            raise ApiError(400, "INVALID_REQUEST", "input.dataBase64 is required.")
        if len(encoded) > (MAX_IMAGE_BYTES * 4 // 3) + 8:
            raise ApiError(413, "INPUT_TOO_LARGE", "The image exceeds 12 MiB.")
        try:
            data = base64.b64decode(encoded, validate=True)
        except (binascii.Error, ValueError) as exc:
            raise ApiError(400, "INVALID_REQUEST", "input.dataBase64 is invalid.") from exc
        if len(data) > MAX_IMAGE_BYTES:
            raise ApiError(413, "INPUT_TOO_LARGE", "The image exceeds 12 MiB.")
        expected_hash = input_value.get("sha256")
        if expected_hash and (not isinstance(expected_hash, str) or not hmac.compare_digest(hashlib.sha256(data).hexdigest(), expected_hash.lower())):
            raise ApiError(400, "INVALID_REQUEST", "The image SHA-256 does not match.")
        validate_image(data, media_type)

        if not self.server.state.inference_lock.acquire(blocking=False):
            raise ApiError(429, "PROVIDER_BUSY", "The local formula model is already processing another image.", True)
        started = time.perf_counter()
        try:
            engine = self.server.state.load()
            inference_started = time.perf_counter()
            latex, reported_elapsed = engine(data)
            inference_ms = round((time.perf_counter() - inference_started) * 1000, 2)
        except ApiError:
            raise
        except Exception as exc:
            raise ApiError(422, "RECOGNITION_FAILED", "The image could not be recognized as a formula.", False) from exc
        finally:
            self.server.state.inference_lock.release()
        latex = str(latex or "").strip()
        if not latex:
            raise ApiError(422, "NO_FORMULA_DETECTED", "No formula was detected in the image.")
        total_ms = round((time.perf_counter() - started) * 1000, 2)
        self._send_json(200, {
            "requestId": request_id,
            "mode": "formula",
            "candidates": [{"id": "candidate-0", "latex": latex, "confidence": None, "formats": {}, "warnings": []}],
            "provider": {"id": "org.excalidraw-manager.rapid-latex-ocr", "version": PROVIDER_VERSION},
            "model": {"id": MODEL_ID, "version": MODEL_VERSION, "revision": self.server.state.revision},
            "timing": {
                "queueMs": 0,
                "preprocessMs": None,
                "inferenceMs": inference_ms,
                "postprocessMs": None,
                "totalMs": total_ms,
                "upstreamReportedMs": round(float(reported_elapsed) * 1000, 2),
            },
            "device": {"type": "cpu", "precision": "fp32"},
            "debug": None,
        })


def parent_is_alive(parent_pid: int) -> bool:
    """Check a process without signalling it.

    On Windows, ``os.kill(pid, 0)`` is not a harmless existence check: CPython
    routes signal 0 through TerminateProcess. Use a waitable process handle
    instead so the manager can never be terminated by its OCR child.
    """
    if os.name == "nt":
        synchronize = 0x00100000
        wait_object_0 = 0x00000000
        wait_timeout = 0x00000102
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel32.OpenProcess.argtypes = (ctypes.c_uint32, ctypes.c_int, ctypes.c_uint32)
        kernel32.OpenProcess.restype = ctypes.c_void_p
        kernel32.WaitForSingleObject.argtypes = (ctypes.c_void_p, ctypes.c_uint32)
        kernel32.WaitForSingleObject.restype = ctypes.c_uint32
        kernel32.CloseHandle.argtypes = (ctypes.c_void_p,)
        kernel32.CloseHandle.restype = ctypes.c_int
        handle = kernel32.OpenProcess(synchronize, False, parent_pid)
        if not handle:
            # Access denied means the process exists but cannot be inspected.
            return ctypes.get_last_error() == 5
        try:
            result = kernel32.WaitForSingleObject(handle, 0)
            if result == wait_timeout:
                return True
            if result == wait_object_0:
                return False
            return True
        finally:
            kernel32.CloseHandle(handle)

    try:
        os.kill(parent_pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def watch_parent(parent_pid: int, server: ThreadingHTTPServer) -> None:
    if parent_pid <= 0 or parent_pid == os.getpid():
        return

    def run() -> None:
        while True:
            time.sleep(3)
            if not parent_is_alive(parent_pid):
                threading.Thread(target=server.shutdown, daemon=True).start()
                return

    threading.Thread(target=run, name="parent-watch", daemon=True).start()


def main() -> int:
    parser = argparse.ArgumentParser(description="Excalidraw Manager local formula OCR provider")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--model-root", type=Path, required=True)
    parser.add_argument("--parent-pid", type=int, default=0)
    parser.add_argument("--token-env", default="EXCALIDRAW_MANAGER_FORMULA_OCR_TOKEN")
    args = parser.parse_args()
    if args.host not in {"127.0.0.1", "::1"}:
        parser.error("--host must be a loopback address")
    if not 1 <= args.port <= 65535:
        parser.error("--port must be between 1 and 65535")
    if not args.token_env.replace("_", "a").isalnum() or args.token_env[0].isdigit():
        parser.error("--token-env is invalid")
    token = os.environ.get(args.token_env, "")
    if len(token) < 32:
        parser.error("provider token is missing or too short")

    server = ProviderServer((args.host, args.port), Handler, ModelState(args.model_root), token)
    watch_parent(args.parent_pid, server)

    def stop(_signum, _frame):
        threading.Thread(target=server.shutdown, daemon=True).start()

    signal.signal(signal.SIGTERM, stop)
    if hasattr(signal, "SIGINT"):
        signal.signal(signal.SIGINT, stop)
    print(f"Formula OCR provider ready at http://{args.host}:{args.port}", flush=True)
    try:
        server.serve_forever(poll_interval=0.25)
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
