// darwintIQ Support/Resistance indicator for cTrader
// Purpose: Fetches latest S/R snapshot and draws levels, regression channel, and swing lines.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using cAlgo.API;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.Internet, AutoRescale = false)]
    public class DarwintIQSupRes : Indicator
    {
        [Parameter("API Base URL", DefaultValue = "https://api.darwintIQ.com/v1")]
        public string ApiBaseUrl { get; set; }

        [Parameter("API Token", DefaultValue = "YOUR_API_TOKEN")]
        public string ApiToken { get; set; }

        [Parameter("Symbol (empty = chart)", DefaultValue = "")]
        public string SymbolOverride { get; set; }

        [Parameter("Fetch On New Bar", DefaultValue = true)]
        public bool FetchOnNewBar { get; set; }

        [Parameter("Min Fetch Secs", DefaultValue = 60, MinValue = 10)]
        public int MinFetchSecs { get; set; }

        [Parameter("Timeout ms", DefaultValue = 8000, MinValue = 1000)]
        public int TimeoutMs { get; set; }

        [Parameter("Ensure Regression", DefaultValue = true)]
        public bool EnsureRegression { get; set; }

        [Parameter("Ensure Reg Window Min", DefaultValue = 360, MinValue = 1)]
        public int EnsureRegWindowMin { get; set; }

        [Parameter("Show S/R", DefaultValue = true)]
        public bool ShowSR { get; set; }

        [Parameter("Max S/R Levels", DefaultValue = 24, MinValue = 1, MaxValue = 200)]
        public int SRMaxLevels { get; set; }

        [Parameter("SR Color (Touches=2)", DefaultValue = "PowderBlue")]
        public Color SRColorLight { get; set; }

        [Parameter("SR Color (Touches>=3)", DefaultValue = "DodgerBlue")]
        public Color SRColorHeavy { get; set; }

        [Parameter("SR Width (Touches=2)", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int SRWidthLight { get; set; }

        [Parameter("SR Width (Touches>=3)", DefaultValue = 3, MinValue = 1, MaxValue = 5)]
        public int SRWidthHeavy { get; set; }

        [Parameter("Show Regression", DefaultValue = true)]
        public bool ShowRegression { get; set; }

        [Parameter("Reg Mid Color", DefaultValue = "Blue")]
        public Color RegMidColor { get; set; }

        [Parameter("Reg Up Color", DefaultValue = "SteelBlue")]
        public Color RegUpColor { get; set; }

        [Parameter("Reg Dn Color", DefaultValue = "SteelBlue")]
        public Color RegDnColor { get; set; }

        [Parameter("Reg Width", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int RegWidth { get; set; }

        [Parameter("Show Swing", DefaultValue = true)]
        public bool ShowSwing { get; set; }

        [Parameter("Swing Top Color", DefaultValue = "DarkOrange")]
        public Color SwingTopColor { get; set; }

        [Parameter("Swing Bot Color", DefaultValue = "SeaGreen")]
        public Color SwingBotColor { get; set; }

        [Parameter("Swing Width", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int SwingWidth { get; set; }

        private static readonly HttpClient Http = new HttpClient();
        private DateTime _lastFetchUtc = DateTime.MinValue;
        private DateTime _lastBarTime = DateTime.MinValue;
        private DateTime _lastFetchedBarTime = DateTime.MinValue;
        private const string Prefix = "dIQ_SR_";

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
                var url = $"{ApiBaseUrl.TrimEnd('/')}/supres?symbol={Uri.EscapeDataString(symbol)}&latest=1";
                if (EnsureRegression)
                    url += $"&ensure_reg=1&windowMin={EnsureRegWindowMin}";

                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (!string.IsNullOrWhiteSpace(ApiToken))
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiToken.Trim());

                    Http.Timeout = TimeSpan.FromMilliseconds(TimeoutMs);
                    var resp = Http.SendAsync(req).Result;
                    var body = resp.Content.ReadAsStringAsync().Result;

                    if (!resp.IsSuccessStatusCode)
                    {
                        Print("HTTP {0}: {1}", (int)resp.StatusCode, body);
                        return;
                    }

                    if (!TryParseSupRes(body, out var model))
                    {
                        Print("Parse error: unexpected JSON shape.");
                        return;
                    }

                    Render(model, symbol);
                    _lastFetchUtc = DateTime.UtcNow;
                    _lastFetchedBarTime = Bars.LastBar.OpenTime;
                }
            }
            catch (Exception ex)
            {
                Print("Error: {0}", ex.Message);
            }
        }

        private void Render(SupResModel model, string symbol)
        {
            DeleteWithPrefix(Prefix + symbol + "_");

            if (ShowSR)
            {
                var count = Math.Min(model.Levels.Count, SRMaxLevels);
                for (var i = 0; i < count; i++)
                {
                    var lvl = model.Levels[i];
                    var color = lvl.Touches >= 3 ? SRColorHeavy : SRColorLight;
                    var width = lvl.Touches >= 3 ? SRWidthHeavy : SRWidthLight;
                    var name = $"{Prefix}{symbol}_SR_{lvl.Kind}_{i}";
                    Chart.DrawHorizontalLine(name, lvl.Price, color, width, LineStyle.Solid);
                }
            }

            if (ShowRegression && model.HasRegression)
            {
                Chart.DrawTrendLine($"{Prefix}{symbol}_REG_MID", model.RegT1, model.RegY1Mid, model.RegT2, model.RegY2Mid, RegMidColor, RegWidth, LineStyle.Solid);
                Chart.DrawTrendLine($"{Prefix}{symbol}_REG_UP", model.RegT1, model.RegY1Up, model.RegT2, model.RegY2Up, RegUpColor, RegWidth, LineStyle.Solid);
                Chart.DrawTrendLine($"{Prefix}{symbol}_REG_DN", model.RegT1, model.RegY1Dn, model.RegT2, model.RegY2Dn, RegDnColor, RegWidth, LineStyle.Solid);
            }

            if (ShowSwing && model.HasSwing)
            {
                Chart.DrawTrendLine($"{Prefix}{symbol}_SW_TOP", model.SwingTopT1, model.SwingTopY1, model.SwingTopT2, model.SwingTopY2, SwingTopColor, SwingWidth, LineStyle.Solid);
                Chart.DrawTrendLine($"{Prefix}{symbol}_SW_BOT", model.SwingBotT1, model.SwingBotY1, model.SwingBotT2, model.SwingBotY2, SwingBotColor, SwingWidth, LineStyle.Solid);
            }
        }

        private void DeleteWithPrefix(string prefix)
        {
            var objects = Chart.Objects;
            for (var i = objects.Count - 1; i >= 0; i--)
            {
                var obj = objects[i];
                if (obj.Name != null && obj.Name.StartsWith(prefix, StringComparison.Ordinal))
                    Chart.RemoveObject(obj.Name);
            }
        }

        private bool TryParseSupRes(string json, out SupResModel model)
        {
            model = new SupResModel();

            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                JsonElement baseEl = root;
                if (TryGetProperty(root, "snapshot", out var snapshot))
                    baseEl = snapshot;

                if (!TryGetProperty(baseEl, "cur", out var cur) || cur.ValueKind != JsonValueKind.Array)
                    return false;

                foreach (var item in cur.EnumerateArray())
                {
                    var kind = GetString(item, "type") ?? "";
                    var price = GetNumber(item, "price");
                    var touches = (int)Math.Round(GetNumber(item, "touches"));
                    model.Levels.Add(new SRLevel { Kind = kind, Price = price, Touches = touches });
                }

                if (TryGetProperty(baseEl, "reg", out var reg))
                {
                    if (TryParseRegSide(reg, "mid", out var t1, out var y1, out var t2, out var y2) &&
                        TryParseRegSide(reg, "up", out var _t1u, out var y1u, out var _t2u, out var y2u) &&
                        TryParseRegSide(reg, "dn", out var _t1d, out var y1d, out var _t2d, out var y2d))
                    {
                        model.HasRegression = true;
                        model.RegT1 = t1;
                        model.RegT2 = t2;
                        model.RegY1Mid = y1;
                        model.RegY2Mid = y2;
                        model.RegY1Up = y1u;
                        model.RegY2Up = y2u;
                        model.RegY1Dn = y1d;
                        model.RegY2Dn = y2d;
                    }
                }

                if (TryGetProperty(baseEl, "swing", out var swing))
                {
                    if (TryParseRegSide(swing, "top", out var st1, out var sy1, out var st2, out var sy2) &&
                        TryParseRegSide(swing, "bot", out var bt1, out var by1, out var bt2, out var by2))
                    {
                        model.HasSwing = true;
                        model.SwingTopT1 = st1;
                        model.SwingTopT2 = st2;
                        model.SwingTopY1 = sy1;
                        model.SwingTopY2 = sy2;
                        model.SwingBotT1 = bt1;
                        model.SwingBotT2 = bt2;
                        model.SwingBotY1 = by1;
                        model.SwingBotY2 = by2;
                    }
                }
            }

            return true;
        }

        private bool TryParseRegSide(JsonElement parent, string key, out DateTime t1, out double y1, out DateTime t2, out double y2)
        {
            t1 = default;
            t2 = default;
            y1 = 0;
            y2 = 0;

            if (!TryGetProperty(parent, key, out var el))
                return false;

            var t1s = GetString(el, "t1");
            var t2s = GetString(el, "t2");
            if (!TryParseTime(t1s, out t1) || !TryParseTime(t2s, out t2))
                return false;

            y1 = GetNumber(el, "y1");
            y2 = GetNumber(el, "y2");
            return true;
        }

        private bool TryParseTime(string input, out DateTime dt)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                dt = default;
                return false;
            }

            if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                return true;

            return DateTime.TryParse(input.Replace("-", "."), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt);
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

        private double GetNumber(JsonElement element, string name)
        {
            if (!TryGetProperty(element, name, out var v))
                return 0;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d))
                return d;
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d2))
                return d2;
            return 0;
        }

        private class SupResModel
        {
            public List<SRLevel> Levels { get; } = new List<SRLevel>();

            public bool HasRegression { get; set; }
            public DateTime RegT1 { get; set; }
            public DateTime RegT2 { get; set; }
            public double RegY1Mid { get; set; }
            public double RegY2Mid { get; set; }
            public double RegY1Up { get; set; }
            public double RegY2Up { get; set; }
            public double RegY1Dn { get; set; }
            public double RegY2Dn { get; set; }

            public bool HasSwing { get; set; }
            public DateTime SwingTopT1 { get; set; }
            public DateTime SwingTopT2 { get; set; }
            public double SwingTopY1 { get; set; }
            public double SwingTopY2 { get; set; }
            public DateTime SwingBotT1 { get; set; }
            public DateTime SwingBotT2 { get; set; }
            public double SwingBotY1 { get; set; }
            public double SwingBotY2 { get; set; }
        }

        private class SRLevel
        {
            public string Kind { get; set; }
            public double Price { get; set; }
            public int Touches { get; set; }
        }
    }
}
