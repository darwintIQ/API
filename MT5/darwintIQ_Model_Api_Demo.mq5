//+------------------------------------------------------------------+
//|                  darwintIQ_Model_Api_Demo.mq5                    |
//| What it does: Calls the models API, parses the first returned    |
//| model, and prints a compact summary into a chart label.          |
//| Purpose: Serves as a minimal API integration example for testing |
//| authentication, response handling, and simple JSON parsing.      |
//+------------------------------------------------------------------+

#property strict

input string ApiBaseUrl = "https://api.darwintiq.com/v1/models";
input string ApiToken = "[YOUR_API_KEY]";
input string SymbolParam = "EURUSD";
input string SortParam = "fitness";
input int    LimitParam = 5;
input string EntryTypeParam = "";
input int    RefreshSeconds = 60;
input string LabelName = "ModelApiLabel";
input string FontName = "Consolas";
input int    FontSize = 10;
input color  FontColor = clrLime;
input int    LabelCorner = 0; // 0=left-top,1=right-top,2=left-bottom,3=right-bottom
input int    LabelX = 10;
input int    LabelY = 10;
input bool   DebugLog = false;
input bool   ParseJsonToObject = true;
input bool   ShowRawJsonOnParseFail = true;
input int    MaxLabelChars = 1000;

string g_lastText = "";

struct ModelObject
{
   string id;
   string symbol;
   string timeframe;
   string entryType;
   double fitness;
   double entryMovePips;
   double expectedValue;
   bool hasFitness;
   bool hasEntryMovePips;
   bool hasExpectedValue;
   bool valid;
};

int OnInit()
{
   if (!EnsureLabel())
      return(INIT_FAILED);

   EventSetTimer(MathMax(5, RefreshSeconds));
   FetchAndDisplay();
   return(INIT_SUCCEEDED);
}

void OnDeinit(const int reason)
{
   EventKillTimer();
   ObjectDelete(0, LabelName);
}

void OnTimer()
{
   FetchAndDisplay();
}

bool EnsureLabel()
{
   if (ObjectFind(0, LabelName) < 0)
   {
      if (!ObjectCreate(0, LabelName, OBJ_LABEL, 0, 0, 0))
         return(false);
   }

   ObjectSetInteger(0, LabelName, OBJPROP_CORNER, LabelCorner);
   ObjectSetInteger(0, LabelName, OBJPROP_XDISTANCE, LabelX);
   ObjectSetInteger(0, LabelName, OBJPROP_YDISTANCE, LabelY);
   ObjectSetInteger(0, LabelName, OBJPROP_COLOR, FontColor);
   ObjectSetString(0, LabelName, OBJPROP_FONT, FontName);
   ObjectSetInteger(0, LabelName, OBJPROP_FONTSIZE, FontSize);
   ObjectSetString(0, LabelName, OBJPROP_TEXT, "Loading...");
   return(true);
}

void FetchAndDisplay()
{
   string url = BuildUrl();
   string token = Trim(ApiToken);
   string headers = "";
   if (StringLen(token) > 0)
      headers = "Authorization: Bearer " + token + "\r\n";
   headers += "User-Agent: MetaTrader5\r\n";
   char data[];
   char result[];
   string resultHeaders;

   ResetLastError();
   ArrayResize(data, 0);
   int timeout = 5000;
   int res = WebRequest("GET", url, headers, timeout, data, result, resultHeaders);
   if (res == -1)
   {
      int err = GetLastError();
      UpdateLabel("WebRequest failed. Error: " + IntegerToString(err));
      Print("WebRequest failed. Error: ", err);
      return;
   }
   if (DebugLog)
   {
      Print("URL: ", url);
      Print("HTTP code: ", res);
      Print("Response headers: ", resultHeaders);
   }

   if (res <= 0)
   {
      UpdateLabel("Empty response. Bytes: " + IntegerToString(res));
      return;
   }

   int bodyLen = ArraySize(result);
   string body = CharArrayToString(result, 0, bodyLen);
   if (StringLen(body) == 0)
      body = "(empty response)";

   string apiError = JsonGetString(body, "error");
   if (apiError == "")
      apiError = JsonGetString(body, "message");
   if (apiError != "")
   {
      Print("API error body: ", body);
      UpdateLabel(ClampText("API error: " + apiError, MaxLabelChars));
      return;
   }

   if (ParseJsonToObject)
   {
      ModelObject model;
      if (ParseFirstModel(body, model))
      {
         string formatted = FormatModelText(res, model);
         UpdateLabel(ClampText(formatted, MaxLabelChars));
         return;
      }

      if (ShowRawJsonOnParseFail)
      {
         string preview = body;
         if (StringLen(preview) > 350)
            preview = StringSubstr(preview, 0, 350) + "...";
         Print("Parse failed. Raw body: ", body);
         UpdateLabel(ClampText("Parse failed, raw preview:\n" + preview, MaxLabelChars));
         return;
      }
   }

   UpdateLabel(ClampText(body, MaxLabelChars));
}

