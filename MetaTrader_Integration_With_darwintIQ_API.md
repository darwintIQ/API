# MetaTrader Integration with darwintIQ API

This guide explains how to connect MetaTrader 4 or MetaTrader 5 to the darwintIQ API, enable outbound API requests, and use the provided example Expert Advisors as a starting point.

## Before You Start

The example EAs use MetaTrader's built-in `WebRequest()` function to call the darwintIQ API. MetaTrader blocks external HTTP requests by default, so you must explicitly allow the API domain before the examples will work.

## Enable `WebRequest`

In both `MT4` and `MT5`:

1. Open `Tools -> Options -> Expert Advisors`.
2. Enable `Allow WebRequest for listed URL:`.
3. Add `https://api.darwintiq.com`.
4. Click `OK`.
5. Remove and reattach the EA, or reload the chart.

If this step is skipped, the EA will fail with a `WebRequest` error and no API data will be returned.

## How the Integration Works

At a high level, the MetaTrader integration follows this pattern:

1. The EA calls a darwintIQ endpoint such as `/v1/models`, `/v1/supres`, or `/v1/trendmatrix`.
2. The API response is parsed inside the EA.
3. The parsed values are either displayed on the chart or used as input for trade logic.
4. The request is repeated on a timer or on new bars, depending on the EA configuration.

The included examples focus on data retrieval and chart rendering. They are intended as integration examples, not finished auto-trading systems.

## Included Example EAs

The repository includes minimal examples for both `MT4` and `MT5`:

- `darwintIQ_Model_Api_Demo`
  Calls the models API, reads the first returned model, and shows a compact summary in a chart label.

- `darwintIQ_SupRes_EA`
  Fetches the latest support and resistance snapshot and draws levels, regression channel lines, and swing structure directly on the chart.

- `darwintIQ_TrendMatrix_EA`
  Fetches the latest trend matrix snapshot and renders a compact multi-timeframe direction and strength panel.

The `MT5` files are direct ports of the `MT4` examples so both platforms stay aligned.

## What the Examples Are For

These EAs are useful for:

- verifying that API authentication works
- testing `WebRequest()` connectivity from MetaTrader
- validating the response shape of the API
- learning how to turn API output into on-chart visuals
- using the examples as a base for your own execution logic

They intentionally use lightweight manual string parsing so they remain self-contained and easy to inspect.

## Production Guidance

For production use, the examples should be hardened before real trading:

- replace naive string parsing with a proper JSON parser for MQL
- add risk management such as lot sizing, stop-loss, take-profit, max spread, and slippage limits
- implement duplicate-entry protection so the EA does not repeatedly open the same position
- add trading session filters and failure handling
- test thoroughly in the MetaTrader Strategy Tester before using a live account

The current examples are best treated as reference implementations for connectivity and visualization.

## Example Request

```http
GET https://api.darwintiq.com/v1/models?symbol=EURUSD&sort=fitness
```

## Repository

Public example code is available here:

`https://github.com/darwintIQ/API`
