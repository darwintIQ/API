// darwintIQ Trend Matrix indicator for cTrader
// Purpose: Fetches the latest trend matrix snapshot and renders a compact text panel on the chart.
// Notes: Indicators in cTrader can access the internet if AccessRights.Internet is set.

using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using cAlgo.API;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.Internet, AutoRescale = false)]
    public class DarwintIQTrendMatrix : Indicator
    {
        [Parameter("API Base URL", DefaultValue = "https://api.darwintIQ.com/v1")]
        public string ApiBaseUrl { get; set; }

        [Parameter("API Token", DefaultValue = "YOUR_API_TOKEN")]
        public string ApiToken { get; set; }

        [Parameter("Symbol (empty = chart)", DefaultValue = "")]
        public string SymbolOverride { get; set; }

        [Parameter("Min Fetch Secs", DefaultValue = 60, MinValue = 10)]
        public int MinFetchSecs { get; set; }

        [Parameter("Timeout ms", DefaultValue = 8000, MinValue = 1000)]
        public int TimeoutMs { get; set; }

        [Parameter("Fetch On New Bar", DefaultValue = true)]
        public bool FetchOnNewBar { get; set; }

        [Parameter("Panel Position", DefaultValue = "TopRight")]
        public StaticPosition PanelPosition { get; set; }

        [Parameter("Font Name", DefaultValue = "Consolas")]
        public string FontName { get; set; }

        [Parameter("Font Size", DefaultValue = 11, MinValue = 8, MaxValue = 22)]
        public int FontSize { get; set; }

        [Parameter("Text Color", DefaultValue = "DimGray")]
        public Color TextColor { get; set; }

        private static readonly HttpClient Http = new HttpClient();
        private DateTime _lastFetchUtc = DateTime.MinValue;
        private DateTime _lastBarTime = DateTime.MinValue;
        private DateTime _lastFetchedBarTime = DateTime.MinValue;
        private string _panelText = "Initializing...";
        private const string PanelName = "dIQ_TrendMatrix_Panel";

        protected override void Initialize()
        {
            _lastBarTime = Bars.LastBar.OpenTime;
            Timer.Start(Math.Max(10, MinFetchSecs));
            FetchAndRender();
        }

        public override void Calculate(int index)
        {
            if (!FetchOnNewBar)
                return;

            var barTime = Bars.LastBar.OpenTime;
            if (barTime != _lastBarTime)
            {
                _lastBarTime = barTime;
            }
        }

        protected override void OnTimer()
        {
            if (FetchOnNewBar)
            {
                var barTime = Bars.LastBar.OpenTime;
                if (barTime == _lastFetchedBarTime)
                    return;

                if (DateTime.UtcNow - _lastFetchUtc < TimeSpan.FromSeconds(MinFetchSecs))
                    return;

                _lastBarTime = barTime;
            }
            else
            {
                if (DateTime.UtcNow - _lastFetchUtc < TimeSpan.FromSeconds(MinFetchSecs))
                    return;
            }

            FetchAndRender();
        }

        private void FetchAndRender()
        {
            try
            {
                var symbol = string.IsNullOrWhiteSpace(SymbolOverride) ? SymbolName : SymbolOverride.Trim();
                var url = $"{ApiBaseUrl.TrimEnd('/')}/trendmatrix?symbol={Uri.EscapeDataString(symbol)}&latest=1";

                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (!string.IsNullOrWhiteSpace(ApiToken))
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiToken.Trim());

                    Http.Timeout = TimeSpan.FromMilliseconds(TimeoutMs);
                    var resp = Http.SendAsync(req).Result;
                    var body = resp.Content.ReadAsStringAsync().Result;

                    if (!resp.IsSuccessStatusCode)
                    {
                        _panelText = $"HTTP {(int)resp.StatusCode}\n{body}";
                        RenderPanel();
                        return;
                    }

                    if (!TryParseTrendMatrix(body, out var panelText))
                    {
                        _panelText = "Parse error: unexpected JSON shape.";
                        RenderPanel();
                        return;
                    }

                    _panelText = panelText;
                    _lastFetchUtc = DateTime.UtcNow;
                    _lastFetchedBarTime = Bars.LastBar.OpenTime;
                    RenderPanel();
                }
            }
            catch (Exception ex)
            {
                _panelText = "Error: " + ex.Message;
                RenderPanel();
            }
        }

        private void RenderPanel()
        {
            var text = _panelText ?? "";
            var obj = Chart.DrawStaticText(PanelName, text, PanelPosition, TextColor);
            obj.Font = FontName;
            obj.FontSize = FontSize;
        }

        private bool TryParseTrendMatrix(string json, out string panelText)
        {
            panelText = "";
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;

                JsonElement trendElement;
                JsonElement baseElement;

                if (TryGetProperty(root, "snapshot", out var snapshot))
                {
                    baseElement = snapshot;
                    if (TryGetProperty(snapshot, "data", out var data))
                        baseElement = data;
                }
                else
                {
                    baseElement = root;
                }

                if (!TryGetProperty(baseElement, "trend", out trendElement))
                {
                    if (!TryGetProperty(root, "trend", out trendElement))
                        return false;
                }

                var consensus = GetString(baseElement, "consensus") ?? GetString(root, "consensus") ?? "NEUTRAL";
                var asOf = GetString(root, "opentime") ?? GetString(baseElement, "time") ?? "n/a";
                var sym = GetString(root, "symbol") ?? (string.IsNullOrWhiteSpace(SymbolOverride) ? SymbolName : SymbolOverride.Trim());

                var sb = new StringBuilder();
                sb.AppendLine("Trend Matrix");
                sb.AppendLine($"{sym} | as of {asOf}");
                sb.AppendLine($"Consensus: {consensus}");

                AppendTf(sb, trendElement, "M1");
                AppendTf(sb, trendElement, "M5");
                AppendTf(sb, trendElement, "M15");
                AppendTf(sb, trendElement, "M30");
                AppendTf(sb, trendElement, "H1");
                AppendTf(sb, trendElement, "H4");
                AppendTf(sb, trendElement, "D1");
                AppendTf(sb, trendElement, "W1");

                panelText = sb.ToString();
                return true;
            }
        }

        private void AppendTf(StringBuilder sb, JsonElement trendElement, string tf)
        {
            if (!TryGetProperty(trendElement, tf, out var tfEl))
            {
                sb.AppendLine($"{tf}: Ranging (0)");
                return;
            }

            var dir = GetStringAny(tfEl, new[] { "dir", "direction", "bias" }) ?? "Ranging";
            var strength = GetNumberAny(tfEl, new[] { "strength", "score", "slope" });
            if (strength < 0)
                strength = Math.Abs(strength);

            var s = (int)Math.Round(strength);
            if (s < 0) s = 0;
            if (s > 5) s = 5;

            sb.AppendLine($"{tf}: {dir} ({s})");
        }

        private bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
                return true;
            value = default;
            return false;
        }

        private string GetString(JsonElement element, string name)
        {
            if (!TryGetProperty(element, name, out var v))
                return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }

        private string GetStringAny(JsonElement element, string[] names)
        {
            foreach (var n in names)
            {
                var s = GetString(element, n);
                if (!string.IsNullOrEmpty(s))
                    return s;
            }
            return null;
        }

        private double GetNumberAny(JsonElement element, string[] names)
        {
            foreach (var n in names)
            {
                if (TryGetProperty(element, n, out var v))
                {
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d))
                        return d;
                    if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d2))
                        return d2;
                }
            }
            return 0;
        }
    }
}
