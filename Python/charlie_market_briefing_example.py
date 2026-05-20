#!/usr/bin/env python3
"""
Minimal example client for the darwintIQ Charlie public API.

Default use case:
- workflow: market_briefing
- symbol: DAX
- prompt: "Give me a trader's desk briefing on DAX. What is actually driving
  the current tone?"

Authentication:
- set DARWINTIQ_API_TOKEN in the environment
- or pass --token on the command line
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from typing import Any


DEFAULT_BASE_URL = "https://api.darwintiq.com/v1/charlie"
DEFAULT_SYMBOL = "DAX"
DEFAULT_WORKFLOW_ID = "market_briefing"
DEFAULT_PROMPT = (
    "Give me a trader's desk briefing on DAX. "
    "What is actually driving the current tone?"
)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Call the darwintIQ Charlie API for a market briefing.",
    )
    parser.add_argument(
        "--token",
        default=os.getenv("DARWINTIQ_API_TOKEN"),
        help="API token. Defaults to DARWINTIQ_API_TOKEN.",
    )
    parser.add_argument(
        "--base-url",
        default=DEFAULT_BASE_URL,
        help=f"Charlie API endpoint. Defaults to {DEFAULT_BASE_URL}.",
    )
    parser.add_argument(
        "--symbol",
        default=DEFAULT_SYMBOL,
        help=f"Market symbol. Defaults to {DEFAULT_SYMBOL}.",
    )
    parser.add_argument(
        "--workflow-id",
        default=DEFAULT_WORKFLOW_ID,
        help=f"Charlie workflow ID. Defaults to {DEFAULT_WORKFLOW_ID}.",
    )
    parser.add_argument(
        "--prompt",
        default=DEFAULT_PROMPT,
        help="User prompt sent to Charlie.",
    )
    parser.add_argument(
        "--timeframe",
        default=None,
        help="Optional timeframe override such as M15, H1 or D1.",
    )
    parser.add_argument(
        "--signal-mode",
        default=None,
        help="Optional signal mode override.",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=20,
        help="HTTP timeout in seconds. Defaults to 20.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Print the full JSON response instead of a formatted summary.",
    )
    return parser


def build_payload(args: argparse.Namespace) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "symbol": args.symbol,
        "workflowId": args.workflow_id,
        "messages": [
            {
                "role": "user",
                "content": args.prompt,
            }
        ],
    }
    if args.timeframe:
        payload["timeframe"] = args.timeframe
    if args.signal_mode:
        payload["signalMode"] = args.signal_mode
    return payload


def call_charlie_api(
    *,
    url: str,
    token: str,
    payload: dict[str, Any],
    timeout: int,
) -> dict[str, Any]:
    body = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=body,
        method="POST",
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            raw = response.read().decode("utf-8")
            return json.loads(raw)
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8", errors="replace")
        try:
            detail = json.loads(raw)
        except json.JSONDecodeError:
            detail = {"error": raw or exc.reason}
        raise SystemExit(
            f"HTTP {exc.code} calling Charlie API:\n"
            f"{json.dumps(detail, indent=2, ensure_ascii=False)}"
        ) from exc
    except urllib.error.URLError as exc:
        raise SystemExit(f"Network error calling Charlie API: {exc.reason}") from exc


def print_formatted_response(data: dict[str, Any]) -> None:
    answer = data.get("answer")
    workflow_label = data.get("workflowLabel")
    symbol = data.get("symbol")
    language = data.get("language")
    summary = data.get("summary") or {}
    evidence = data.get("evidence") or []

    print(f"Symbol: {symbol}")
    print(f"Workflow: {workflow_label}")
    print(f"Language: {language}")

    if isinstance(summary, dict) and summary:
        bottom_line = summary.get("bottomLine")
        bias = summary.get("bias")
        confidence = summary.get("confidence")
        main_risk = summary.get("mainRisk")

        if bottom_line:
            print(f"Bottom line: {bottom_line}")
        if bias:
            print(f"Bias: {bias}")
        if confidence:
            print(f"Confidence: {confidence}")
        if main_risk:
            print(f"Main risk: {main_risk}")

    if answer:
        print("\nAnswer:\n")
        print(answer)

    if isinstance(evidence, list) and evidence:
        print("\nEvidence:")
        for line in evidence:
            print(f"- {line}")


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    if not args.token:
        parser.error(
            "missing API token. Set DARWINTIQ_API_TOKEN or pass --token."
        )

    payload = build_payload(args)
    response = call_charlie_api(
        url=args.base_url,
        token=args.token,
        payload=payload,
        timeout=args.timeout,
    )

    if args.json:
        print(json.dumps(response, indent=2, ensure_ascii=False))
    else:
        print_formatted_response(response)

    return 0


if __name__ == "__main__":
    sys.exit(main())
