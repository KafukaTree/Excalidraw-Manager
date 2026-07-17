#!/usr/bin/env bash
set -euo pipefail

install_root=""
python_bin="python3"
accepted_license="false"

usage() {
  printf '%s\n' \
    'Install the optional local RapidLaTeXOCR provider for Excalidraw Manager.' \
    '' \
    'Usage:' \
    '  ./scripts/install-formula-ocr.sh --install-root PATH [--python PATH] --accept-upstream-model-license' \
    '' \
    'The model weights originate from pix2tex and are licensed CC BY-NC-SA.'
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --install-root)
      [[ $# -ge 2 ]] || { printf 'Missing value for --install-root\n' >&2; exit 2; }
      install_root="$2"
      shift 2
      ;;
    --python)
      [[ $# -ge 2 ]] || { printf 'Missing value for --python\n' >&2; exit 2; }
      python_bin="$2"
      shift 2
      ;;
    --accept-upstream-model-license)
      accepted_license="true"
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      printf 'Unknown argument: %s\n' "$1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ "$accepted_license" != "true" ]]; then
  printf '%s\n' \
    'Installation stopped: explicit model-license acceptance is required.' \
    'The weights originate from pix2tex and are marked CC BY-NC-SA.' \
    'Review https://github.com/lukas-blecher/LaTeX-OCR/releases, then rerun with:' \
    '  --accept-upstream-model-license' >&2
  exit 2
fi

if [[ -z "$install_root" ]]; then
  printf 'Missing required --install-root PATH\n' >&2
  usage >&2
  exit 2
fi
if ! command -v "$python_bin" >/dev/null 2>&1 && [[ ! -x "$python_bin" ]]; then
  printf 'Python was not found: %s\n' "$python_bin" >&2
  exit 2
fi
if ! command -v curl >/dev/null 2>&1; then
  printf 'curl is required to download the pinned model files.\n' >&2
  exit 2
fi

install_root="$($python_bin -c 'import os,sys; print(os.path.abspath(os.path.expanduser(sys.argv[1])))' "$install_root")"
python_facts="$($python_bin -c 'import struct,sys; print(f"{sys.version_info.major}.{sys.version_info.minor}|{struct.calcsize(chr(80))*8}")')"
case "$python_facts" in
  3.10\|64|3.11\|64|3.12\|64) ;;
  *)
    printf 'Formula OCR requires 64-bit Python 3.10-3.12; found %s\n' "$python_facts" >&2
    exit 2
    ;;
esac

venv_root="$install_root/venv"
venv_python="$venv_root/bin/python3"
model_root="$install_root/models/rapid-latex-ocr"
cache_root="$install_root/cache"
temp_root="$install_root/temp"
run_root="$install_root/run"
install_marker="$install_root/install-complete.json"

mkdir -p "$model_root" "$cache_root/pip" "$cache_root/torch" "$cache_root/huggingface" "$temp_root" "$run_root"
if [[ ! -x "$venv_python" ]]; then
  printf 'Creating isolated Python environment in %s\n' "$venv_root"
  "$python_bin" -m venv "$venv_root"
fi

unset PIP_TARGET PIP_PREFIX PIP_INDEX_URL PIP_EXTRA_INDEX_URL PIP_TRUSTED_HOST PIP_NO_INDEX PIP_FIND_LINKS
unset PYTHONHOME PYTHONPATH PYTHONUSERBASE VIRTUAL_ENV
export PIP_CONFIG_FILE=/dev/null
export PIP_REQUIRE_VIRTUALENV=1
export PIP_CACHE_DIR="$cache_root/pip"
export TORCH_HOME="$cache_root/torch"
export HF_HOME="$cache_root/huggingface"
export TEMP="$temp_root"
export TMP="$temp_root"
export TMPDIR="$temp_root"
export PYTHONNOUSERSITE=1
export PYTHONDONTWRITEBYTECODE=1
export PIP_DISABLE_PIP_VERSION_CHECK=1
export PIP_NO_INPUT=1
if [[ "$(uname -s)" == "Darwin" ]] && [[ -f /etc/ssl/cert.pem ]]; then
  # python.org framework builds do not always inherit the current macOS CA path.
  export SSL_CERT_FILE=/etc/ssl/cert.pem
fi

