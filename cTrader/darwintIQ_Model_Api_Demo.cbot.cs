// darwintIQ Model API demo cBot for cTrader
// Purpose: Calls the models API, parses the first returned model, and prints a compact summary.

using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using cAlgo.API;

namespace cAlgo
{
    [Robot(AccessRights = AccessRights.Internet)]
    public class DarwintIQModelApiDemo : Robot
    {
        [Parameter("API Base URL", DefaultValue = "https://api.darwintIQ.com/v1/models")]
        public string ApiBaseUrl { get; set; }

        [Parameter("API Token", DefaultValue = "YOUR_API_TOKEN")]
        public string ApiToken { get; set; }

        [Parameter("Symbol", DefaultValue = "EURUSD")]
        public string SymbolParam { get; set; }

        [Parameter("Sort", DefaultValue = "fitness")]
        public string SortParam { get; set; }

        [Parameter("Limit", DefaultValue = 5, MinValue = 1, MaxValue = 100)]
        public int LimitParam { get; set; }

        [Parameter("Entry Type", DefaultValue = "")]
        public string EntryTypeParam { get; set; }

        [Parameter("Refresh Secs", DefaultValue = 60, MinValue = 10)]
        public int RefreshSeconds { get; set; }

        [Parameter("Show On Chart", DefaultValue = true)]
        public bool ShowOnChart { get; set; }

        [Parameter("Chart Position", DefaultValue = "TopLeft")]
        public StaticPosition PanelPosition { get; set; }

        [Parameter("Font Name", DefaultValue = "Consolas")]
        public string FontName { get; set; }

        [Parameter("Font Size", DefaultValue = 11, MinValue = 8, MaxValue = 22)]
        public int FontSize { get; set; }

        [Parameter("Text Color", DefaultValue = "Lime")]
        public Color TextColor { get; set; }

        private static readonly HttpClient Http = new HttpClient();
        private const string PanelName = "dIQ_ModelApi_Panel";

        protected override void OnStart()
        {
            Timer.Start(Math.Max(10, RefreshSeconds));
            FetchAndDisplay();
        }

        protected override void OnTimer()
        {
            FetchAndDisplay();
        }

