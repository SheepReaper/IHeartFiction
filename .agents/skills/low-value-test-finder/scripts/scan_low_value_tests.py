#!/usr/bin/env python3
"""Heuristic scanner for low-value automated test candidates.

This script intentionally emits candidates, not final judgments. Review the
test body and production code before deleting or rewriting anything.
"""

from __future__ import annotations

import argparse
import json
import os
import re
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Iterable


TEST_FILE_PATTERNS = (
    "*Tests.cs",
    "*Test.cs",
    "*.test.ts",
    "*.test.tsx",
    "*.spec.ts",
    "*.spec.tsx",
    "*.test.js",
    "*.spec.js",
    "test_*.py",
    "*_test.py",
)

EXCLUDED_DIRS = {
    ".git",
    ".agents",
    ".artifacts",
    ".hg",
    ".playwright-cli",
    ".serena",
    ".svn",
    ".tmp",
    "bin",
    "obj",
    "node_modules",
    "dist",
    "build",
    "coverage",
    "data",
    ".next",
    ".venv",
    "venv",
    "test-results",
    "packages",
    "target",
    "out",
    "__pycache__",
}

TEST_ROOT_NAMES = {"test", "tests", "spec", "specs", "__tests__"}

LOW_VALUE_NAME_PATTERNS = [
    (re.compile(r"(constructor|ctor).*(set|assign|init|populate|create)", re.I), "constructor/property echo"),
    (re.compile(r"(get|set|property|properties).*(return|set|assign|value)", re.I), "getter/setter/property echo"),
    (re.compile(r"(default|empty).*(constructor|ctor)", re.I), "default construction smoke"),
    (re.compile(r"(can|should)_(create|instantiate|construct)", re.I), "construction-only smoke"),
    (re.compile(r"canbecreated|can_be_created", re.I), "construction-only smoke"),
    (re.compile(r"recordequality.*works", re.I), "language/compiler-generated equality"),
    (re.compile(r"(not_null|notnull|non_null|nonnull)", re.I), "non-null-only assertion"),
]

ASSERTION_PATTERNS = [
    re.compile(r"\bAssert\."),
    re.compile(r"\bShould\(\)"),
    re.compile(r"\bexpect\s*\("),
    re.compile(r"\bassert\s+"),
    re.compile(r"\bself\.assert"),
    re.compile(r"\bverify\s*\(", re.I),
]

ECHO_ASSERTION_PATTERNS = [
    re.compile(r"Assert\.Equal\s*\(\s*[^,\n]+,\s*\w+\.\w+\s*\)", re.I),
    re.compile(r"\w+\.\w+\.Should\(\)\.Be\s*\(", re.I),
    re.compile(r"expect\s*\([^)]*\.\w+\)\.(toBe|toEqual)\s*\(", re.I),
    re.compile(r"assert\s+\w+\.\w+\s*==", re.I),
]

USE_CASE_INVOCATION_PATTERNS = [
    re.compile(r"\b\w*(?:UseCase|Usecase|Handler|ApplicationService)\b"),
    re.compile(r"\b(?:mediator|sender|dispatcher)\.(?:send|dispatch|execute|handle)\s*\(", re.I),
    re.compile(r"\b(?:useCase|usecase|handler|applicationService)\.(?:execute|handle|run|invoke)\s*\(", re.I),
]

TEST_START_PATTERNS = [
    re.compile(r"^\s*\[(Fact|Theory|TestMethod|Test)\b"),
    re.compile(r"^\s*(public\s+)?(async\s+)?Task\s+\w+\s*\("),
    re.compile(r"^\s*(public\s+)?void\s+\w+\s*\("),
    re.compile(r"^\s*(it|test|describe)\s*\(\s*['\"]"),
    re.compile(r"^\s*def\s+test_\w+\s*\("),
]


@dataclass
class Candidate:
    file: str
    line: int
    end_line: int
    test_name: str
    confidence: str
    classification: str
    removal_action: str
    signals: list[str]
    valuation: dict[str, int]
    justification: str


def seed_roots(root: Path, scan_all: bool) -> list[Path]:
    if scan_all:
        return [root]

    seeds = [
        child
        for child in root.iterdir()
        if child.is_dir() and child.name.lower() in TEST_ROOT_NAMES and child.name not in EXCLUDED_DIRS
    ]
    return seeds or [root]


def iter_test_files(root: Path, max_files: int, scan_all: bool) -> Iterable[Path]:
    pattern_regexes = [
        re.compile("^" + re.escape(pattern).replace(r"\*", ".*").replace(r"\?", ".") + "$")
        for pattern in TEST_FILE_PATTERNS
    ]
    count = 0
    for seed in seed_roots(root, scan_all):
        for dirpath, dirnames, filenames in os.walk(seed):
            dirnames[:] = [name for name in dirnames if name not in EXCLUDED_DIRS]
            for filename in filenames:
                if count >= max_files:
                    return
                if not any(pattern.match(filename) for pattern in pattern_regexes):
                    continue
                path = Path(dirpath) / filename
                count += 1
                yield path


def looks_like_start(line: str) -> bool:
    return any(pattern.search(line) for pattern in TEST_START_PATTERNS)


def extract_name(lines: list[str], start: int) -> str:
    window = "\n".join(lines[start : min(len(lines), start + 4)])
    patterns = [
        re.compile(r"\b(?:Task|void)\s+(\w+)\s*\("),
        re.compile(r"\b(?:it|test|describe)\s*\(\s*['\"]([^'\"]+)"),
        re.compile(r"\bdef\s+(test_\w+)\s*\("),
    ]
    for pattern in patterns:
        match = pattern.search(window)
        if match:
            return match.group(1)
    return f"test_at_line_{start + 1}"