venv_facts="$("$venv_python" -c 'import pathlib,struct,sys; print(f"{sys.version_info.major}.{sys.version_info.minor}|{struct.calcsize(chr(80))*8}|{pathlib.Path(sys.prefix).resolve()}")')"
expected_prefix="$("$venv_python" -c 'import pathlib,sys; print(pathlib.Path(sys.argv[1]).resolve())' "$venv_root")"
if [[ "$venv_facts" != "3.10|64|$expected_prefix" ]] &&
   [[ "$venv_facts" != "3.11|64|$expected_prefix" ]] &&
   [[ "$venv_facts" != "3.12|64|$expected_prefix" ]]; then
  printf 'The virtual environment is incompatible or resolves outside the install root: %s\n' "$venv_facts" >&2
  exit 2
fi

# Python 3.11.0 framework installations can contain pip 22.3, whose vendored
# TLS stack fails against current PyPI on macOS. Bootstrap a verified modern
# pip wheel with curl before asking pip to access the index.
pip_wheel="$cache_root/pip/pip-25.3-py3-none-any.whl"
pip_wheel_sha='9655943313a94722b7774661c21049070f6bbb0a1516bf02f7c8d5d9201514cd'
if [[ ! -f "$pip_wheel" ]] ||
   [[ "$("$venv_python" -c 'import hashlib,pathlib,sys; print(hashlib.sha256(pathlib.Path(sys.argv[1]).read_bytes()).hexdigest())' "$pip_wheel")" != "$pip_wheel_sha" ]]; then
  rm -f "$pip_wheel"
  printf 'Downloading verified pip 25.3 bootstrap wheel\n'
  curl --fail --location --proto '=https' --tlsv1.2 \
    --output "$pip_wheel" \
    'https://files.pythonhosted.org/packages/44/3c/d717024885424591d5376220b5e836c2d5293ce2011523c9de23ff7bf068/pip-25.3-py3-none-any.whl'
fi
downloaded_pip_sha="$("$venv_python" -c 'import hashlib,pathlib,sys; print(hashlib.sha256(pathlib.Path(sys.argv[1]).read_bytes()).hexdigest())' "$pip_wheel")"
if [[ "$downloaded_pip_sha" != "$pip_wheel_sha" ]]; then
  printf 'Downloaded pip bootstrap SHA-256 mismatch.\n' >&2
  exit 1
fi
"$venv_python" -m pip install --no-index "$pip_wheel"

packages=(
  'numpy==1.26.4'
  'opencv-python-headless==4.10.0.84'
  'onnxruntime==1.19.2'
  'tokenizers==0.22.2'
  'Pillow==12.3.0'
  'PyYAML==6.0.3'
  'chardet==5.2.0'
  'requests==2.32.5'
  'tqdm==4.67.1'
)

printf 'Installing pinned CPU runtime packages. Caches remain under %s\n' "$install_root"
"$venv_python" -m pip install --index-url 'https://pypi.org/simple' --only-binary=:all: "${packages[@]}"
"$venv_python" -m pip install --index-url 'https://pypi.org/simple' --only-binary=:all: --no-deps 'rapid-latex-ocr==0.0.9'

assets=(
  'decoder.onnx|50952726|bd695497bf1b22279b7626f5916c79226e1e244c84355f8da7edfd2d921d0072|https://github.com/RapidAI/RapidLaTeXOCR/releases/download/v0.0.0/decoder.onnx'
  'encoder.onnx|89008136|01bf5dc25539ca0cd5b1bd29296ea495977a6ba5f629dc4178277809d26e5e7d|https://github.com/RapidAI/RapidLaTeXOCR/releases/download/v0.0.0/encoder.onnx'
  'image_resizer.onnx|38967751|e0b075c39700f64d50400f39c8fc186bbb3b5d84d31864008313f376603aca9d|https://github.com/RapidAI/RapidLaTeXOCR/releases/download/v0.0.0/image_resizer.onnx'
  'tokenizer.json|24174|1dc27b18d6a518d0d5ff3f4bb7bd98521fe80ad39e5b2a246d4109f1bb9d5019|https://github.com/RapidAI/RapidLaTeXOCR/releases/download/v0.0.0/tokenizer.json'
)

file_size() {
  if stat -f '%z' "$1" >/dev/null 2>&1; then
    stat -f '%z' "$1"
  else
    stat -c '%s' "$1"
  fi
}

