"""Shared ONNX inference and validated JSON responses for NPC dialogue."""
import json
import os
import re
from pathlib import Path
from time import perf_counter

MODEL_DIR = Path(
    os.environ.get(
        "MODEL_DIR",
        Path(__file__).resolve().parents[1] / "models" / "qwen3-4b-instruct-2507-onnx-int4"
    )
)


class OutputError(ValueError):
    """Unusable or internally inconsistent model output."""


def parse_object(raw):
    # Tolerate one enclosing Markdown fence, but reject surrounding prose.
    raw = raw.strip()
    if raw.startswith("```") and raw.endswith("```"):
        raw = re.sub(r"^```(?:json)?\s*", "", raw, count=1).removesuffix("```").strip()
    try:
        result = json.loads(raw)
    except json.JSONDecodeError as error:
        raise OutputError(f"JSON khong hop le: {error.msg}") from error
    if not isinstance(result, dict):
        raise OutputError("Phan hoi phai la mot JSON object")
    return result


class EnglishModel:
    def __init__(self, model_dir):
        import onnxruntime_genai as og

        self.og = og
        if not (model_dir / "genai_config.json").is_file():
            raise FileNotFoundError(f"Khong tim thay genai_config.json: {model_dir}")
        print("Dang nap model...", flush=True)
        started = perf_counter()
        self.model = og.Model(str(model_dir))
        self.tokenizer = og.Tokenizer(self.model)
        print(f"Nap thanh cong: {perf_counter() - started:.2f} giay", flush=True)

    def generate(self, system_prompt, user_prompt, max_new_tokens=600,
                 sample=False, temperature=0.7, top_p=0.9):
        prompt = (
            f"<|im_start|>system\n{system_prompt}<|im_end|>\n"
            f"<|im_start|>user\n{user_prompt}<|im_end|>\n"
            "<|im_start|>assistant\n"
        )
        tokens = self.tokenizer.encode(prompt)
        params = self.og.GeneratorParams(self.model)
        if sample:
            params.set_search_options(max_length=len(tokens) + max_new_tokens,
                                      do_sample=True, temperature=temperature,
                                      top_p=top_p, top_k=40)
        else:
            params.set_search_options(max_length=len(tokens) + max_new_tokens,
                                      do_sample=False)
        generator = self.og.Generator(self.model, params)
        started = perf_counter()
        try:
            generator.append_tokens(tokens)
            while not generator.is_done():
                generator.generate_next_token()
            output_ids = generator.get_sequence(0)[len(tokens):].copy()
            raw = self.tokenizer.decode(output_ids)
        finally:
            del generator
        for suffix in ("<|im_end|>", "<|endoftext|>"):
            raw = raw.strip().removesuffix(suffix).strip()
        stats = {"seconds": round(perf_counter() - started, 2),
                 "output_tokens": len(output_ids)}
        return raw, stats, len(output_ids) >= max_new_tokens

    def request_json(self, system_prompt, payload, validator,
                     max_new_tokens=600, sample=False):
        request = payload if isinstance(payload, str) else json.dumps(payload, ensure_ascii=False)
        attempts = []
        for attempt in range(2):
            print(f"  Dang xu ly, lan {attempt + 1}/2...", flush=True)
            raw, stats, truncated = self.generate(
                system_prompt, request, max_new_tokens=max_new_tokens, sample=sample)
            try:
                if truncated:
                    raise OutputError("Phan hoi cham gioi han token")
                data = parse_object(raw)
                validator(data)
                return {"status": "validated", "data": data,
                        "attempts": attempts + [{"stats": stats}]}
            except OutputError as error:
                attempts.append({"error": str(error), "stats": stats,
                                 "raw_output": raw})
                print(f"  Chua chap nhan ket qua: {error}", flush=True)
                request = json.dumps({
                    "original_task": payload,
                    "retry_instruction": "Re-evaluate the original task. Return a complete JSON object.",
                    "previous_validation_error": str(error),
                }, ensure_ascii=False)
        return {"status": "review_required", "data": None, "attempts": attempts}
