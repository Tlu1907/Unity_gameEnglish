"""Download the pinned Qwen3 ONNX INT4 export without removing older models."""

from pathlib import Path

from huggingface_hub import snapshot_download


REPO_ID = "robinsmits/qwen3-4b-instruct-2507-onnx-int4"
REVISION = "97d8690faf0cad28279dd10334a67c79f673a57f"
DESTINATION = Path(__file__).resolve().parents[1] / "models" / "qwen3-4b-instruct-2507-onnx-int4"


def main():
    snapshot_download(
        repo_id=REPO_ID,
        revision=REVISION,
        local_dir=str(DESTINATION),
        allow_patterns=["*.json", "*.jinja", "*.txt", "*.onnx", "*.onnx.data", "README.md"],
        max_workers=2,
    )
    print(f"Model downloaded to: {DESTINATION}")


if __name__ == "__main__":
    main()