string BuildUrl()
{
   string url = ApiBaseUrl;
   string sep = (StringFind(url, "?") >= 0) ? "&" : "?";

   url += sep + "symbol=" + UrlEncode(SymbolParam);

   if (StringLen(SortParam) > 0)
      url += "&sort=" + UrlEncode(SortParam);

   if (LimitParam > 0)
      url += "&limit=" + IntegerToString(LimitParam);

   if (StringLen(EntryTypeParam) > 0)
      url += "&entryType=" + UrlEncode(EntryTypeParam);

   return(url);
}

void UpdateLabel(string text)
{
   if (text == g_lastText)
      return;

   g_lastText = text;
   if (!ObjectSetString(0, LabelName, OBJPROP_TEXT, text))
      Print("Failed to set label text for ", LabelName);
}

string UrlEncode(string s)
{
   string out = "";
   int len = StringLen(s);
   for (int i = 0; i < len; i++)
   {
      ushort c = StringGetCharacter(s, i);
      if ((c >= 'A' && c <= 'Z') ||
          (c >= 'a' && c <= 'z') ||
          (c >= '0' && c <= '9') ||
          c == '-' || c == '_' || c == '.' || c == '~')
      {
         out += CharToString(c);
      }
      else if (c == ' ')
      {
         out += "%20";
      }
      else
      {
         out += "%" + StringFormat("%02X", c);
      }
   }
   return(out);
}

string Trim(string s)
{
   s = StringTrimLeft(s);
   s = StringTrimRight(s);
   return s;
}

string ClampText(string s, int maxLen)
{
   if (maxLen <= 0) return s;
   if (StringLen(s) <= maxLen) return s;
   if (maxLen <= 3) return StringSubstr(s, 0, maxLen);
   return StringSubstr(s, 0, maxLen - 3) + "...";
}

void ResetModel(ModelObject &m)
{
   m.id = "";
   m.symbol = "";
   m.timeframe = "";
   m.entryType = "";
   m.fitness = 0.0;
   m.entryMovePips = 0.0;
   m.expectedValue = 0.0;
   m.hasFitness = false;
   m.hasEntryMovePips = false;
   m.hasExpectedValue = false;
   m.valid = false;
}

string FormatModelText(int httpCode, ModelObject &m)
{
   string t = "HTTP: " + IntegerToString(httpCode);
   if (m.symbol != "") t += "\nSymbol: " + m.symbol;
   else t += "\nSymbol: " + SymbolParam;
   if (m.id != "") t += "\nModel: " + m.id;
   if (m.timeframe != "") t += "\nTF: " + m.timeframe;
   if (m.entryType != "") t += "\nEntry: " + m.entryType;
   if (m.hasFitness) t += "\nFitness: " + DoubleToString(m.fitness, 2);
   if (m.hasEntryMovePips) t += "\nEntryMove: " + DoubleToString(m.entryMovePips, 2);
   if (m.hasExpectedValue) t += "\nExpected: " + DoubleToString(m.expectedValue, 2);
   return t;
}

bool ParseFirstModel(string json, ModelObject &m)
{
   ResetModel(m);

   string obj = ExtractFirstObjectFromNamedArray(json, "trading_models");
   if (StringLen(obj) == 0)
      obj = ExtractFirstObjectFromNamedArray(json, "traders");
   if (StringLen(obj) == 0)
      obj = ExtractNamedObject(json, "trader");
   if (StringLen(obj) == 0)
      obj = ExtractNamedObject(json, "model");
   if (StringLen(obj) == 0)
      obj = ExtractNamedObject(json, "best");
   if (StringLen(obj) == 0)
      obj = ExtractFirstObjectFromRootArray(json);
   if (StringLen(obj) == 0)
      return false;

   m.id = JsonGetString(obj, "id");
   if (m.id == "")
      m.id = JsonGetString(obj, "modelId");
   m.symbol = JsonGetString(obj, "symbol");
   m.timeframe = JsonGetString(obj, "timeframe");
   m.entryType = JsonGetString(obj, "entryType");
   if (m.entryType == "")
      m.entryType = JsonGetString(obj, "type");

   string n = JsonGetNumberOrString(obj, "fitness");
   if (n != "")
   {
      m.fitness = StrToDouble(n);
      m.hasFitness = true;
   }

   n = JsonGetNumberOrString(obj, "entryMovePips");
   if (n == "")
      n = JsonGetNumberOrString(obj, "potentialPips");
   if (n == "")
      n = JsonGetNumberOrString(obj, "entryMove");
   if (n != "")
   {
      m.entryMovePips = StrToDouble(n);
      m.hasEntryMovePips = true;
   }

   n = JsonGetNumberOrString(obj, "expectedValue");
   if (n != "")
   {
      m.expectedValue = StrToDouble(n);
      m.hasExpectedValue = true;
   }

   m.valid = (m.id != "" || m.symbol != "" || m.entryType != "" || m.hasFitness || m.hasEntryMovePips || m.hasExpectedValue);
   return m.valid;
}

