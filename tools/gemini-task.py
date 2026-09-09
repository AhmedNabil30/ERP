"""Hand a mechanical task to Gemini instead of a Claude agent.

    python tools/gemini-task.py "<instruction>" <file> [<file> ...]

Reads the key from GEMINI_API_KEY. Requires the graphify venv, which is the only
interpreter here with an openai>=1.0 client:

    .graphify-venv\\Scripts\\python.exe tools/gemini-task.py "..." file.md

WHAT THIS IS FOR, AND WHAT IT IS NOT FOR
----------------------------------------
`agents.md` §M sets the model tier by one question: **is the answer discoverable
by following a rule, or does it require judging a trade-off nobody has written
down?** This script is the small-model tier and answers only the first kind.

Send it:  citation sweeps · i18n key inventories · "which of these files is
missing X" · renames · listing what a register does not contain · counting.

Never send it, per `agents.md` §M's "never downgrade for" list:
  * anything deciding who may touch money, or what the ledger records
  * anything appended to decisions.md AS a decision (recording one is
    bookkeeping; making one is not)
  * a Verifier pass - a cheap verification that misses a defect costs more
    than the defect
  * anything where the agent must REFUSE: a business rule that does not exist,
    a story that contradicts spec.md. Refusing well is the most valuable
    behaviour on this project and the easiest to lose to a weaker model.

Its output is a DRAFT and is never committed unread. The checker, the compiler
or a human confirms it - the same rule that applies to every agent here.

RATE LIMIT
----------
The current key is free-tier: 5 requests/minute and 250,000 input tokens/minute.
Batch the files into one call rather than looping, and keep a call under ~200k
tokens of input - decisions.md alone will not fit.
"""
import os
import sys
from pathlib import Path

MODEL = os.environ.get("GRAPHIFY_GEMINI_MODEL", "gemini-3-flash-preview")


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    key = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")
    if not key:
        print("GEMINI_API_KEY is not set.", file=sys.stderr)
        return 2

    instruction = sys.argv[1]
    paths = [Path(p) for p in sys.argv[2:]]

    parts = [instruction, ""]
    budget = 0
    for p in paths:
        if not p.exists():
            print(f"missing: {p}", file=sys.stderr)
            return 1
        text = p.read_text(encoding="utf-8", errors="replace")
        budget += len(text)
        parts += [f"===== FILE: {p.as_posix()} =====", text, ""]

    # ~4 chars/token, and the free tier caps input at 250k tokens per minute.
    if budget > 700_000:
        print(
            f"refusing: ~{budget//4:,} tokens of input exceeds the free-tier "
            f"per-minute cap. Split the file list.",
            file=sys.stderr,
        )
        return 1

    from openai import OpenAI

    client = OpenAI(
        api_key=key,
        base_url="https://generativelanguage.googleapis.com/v1beta/openai/",
    )
    r = client.chat.completions.create(
        model=MODEL,
        messages=[{"role": "user", "content": "\n".join(parts)}],
    )
    print(r.choices[0].message.content)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
