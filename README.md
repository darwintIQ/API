# MetaTrader Examples

This folder contains example Expert Advisors for both MetaTrader 4 (`MT4`) and MetaTrader 5 (`MT5`) that demonstrate how to connect to the darwintIQ API and render the returned data inside a chart.

## Purpose

These examples are intended to:

- show how to authenticate against the API with a bearer token
- demonstrate `WebRequest` usage from MetaTrader
- provide simple, self-contained JSON parsing examples
- render API output directly on the chart for testing and discretionary trading workflows

## Folder Structure

- `MT4/`: MetaTrader 4 example EAs (`.mq4`)
- `MT5/`: MetaTrader 5 ports of the same examples (`.mq5`)

## Included Examples

- `darwintIQ_SupRes_EA`
  Fetches the latest support/resistance snapshot and draws support/resistance levels, regression channel lines, and swing structure on the chart.

- `darwintIQ_TrendMatrix_EA`
  Fetches the latest trend matrix snapshot and renders a compact multi-timeframe panel showing direction and strength.

- `darwintIQ_Model_Api_Demo`
  Calls the models API, parses the first returned model, and displays a compact summary in a chart label.

## Setup

1. Open the desired file in MetaEditor.
2. Replace the placeholder API token with a valid token.
3. Compile the EA.
4. In MetaTrader, allow `WebRequest` for the API base URL used by the script.
5. Attach the EA to a chart.

## Notes

- These examples are intentionally lightweight and use manual string-based JSON parsing to stay self-contained.
- The MT5 files are direct ports of the MT4 examples and are meant to stay behaviorally aligned.
- Depending on your broker symbol naming, you may need to adjust the symbol input parameters.