        private void FetchAndDisplay()
        {
            try
            {
                var url = BuildUrl();

                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (!string.IsNullOrWhiteSpace(ApiToken))
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiToken.Trim());

                    Http.Timeout = TimeSpan.FromMilliseconds(8000);
                    var resp = Http.SendAsync(req).Result;
                    var body = resp.Content.ReadAsStringAsync().Result;

                    if (!resp.IsSuccessStatusCode)
                    {
                        Print("HTTP {0}: {1}", (int)resp.StatusCode, body);
                        UpdatePanel($"HTTP {(int)resp.StatusCode}\n{body}");
                        return;
                    }

                    if (!TryParseFirstModel(body, out var model))
                    {
                        UpdatePanel("Parse error: unexpected JSON shape.");
                        return;
                    }

                    var summary = FormatModel(model, (int)resp.StatusCode);
                    Print(summary);
                    UpdatePanel(summary);
                }
            }
            catch (Exception ex)
            {
                UpdatePanel("Error: " + ex.Message);
            }
        }

        private string BuildUrl()
        {
            var url = ApiBaseUrl.TrimEnd('/');
            var sb = new StringBuilder();
            sb.Append(url);
            sb.Append("?symbol=").Append(Uri.EscapeDataString(SymbolParam));

            if (!string.IsNullOrWhiteSpace(SortParam))
                sb.Append("&sort=").Append(Uri.EscapeDataString(SortParam));

            if (LimitParam > 0)
                sb.Append("&limit=").Append(LimitParam);

            if (!string.IsNullOrWhiteSpace(EntryTypeParam))
                sb.Append("&entryType=").Append(Uri.EscapeDataString(EntryTypeParam));

            return sb.ToString();
        }

        private void UpdatePanel(string text)
        {
            if (!ShowOnChart)
                return;

            var obj = Chart.DrawStaticText(PanelName, text, PanelPosition, TextColor);
            obj.Font = FontName;
            obj.FontSize = FontSize;
        }

        private string FormatModel(ModelObject m, int httpCode)
        {
            var sb = new StringBuilder();
            sb.Append("HTTP: ").Append(httpCode);
            sb.Append("\nSymbol: ").Append(string.IsNullOrWhiteSpace(m.Symbol) ? SymbolParam : m.Symbol);
            if (!string.IsNullOrWhiteSpace(m.Id)) sb.Append("\nModel: ").Append(m.Id);
            if (!string.IsNullOrWhiteSpace(m.Timeframe)) sb.Append("\nTF: ").Append(m.Timeframe);
            if (!string.IsNullOrWhiteSpace(m.EntryType)) sb.Append("\nEntry: ").Append(m.EntryType);
            if (m.HasFitness) sb.Append("\nFitness: ").Append(m.Fitness.ToString("0.00", CultureInfo.InvariantCulture));
            if (m.HasEntryMovePips) sb.Append("\nEntryMove: ").Append(m.EntryMovePips.ToString("0.00", CultureInfo.InvariantCulture));
            if (m.HasExpectedValue) sb.Append("\nExpected: ").Append(m.ExpectedValue.ToString("0.00", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private bool TryParseFirstModel(string json, out ModelObject model)
        {
            model = new ModelObject();
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;

                JsonElement obj;
                if (TryGetFirstFromArray(root, "trading_models", out obj) ||
                    TryGetFirstFromArray(root, "traders", out obj) ||
                    TryGetObject(root, "trader", out obj) ||
                    TryGetObject(root, "model", out obj) ||
                    TryGetObject(root, "best", out obj) ||
                    TryGetFirstFromRootArray(root, out obj))
                {
                    model.Id = GetString(obj, "id") ?? GetString(obj, "modelId") ?? "";
                    model.Symbol = GetString(obj, "symbol") ?? "";
                    model.Timeframe = GetString(obj, "timeframe") ?? "";
                    model.EntryType = GetString(obj, "entryType") ?? GetString(obj, "type") ?? "";

                    if (TryGetNumberOrString(obj, "fitness", out var n)) { model.Fitness = n; model.HasFitness = true; }
                    if (TryGetNumberOrString(obj, "entryMovePips", out n) || TryGetNumberOrString(obj, "potentialPips", out n) || TryGetNumberOrString(obj, "entryMove", out n)) { model.EntryMovePips = n; model.HasEntryMovePips = true; }
                    if (TryGetNumberOrString(obj, "expectedValue", out n)) { model.ExpectedValue = n; model.HasExpectedValue = true; }

                    return true;
                }
            }

            return false;
        }

        private bool TryGetFirstFromArray(JsonElement root, string name, out JsonElement obj)
        {
            obj = default;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    obj = item;
                    return true;
                }
            }
            return false;
        }

        private bool TryGetFirstFromRootArray(JsonElement root, out JsonElement obj)
        {
            obj = default;
            if (root.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    obj = item;
                    return true;
                }
            }
            return false;
        }

        private bool TryGetObject(JsonElement root, string name, out JsonElement obj)
        {
            obj = default;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object)
                return false;
            obj = el;
            return true;
        }

        private string GetString(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;
            if (!element.TryGetProperty(name, out var v))
                return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }

        private bool TryGetNumberOrString(JsonElement element, string name, out double value)
        {
            value = 0;
            if (element.ValueKind != JsonValueKind.Object)
                return false;
            if (!element.TryGetProperty(name, out var v))
                return false;

            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d))
            {
                value = d;
                return true;
            }
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d2))
            {
                value = d2;
                return true;
            }
            return false;
        }

        private class ModelObject
        {
            public string Id { get; set; }
            public string Symbol { get; set; }
            public string Timeframe { get; set; }
            public string EntryType { get; set; }
            public double Fitness { get; set; }
            public double EntryMovePips { get; set; }
            public double ExpectedValue { get; set; }
            public bool HasFitness { get; set; }
            public bool HasEntryMovePips { get; set; }
            public bool HasExpectedValue { get; set; }
        }
    }
}
