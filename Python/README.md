# Scripts

## Charlie Market Briefing Example

[`charlie_market_briefing_example.py`](./charlie_market_briefing_example.py) is a minimal Python example for calling the darwintIQ Charlie public API.

Default use case:

- workflow: `market_briefing`
- symbol: `DAX`
- prompt: `Give me a trader's desk briefing on DAX. What is actually driving the current tone?`

The script uses only the Python standard library and does not require external packages.

### Requirements

- Python 3
- a valid darwintIQ API token with access to Charlie and the requested symbol

### Quick Start

```bash
export DARWINTIQ_API_TOKEN="YOUR_TOKEN"
python3 scripts/charlie_market_briefing_example.py
```

### Print Full JSON

```bash
python3 scripts/charlie_market_briefing_example.py --json
```

### Override Symbol or Prompt

```bash
python3 scripts/charlie_market_briefing_example.py \
  --symbol EURUSD \
  --prompt "Give me a trader's desk briefing on EURUSD. What is actually driving the current tone?"
```

### Optional Parameters

- `--timeframe M15`
- `--signal-mode Breakout`
- `--workflow-id market_briefing`
- `--base-url https://api.darwintiq.com/v1/charlie`
- `--timeout 20`

### Notes

- Charlie response language follows the language of the prompt.
- If the token is missing, the script stops before sending the request.
- API errors such as `401`, `403`, `429`, or `500` are printed with the returned JSON body when available.
