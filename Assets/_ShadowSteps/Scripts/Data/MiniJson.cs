using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShadowSteps.Data
{
    /// <summary>Insertion-ordered JSON object so written files stay diff-friendly.</summary>
    public sealed class JsonObject
    {
        private readonly List<string> _keys = new List<string>();
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public IReadOnlyList<string> Keys => _keys;
        public int Count => _keys.Count;
        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public object this[string key]
        {
            get => _values[key];
            set
            {
                if (!_values.ContainsKey(key)) _keys.Add(key);
                _values[key] = value;
            }
        }

        public bool TryGetValue(string key, out object value) => _values.TryGetValue(key, out value);

        public JsonObject Set(string key, object value) { this[key] = value; return this; }

        public bool Remove(string key)
        {
            if (!_values.Remove(key)) return false;
            _keys.Remove(key);
            return true;
        }
    }

    public sealed class JsonParseException : Exception
    {
        public int Position { get; }
        public JsonParseException(string message, int position) : base(message + " (at char " + position + ")") { Position = position; }
    }

    /// <summary>
    /// Small strict JSON parser/writer with no engine or package dependency. Values map to:
    /// object -> <see cref="JsonObject"/>, array -> List&lt;object&gt;, string, bool, null,
    /// integral number -> long, other number -> double.
    /// Used because Unity's JsonUtility cannot deserialize nested arrays, nulls or dictionaries that the
    /// level and save formats need.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var p = new Parser(json);
            p.SkipWhitespace();
            var v = p.ParseValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw new JsonParseException("Unexpected trailing content", p.Pos);
            return v;
        }

        public static string Serialize(object value, bool pretty = true)
        {
            var sb = new StringBuilder();
            Write(sb, value, pretty, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, bool pretty, int indent)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: WriteString(sb, s); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case JsonObject o: WriteObject(sb, o, pretty, indent); break;
                case System.Collections.IEnumerable e: WriteArray(sb, e, pretty, indent); break;
                default: WriteString(sb, value.ToString()); break;
            }
        }

        private static void WriteObject(StringBuilder sb, JsonObject o, bool pretty, int indent)
        {
            if (o.Count == 0) { sb.Append("{}"); return; }
            sb.Append('{');
            bool first = true;
            foreach (var k in o.Keys)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, pretty, indent + 1);
                WriteString(sb, k);
                sb.Append(pretty ? ": " : ":");
                Write(sb, o[k], pretty, indent + 1);
            }
            NewLine(sb, pretty, indent);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, System.Collections.IEnumerable e, bool pretty, int indent)
        {
            var items = new List<object>();
            foreach (var x in e) items.Add(x);
            if (items.Count == 0) { sb.Append("[]"); return; }
            // Compact arrays of scalars on one line for readability ([2, 5]); nested containers go multi-line.
            bool scalarsOnly = true;
            foreach (var x in items) if (x is JsonObject || (x is System.Collections.IEnumerable && !(x is string))) { scalarsOnly = false; break; }
            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                if (pretty && scalarsOnly) { if (i > 0) sb.Append(' '); }
                else NewLine(sb, pretty, indent + 1);
                Write(sb, items[i], pretty, indent + 1);
            }
            if (!(pretty && scalarsOnly)) NewLine(sb, pretty, indent);
            sb.Append(']');
        }

        private static void NewLine(StringBuilder sb, bool pretty, int indent)
        {
            if (!pretty) return;
            sb.Append('\n');
            for (int i = 0; i < indent; i++) sb.Append("  ");
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _s;
            public int Pos;
            public Parser(string s) { _s = s; }
            public bool AtEnd => Pos >= _s.Length;

            public void SkipWhitespace()
            {
                while (Pos < _s.Length && (_s[Pos] == ' ' || _s[Pos] == '\t' || _s[Pos] == '\n' || _s[Pos] == '\r')) Pos++;
            }

            public object ParseValue()
            {
                if (AtEnd) throw new JsonParseException("Unexpected end of input", Pos);
                char c = _s[Pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw new JsonParseException("Unexpected character '" + c + "'", Pos);
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, Pos, word, 0, word.Length) != 0) throw new JsonParseException("Expected '" + word + "'", Pos);
                Pos += word.Length;
            }

            private JsonObject ParseObject()
            {
                var o = new JsonObject();
                Pos++; // {
                SkipWhitespace();
                if (!AtEnd && _s[Pos] == '}') { Pos++; return o; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _s[Pos] != '"') throw new JsonParseException("Expected string key", Pos);
                    string key = ParseString();
                    SkipWhitespace();
                    if (AtEnd || _s[Pos] != ':') throw new JsonParseException("Expected ':'", Pos);
                    Pos++;
                    SkipWhitespace();
                    o[key] = ParseValue();
                    SkipWhitespace();
                    if (AtEnd) throw new JsonParseException("Unterminated object", Pos);
                    if (_s[Pos] == ',') { Pos++; continue; }
                    if (_s[Pos] == '}') { Pos++; return o; }
                    throw new JsonParseException("Expected ',' or '}'", Pos);
                }
            }

            private List<object> ParseArray()
            {
                var a = new List<object>();
                Pos++; // [
                SkipWhitespace();
                if (!AtEnd && _s[Pos] == ']') { Pos++; return a; }
                while (true)
                {
                    SkipWhitespace();
                    a.Add(ParseValue());
                    SkipWhitespace();
                    if (AtEnd) throw new JsonParseException("Unterminated array", Pos);
                    if (_s[Pos] == ',') { Pos++; continue; }
                    if (_s[Pos] == ']') { Pos++; return a; }
                    throw new JsonParseException("Expected ',' or ']'", Pos);
                }
            }

            private string ParseString()
            {
                Pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw new JsonParseException("Unterminated string", Pos);
                    char c = _s[Pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw new JsonParseException("Unterminated escape", Pos);
                    char e = _s[Pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (Pos + 4 > _s.Length) throw new JsonParseException("Bad unicode escape", Pos);
                            sb.Append((char)int.Parse(_s.Substring(Pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            Pos += 4;
                            break;
                        default: throw new JsonParseException("Bad escape '\\" + e + "'", Pos);
                    }
                }
            }

            private object ParseNumber()
            {
                int start = Pos;
                bool isFloat = false;
                if (_s[Pos] == '-') Pos++;
                while (!AtEnd)
                {
                    char c = _s[Pos];
                    if (c >= '0' && c <= '9') { Pos++; continue; }
                    if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') { isFloat = true; Pos++; continue; }
                    break;
                }
                string text = _s.Substring(start, Pos - start);
                if (!isFloat && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)) return l;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
                throw new JsonParseException("Invalid number '" + text + "'", start);
            }
        }
    }

    /// <summary>Typed accessors with precise error paths, shared by the level and save loaders.</summary>
    public static class JsonAccess
    {
        public static JsonObject AsObject(object v, string path)
        {
            if (v is JsonObject o) return o;
            throw new LevelDataException(path + " must be an object");
        }

        public static List<object> AsArray(object v, string path)
        {
            if (v is List<object> a) return a;
            throw new LevelDataException(path + " must be an array");
        }

        public static int AsInt(object v, string path)
        {
            if (v is long l) { if (l < int.MinValue || l > int.MaxValue) throw new LevelDataException(path + " is out of int range"); return (int)l; }
            if (v is double d && Math.Abs(d - Math.Round(d)) < 1e-9) return (int)Math.Round(d);
            throw new LevelDataException(path + " must be an integer, got " + Describe(v));
        }

        public static bool AsBool(object v, string path)
        {
            if (v is bool b) return b;
            throw new LevelDataException(path + " must be true or false, got " + Describe(v));
        }

        public static string AsString(object v, string path)
        {
            if (v is string s) return s;
            throw new LevelDataException(path + " must be a string, got " + Describe(v));
        }

        public static object Require(JsonObject o, string key, string path)
        {
            if (!o.TryGetValue(key, out var v)) throw new LevelDataException(path + "." + key + " is missing");
            return v;
        }

        public static int RequireInt(JsonObject o, string key, string path) => AsInt(Require(o, key, path), path + "." + key);
        public static string RequireString(JsonObject o, string key, string path) => AsString(Require(o, key, path), path + "." + key);

        public static int OptionalInt(JsonObject o, string key, int fallback, string path) =>
            o.TryGetValue(key, out var v) && v != null ? AsInt(v, path + "." + key) : fallback;

        public static bool OptionalBool(JsonObject o, string key, bool fallback, string path) =>
            o.TryGetValue(key, out var v) && v != null ? AsBool(v, path + "." + key) : fallback;

        public static string OptionalString(JsonObject o, string key, string fallback, string path) =>
            o.TryGetValue(key, out var v) && v != null ? AsString(v, path + "." + key) : fallback;

        public static string Describe(object v)
        {
            if (v == null) return "null";
            if (v is string) return "string";
            if (v is bool) return "boolean";
            if (v is long || v is double) return "number";
            if (v is JsonObject) return "object";
            return "array";
        }
    }

    public sealed class LevelDataException : Exception
    {
        public LevelDataException(string message) : base(message) { }
        public LevelDataException(string message, Exception inner) : base(message, inner) { }
    }
}