string ExtractFirstObjectFromNamedArray(string json, string key)
{
   string pat = "\"" + key + "\"";
   int k = StringFind(json, pat);
   if (k < 0)
      return "";
   int arrStart = StringFind(json, "[", k);
   if (arrStart < 0)
      return "";
   return ExtractFirstObjectFromArrayAt(json, arrStart);
}

string ExtractNamedObject(string json, string key)
{
   string pat = "\"" + key + "\"";
   int k = StringFind(json, pat);
   if (k < 0)
      return "";

   int colon = StringFind(json, ":", k + StringLen(pat));
   if (colon < 0)
      return "";

   int v = SkipWs(json, colon + 1);
   if (v < 0 || v >= StringLen(json))
      return "";
   if (StringGetCharacter(json, v) != '{')
      return "";

   int end = FindMatching(json, v, '{', '}');
   if (end < 0)
      return "";
   return StringSubstr(json, v, end - v + 1);
}

string ExtractFirstObjectFromRootArray(string json)
{
   int i = SkipWs(json, 0);
   if (i < 0 || i >= StringLen(json))
      return "";
   if (StringGetCharacter(json, i) != '[')
      return "";
   return ExtractFirstObjectFromArrayAt(json, i);
}

string ExtractFirstObjectFromArrayAt(string json, int arrStart)
{
   int end = FindMatching(json, arrStart, '[', ']');
   if (end < 0)
      return "";

   int i = arrStart + 1;
   while (i < end)
   {
      i = SkipWs(json, i);
      if (i < 0 || i >= end)
         break;

      int c = StringGetCharacter(json, i);
      if (c == '{')
      {
         int objEnd = FindMatching(json, i, '{', '}');
         if (objEnd < 0 || objEnd > end)
            return "";
         return StringSubstr(json, i, objEnd - i + 1);
      }

      if (c == '[')
      {
         int nested = FindMatching(json, i, '[', ']');
         if (nested < 0)
            return "";
         i = nested + 1;
         continue;
      }

      i++;
   }

   return "";
}

int SkipWs(string s, int pos)
{
   int n = StringLen(s);
   int i = pos;
   while (i < n)
   {
      int c = StringGetCharacter(s, i);
      if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
         return i;
      i++;
   }
   return n;
}

int FindMatching(string s, int start, int openCh, int closeCh)
{
   int n = StringLen(s);
   if (start < 0 || start >= n)
      return -1;
   if (StringGetCharacter(s, start) != openCh)
      return -1;

   int depth = 0;
   bool inStr = false;
   bool esc = false;

   for (int i = start; i < n; i++)
   {
      int c = StringGetCharacter(s, i);

      if (inStr)
      {
         if (esc)
         {
            esc = false;
            continue;
         }
         if (c == '\\')
         {
            esc = true;
            continue;
         }
         if (c == '"')
            inStr = false;
         continue;
      }

      if (c == '"')
      {
         inStr = true;
         continue;
      }
      if (c == openCh)
         depth++;
      else if (c == closeCh)
      {
         depth--;
         if (depth == 0)
            return i;
      }
   }

   return -1;
}

int FindKeyValueStart(string jsonObj, string key)
{
   string pat = "\"" + key + "\"";
   int k = StringFind(jsonObj, pat);
   if (k < 0)
      return -1;

   int colon = StringFind(jsonObj, ":", k + StringLen(pat));
   if (colon < 0)
      return -1;
   return SkipWs(jsonObj, colon + 1);
}

string JsonGetString(string jsonObj, string key)
{
   int v = FindKeyValueStart(jsonObj, key);
   if (v < 0 || v >= StringLen(jsonObj))
      return "";
   if (StringGetCharacter(jsonObj, v) != '"')
      return "";

   int i = v + 1;
   bool esc = false;
   string out = "";
   int n = StringLen(jsonObj);
   while (i < n)
   {
      int c = StringGetCharacter(jsonObj, i);
      if (esc)
      {
         out += CharToString((uchar)c);
         esc = false;
      }
      else if (c == '\\')
      {
         esc = true;
      }
      else if (c == '"')
      {
         return out;
      }
      else
      {
         out += CharToString((uchar)c);
      }
      i++;
   }
   return "";
}

string JsonGetNumberOrString(string jsonObj, string key)
{
   int v = FindKeyValueStart(jsonObj, key);
   if (v < 0 || v >= StringLen(jsonObj))
      return "";

   int c = StringGetCharacter(jsonObj, v);
   if (c == '"')
      return JsonGetString(jsonObj, key);

   int i = v;
   int n = StringLen(jsonObj);
   while (i < n)
   {
      c = StringGetCharacter(jsonObj, i);
      if (c == ',' || c == '}' || c == ']' || c == ' ' || c == '\t' || c == '\r' || c == '\n')
         break;
      i++;
   }
   if (i <= v)
      return "";
   return StringSubstr(jsonObj, v, i - v);
}
