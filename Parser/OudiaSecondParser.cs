using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OuDiaSecondParser
{
    /// <summary>
    /// OuDiaSecond(.oud2)形式のテキストをJSON互換構造にパースする
    /// </summary>
    public static class OudiaSecondParser
    {
        /// <summary>
        /// ノード（階層）を表す
        /// </summary>
        private sealed class Node
        {
            public string Name { get; }

            // 値は string か List<string> を入れる（JSON互換）
            public Dictionary<string, List<object>> Props { get; } = new();

            public Dictionary<string, List<Node>> Children { get; } = new();

            public Node(string name) => Name = name;

            public void AddProp(string key, object value)
            {
                if (!Props.TryGetValue(key, out var list))
                {
                    list = new List<object>();
                    Props[key] = list;
                }
                list.Add(value);
            }

            public void AddChild(Node child)
            {
                if (!Children.TryGetValue(child.Name, out var list))
                {
                    list = new List<Node>();
                    Children[child.Name] = list;
                }
                list.Add(child);
            }
        }

        /// <summary>
        /// ファイルを読み込んで、階層構造をパースする
        /// </summary>
        public static object ParseToJsonCompatibleObject(string filePath)
        {
            using var sr = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);

            var root = new Node("Root");
            var stack = new Stack<Node>();
            stack.Push(root);

            while (!sr.EndOfStream)
            {
                var raw = sr.ReadLine();
                if (raw == null) continue;

                var line = raw.Trim();
                if (line.Length == 0) continue;

                // 階層終了
                if (line == ".")
                {
                    if (stack.Count > 1) stack.Pop();
                    continue;
                }

                // 階層開始（末尾がドット）
                if (line.EndsWith(".", StringComparison.Ordinal))
                {
                    var name = line.Substring(0, line.Length - 1).Trim();
                    if (name.Length == 0) continue;

                    var child = new Node(name);
                    stack.Peek().AddChild(child);
                    stack.Push(child);
                    continue;
                }

                // key=value
                var eqIndex = line.IndexOf('=');
                if (eqIndex >= 1)
                {
                    var key = line.Substring(0, eqIndex).Trim();
                    var valueRaw = line.Substring(eqIndex + 1); // value側は空白も含める

                    var parsedValue = ParseValueCommaArray(valueRaw);
                    stack.Peek().AddProp(key, parsedValue);
                }
                // それ以外の行は無視（必要ならログ出し可）
            }

            return ConvertNodeToJsonCompatible(root);
        }

        /// <summary>
        /// valueを "," を区切りとして配列化して返す（なければ文字列）
        /// </summary>
        private static object ParseValueCommaArray(string valueRaw)
        {
            // そのままが良いケース（空文字など）
            if (valueRaw == null) return "";

            // カンマがなければ文字列で返す
            if (!valueRaw.Contains(',')) return valueRaw;

            // カンマがあれば配列として返す（空要素も保持）
            var parts = valueRaw.Split(',')
                                .Select(x => x.Trim())
                                .ToList();

            return parts;
        }

        /// <summary>
        /// JSON互換になる形（Dictionary/List/string）に変換する
        /// </summary>
        private static object ConvertNodeToJsonCompatible(Node node)
        {
            var dict = new Dictionary<string, object>();

            // プロパティ
            foreach (var kv in node.Props)
            {
                // keyが1回だけならその値（string or List<string>）を直置き
                // 複数回なら配列にする（要素が配列になることもある）
                dict[kv.Key] = kv.Value.Count == 1 ? kv.Value[0] : kv.Value.ToList();
            }

            // 子要素
            foreach (var ck in node.Children)
            {
                var children = ck.Value.Select(ConvertNodeToJsonCompatible).ToList();
                dict[ck.Key] = children.Count == 1 ? children[0] : children;
            }

            return dict;
        }

        /// <summary>
        /// パースして、整形済みJSON文字列にする
        /// </summary>
        public static string ParseToJsonString(string filePath)
        {
            var obj = ParseToJsonCompatibleObject(filePath);

            var opt = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 日本語を \u にしない
            };

            return JsonSerializer.Serialize(obj, opt);
        }
    }
}