file_sha256() {
  if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print $1}'
  else
    sha256sum "$1" | awk '{print $1}'
  fi
}

for row in "${assets[@]}"; do
  IFS='|' read -r name expected_size expected_sha url <<< "$row"
  destination="$model_root/$name"
  if [[ -f "$destination" ]] && [[ "$(file_size "$destination")" == "$expected_size" ]] && [[ "$(file_sha256 "$destination")" == "$expected_sha" ]]; then
    printf 'Model file already present: %s\n' "$name"
    continue
  fi
  if [[ -f "$destination" ]]; then
    mv "$destination" "$destination.invalid-$(date -u +%Y%m%d%H%M%S)"
  fi
  partial="$destination.part"
  rm -f "$partial"
  printf 'Downloading %s\n' "$name"
  curl --fail --location --proto '=https' --tlsv1.2 --output "$partial" "$url"
  if [[ "$(file_size "$partial")" != "$expected_size" ]]; then
    printf 'Downloaded size mismatch for %s\n' "$name" >&2
    exit 1
  fi
  downloaded_sha="$(file_sha256 "$partial")"
  if [[ "$downloaded_sha" != "$expected_sha" ]]; then
    rm -f "$partial"
    printf 'Downloaded SHA-256 mismatch for %s\n' "$name" >&2
    exit 1
  fi
  mv "$partial" "$destination"
done

"$venv_python" - "$model_root" "$install_marker" "$install_root" "$venv_python" <<'PY'
import datetime
import hashlib
import json
import pathlib
import sys
import onnxruntime
import PIL
import rapid_latex_ocr
import tokenizers

model_root = pathlib.Path(sys.argv[1])
install_marker = pathlib.Path(sys.argv[2])
install_root = pathlib.Path(sys.argv[3])
venv_python = pathlib.Path(sys.argv[4])
venv_root = pathlib.Path(sys.prefix).resolve()
for module in (onnxruntime, PIL, rapid_latex_ocr, tokenizers):
    if not pathlib.Path(module.__file__).resolve().is_relative_to(venv_root):
        raise SystemExit(f"Runtime module escaped the isolated environment: {module.__name__}")
expected = {
    "decoder.onnx": (50952726, "bd695497bf1b22279b7626f5916c79226e1e244c84355f8da7edfd2d921d0072"),
    "encoder.onnx": (89008136, "01bf5dc25539ca0cd5b1bd29296ea495977a6ba5f629dc4178277809d26e5e7d"),
    "image_resizer.onnx": (38967751, "e0b075c39700f64d50400f39c8fc186bbb3b5d84d31864008313f376603aca9d"),
    "tokenizer.json": (24174, "1dc27b18d6a518d0d5ff3f4bb7bd98521fe80ad39e5b2a246d4109f1bb9d5019"),
}
rows = []
for name in sorted(expected):
    path = model_root / name
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    size, pinned = expected[name]
    if path.stat().st_size != size or digest != pinned:
        raise SystemExit(f"Installed model verification failed for {name}")
    rows.append({"name": name, "size": size, "sha256": digest})
revision_input = "\n".join(f"{row['name']}:{row['sha256']}" for row in rows)
revision = "sha256:" + hashlib.sha256(revision_input.encode()).hexdigest()
manifest = {
    "provider": "rapid-latex-ocr",
    "packageVersion": "0.0.9",
    "modelVersion": "v0.0.0",
    "modelSource": "https://github.com/RapidAI/RapidLaTeXOCR/releases/tag/v0.0.0",
    "modelLicense": "CC-BY-NC-SA (upstream pix2tex weights; review upstream terms)",
    "revision": revision,
    "files": rows,
}
(model_root / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
completed = {
    "schemaVersion": 1,
    "completedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "installRoot": str(install_root),
    "python": str(venv_python),
    "packageVersion": "0.0.9",
    "modelVersion": "v0.0.0",
    "modelRevision": revision,
}
install_marker.write_text(json.dumps(completed, indent=2) + "\n", encoding="utf-8")
print(revision)
PY

printf '\nFormula OCR installation completed.\n'
printf 'Root: %s\n' "$install_root"
printf 'Python: %s\n' "$venv_python"
printf 'Models: %s\n' "$model_root"
printf 'Choose this root in Excalidraw Manager > Settings > Local formula OCR.\n'
