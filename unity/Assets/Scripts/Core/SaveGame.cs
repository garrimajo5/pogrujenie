using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Сохранение Unity-версии (перенос js/save.js): груз трюма и поднятые со дна образцы.
    /// Формат — JSON той же формы, что снимок браузерной версии:
    /// {"v":1,"cargo":[{"kind":"moss","i":0}],"got":[0]}. Лишние поля (виды, места, улучшения)
    /// при чтении пропускаются, неверные записи груза отбрасываются, как в M.restore.
    /// </summary>
    public static class SaveGame
    {
        public const int V = 1;

        static readonly string[] Keys = { "moss", "shell", "coral", "spine" };

        public static string Key(SampleKind k) => Keys[(int)k];

        public static bool TryKind(string key, out SampleKind k)
        {
            int i = Array.IndexOf(Keys, key);
            k = i < 0 ? SampleKind.Moss : (SampleKind)i;
            return i >= 0;
        }

        /// <summary>Снимок: груз трюма по порядку и номера поднятых образцов по возрастанию.</summary>
        public static string Write(Samples m)
        {
            var sb = new StringBuilder();
            sb.Append("{\"v\":").Append(V).Append(",\"cargo\":[");
            for (int i = 0; i < m.Cargo.Count; i++)
            {
                var c = m.Cargo[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"kind\":\"").Append(Key(c.Kind)).Append("\",\"i\":");
                sb.Append(c.I >= 0 ? c.I.ToString(CultureInfo.InvariantCulture) : "null").Append('}');
            }
            sb.Append("],\"got\":[");
            var got = new List<int>(m.Got);
            got.Sort();
            for (int i = 0; i < got.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(got[i].ToString(CultureInfo.InvariantCulture));
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Прочитать снимок в образцы. false — снимок не наш (пустой, битый, другая версия): тогда
        /// образцы не трогаем. Иначе состояние сбрасывается и восстанавливается из снимка.
        /// </summary>
        public static bool Read(string text, Samples m)
        {
            if (string.IsNullOrEmpty(text)) return false;
            object root;
            try { root = new Json(text).Parse(); }
            catch (FormatException) { return false; }
            if (!(root is Dictionary<string, object> d)) return false;
            if (!d.TryGetValue("v", out var v) || !(v is double dv) || dv != V) return false;

            var got = new List<int>();
            if (d.TryGetValue("got", out var g) && g is List<object> gl)
                foreach (var x in gl) if (AsIndex(x, out int i)) got.Add(i);

            var cargo = new List<CargoItem>();
            if (d.TryGetValue("cargo", out var c) && c is List<object> cl)
                foreach (var x in cl)
                {
                    if (!(x is Dictionary<string, object> e)) continue;
                    if (!e.TryGetValue("kind", out var k) || !(k is string ks) || !TryKind(ks, out var kind)) continue;
                    int i = -1;
                    if (e.TryGetValue("i", out var iv) && iv != null && !AsIndex(iv, out i)) continue;
                    cargo.Add(new CargoItem(kind, i));
                }
            m.Restore(got, cargo);
            return true;
        }

        static bool AsIndex(object x, out int i)
        {
            i = -1;
            if (!(x is double dx) || dx < 0 || dx > int.MaxValue || Math.Floor(dx) != dx) return false;
            i = (int)dx;
            return true;
        }

        /// <summary>Небольшой разбор JSON: объекты, массивы, строки, числа (double), true/false/null.</summary>
        sealed class Json
        {
            readonly string s; int p;
            public Json(string s) { this.s = s; }

            public object Parse()
            {
                var v = Value();
                Ws();
                if (p != s.Length) throw new FormatException("лишнее в конце");
                return v;
            }

            void Ws() { while (p < s.Length && char.IsWhiteSpace(s[p])) p++; }

            char Peek() { Ws(); if (p >= s.Length) throw new FormatException("конец"); return s[p]; }

            void Expect(char ch) { if (Peek() != ch) throw new FormatException("ждали " + ch); p++; }

            object Value()
            {
                char ch = Peek();
                if (ch == '{') return Obj();
                if (ch == '[') return Arr();
                if (ch == '"') return Str();
                if (Word("true")) return true;
                if (Word("false")) return false;
                if (Word("null")) return null;
                return Num();
            }

            bool Word(string w)
            {
                if (string.CompareOrdinal(s, p, w, 0, w.Length) != 0) return false;
                p += w.Length;
                return true;
            }

            Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>();
                Expect('{');
                if (Peek() == '}') { p++; return d; }
                while (true)
                {
                    if (Peek() != '"') throw new FormatException("ключ");
                    string k = Str();
                    Expect(':');
                    d[k] = Value();
                    char ch = Peek(); p++;
                    if (ch == '}') return d;
                    if (ch != ',') throw new FormatException("ждали ,");
                }
            }

            List<object> Arr()
            {
                var a = new List<object>();
                Expect('[');
                if (Peek() == ']') { p++; return a; }
                while (true)
                {
                    a.Add(Value());
                    char ch = Peek(); p++;
                    if (ch == ']') return a;
                    if (ch != ',') throw new FormatException("ждали ,");
                }
            }

            string Str()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (p >= s.Length) throw new FormatException("строка");
                    char ch = s[p++];
                    if (ch == '"') return sb.ToString();
                    if (ch != '\\') { sb.Append(ch); continue; }
                    if (p >= s.Length) throw new FormatException("строка");
                    char e = s[p++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (p + 4 > s.Length || !int.TryParse(s.Substring(p, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int u))
                                throw new FormatException("\\u");
                            sb.Append((char)u); p += 4; break;
                        default: sb.Append(e); break;
                    }
                }
            }

            double Num()
            {
                int a = p;
                while (p < s.Length && "+-0123456789.eE".IndexOf(s[p]) >= 0) p++;
                if (a == p || !double.TryParse(s.Substring(a, p - a), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    throw new FormatException("число");
                return v;
            }
        }
    }
}