def collect_block(lines: list[str], start: int) -> tuple[int, str]:
    end = min(len(lines), start + 80)
    for index in range(start + 1, min(len(lines), start + 80)):
        if index > start + 2 and looks_like_start(lines[index]):
            end = index
            break
    return end, "\n".join(lines[start:end])


def score_candidate(name: str, body: str) -> tuple[list[str], str, dict[str, int]]:
    signals: list[str] = []
    lowered = body.lower()
    low_value_name = False
    invokes_use_case = any(pattern.search(body) for pattern in USE_CASE_INVOCATION_PATTERNS)

    for pattern, signal in LOW_VALUE_NAME_PATTERNS:
        if pattern.search(name):
            low_value_name = True
            signals.append(signal)

    assertions = sum(1 for pattern in ASSERTION_PATTERNS if pattern.search(body))
    echo_assertions = sum(1 for pattern in ECHO_ASSERTION_PATTERNS if pattern.search(body))

    if assertions == 0:
        signals.append("assertion-free test")
    if echo_assertions and low_value_name:
        signals.append("property echo assertion")
    if low_value_name and re.search(r"\bnew\s+\w+\s*\(", body) and assertions <= 2:
        signals.append("construction with minimal assertion")
    if "doesnotthrow" in lowered or "does_not_raise" in lowered:
        signals.append("does-not-throw smoke")
    if re.search(r"\bmock\b|\bsubstitute\b|\bverify\s*\(", lowered) and assertions <= 1:
        signals.append("interaction-heavy or mock-centered assertion")
    if invokes_use_case:
        signals.append("use case boundary requires unfolding review")

    maintenance_cost = 1 if len(body.splitlines()) <= 25 else 2
    brittleness = 1 if any("mock" in s or "implementation" in s for s in signals) else 0
    duplication = 1 if any("echo" in s or "construction" in s for s in signals) else 0
    boundary_span = 2 if invokes_use_case else 0
    behavior_value = 0 if low_value_name else 1
    defect_detection = 0 if low_value_name else 1
    regression_value = 0

    if any(word in lowered for word in ("invalid", "throws", "exception", "permission", "auth", "serialize", "deserialize")):
        behavior_value += 1
        defect_detection += 1
    if invokes_use_case and any(
        word in lowered
        for word in ("invalid", "throws", "exception", "permission", "auth", "validate", "validation")
    ):
        behavior_value += 1
        defect_detection += 1

    valuation = {
        "behavior_value": behavior_value,
        "defect_detection": defect_detection,
        "regression_value": regression_value,
        "maintenance_cost": maintenance_cost,
        "brittleness": brittleness,
        "duplication": duplication,
        "boundary_span": boundary_span,
    }
    valuation["net_value"] = (
        behavior_value
        + defect_detection
        + regression_value
        - maintenance_cost
        - brittleness
        - duplication
        - boundary_span
    )

    confidence = (
        "high"
        if low_value_name and not invokes_use_case and valuation["net_value"] <= -2 and len(signals) >= 2
        else "medium"
    )
    return signals, confidence, valuation


def scan_file(root: Path, path: Path) -> list[Candidate]:
    text = path.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()
    findings: list[Candidate] = []
    index = 0
    while index < len(lines):
        line = lines[index]
        if not looks_like_start(line):
            index += 1
            continue
        name = extract_name(lines, index)
        end, body = collect_block(lines, index)
        signals, confidence, valuation = score_candidate(name, body)
        if not signals or valuation["net_value"] > 0:
            index = max(index + 1, end)
            continue
        classification = "low-value" if confidence == "high" else "review"
        removal_action = "delete-test" if classification == "low-value" else "review"
        if "use case boundary requires unfolding review" in signals:
            classification = "rewrite-candidate"
            removal_action = "unfold-test"
        findings.append(
            Candidate(
                file=str(path.relative_to(root)).replace("\\", "/"),
                line=index + 1,
                end_line=end,
                test_name=name,
                confidence=confidence,
                classification=classification,
                removal_action=removal_action,
                signals=signals,
                valuation=valuation,
                justification=(
                    "Heuristic candidate: "
                    + "; ".join(signals)
                    + ". Review production behavior before removal."
                ),
            )
        )
        index = max(index + 1, end)
    return findings


def to_markdown(candidates: list[Candidate]) -> str:
    rows = ["| File | Lines | Test | Confidence | Net | Signals |", "|---|---:|---|---|---:|---|"]
    for item in candidates:
        rows.append(
            f"| {item.file} | {item.line}-{item.end_line} | {item.test_name} | "
            f"{item.confidence} | {item.valuation['net_value']} | {', '.join(item.signals)} |"
        )
    return "\n".join(rows)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    parser.add_argument("--format", choices=("json", "markdown"), default="json")
    parser.add_argument("--include-reviewed", action="store_true")
    parser.add_argument("--max-files", type=int, default=2000)
    parser.add_argument("--scan-all", action="store_true", help="scan the whole repository instead of conventional test roots")
    args = parser.parse_args()

    root = args.root.resolve()
    candidates: list[Candidate] = []
    for path in iter_test_files(root, args.max_files, args.scan_all):
        candidates.extend(scan_file(root, path))

    if not args.include_reviewed:
        candidates = [
            item for item in candidates if item.classification in {"low-value", "rewrite-candidate"}
        ]

    candidates.sort(key=lambda item: (item.valuation["net_value"], item.file, item.line))

    if args.format == "json":
        print(json.dumps([asdict(item) for item in candidates], indent=2))
    else:
        print(to_markdown(candidates))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
