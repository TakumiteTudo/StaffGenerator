using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OuDiaSecondParser
{
    /// <summary>
    /// oud2ファイルを読み込み、互換性のある列車データ(JSON互換)に変換する
    /// </summary>
    public static class OudiaSecondTrainConverter
    {
        #region JSON出力用モデル

        /// <summary>
        /// 出力ルート
        /// </summary>
        public sealed class TrainCompatRoot
        {
            public List<TrainCompat> trainList { get; set; } = new();
            public List<StationInfoCompat> stationInfoList { get; set; } = new();
        }

        /// <summary>
        /// 列車データ（互換）
        /// </summary>
        public sealed class TrainCompat
        {
            public string trainNumber { get; set; } = "";
            public string trainClass { get; set; } = "";
            public List<StaCompat> staList { get; set; } = new();
            /// <summary>
            /// Operation行をそのまま保持する（キー例: "Operation3A"）
            /// </summary>
            public Dictionary<string, string>? operationRaw { get; set; }
        }

        /// <summary>
        /// 停車駅データ（互換）
        /// </summary>
        public sealed class StaCompat
        {
            public string stationID { get; set; } = "";
            public string stationName { get; set; } = "";
            public string bansen { get; set; } = "";
            public TimeObj arrivalTime { get; set; } = new();
            public TimeObj departureTime { get; set; } = new();

            public override string ToString()
            {
                return $"{stationID}@{arrivalTime}/{departureTime}${bansen}";
            }
        }


        /// <summary>
        /// 時刻オブジェクト（h/m/s）
        /// </summary>
        public sealed class TimeObj
        {
            public int h { get; set; }
            public int m { get; set; }
            public int s { get; set; }
            public override string ToString()
            {
                return $"{h:00}:{m:00}.{s:00}";
            }
        }

        /// <summary>
        /// 駅情報データ（非互換）
        /// </summary>
        public sealed class StationInfoCompat
        {
            public string stationID { get; set; } = "";
            public string stationName { get; set; } = "";
            public double staPos { get; set; } = 0.0d;
            public List<string> trackList { get; set; } = new();
        }

        /// <summary>
        /// 列車データの一時保存用（駅ID表が後ろに出るので遅延変換する）
        /// </summary>
        private sealed class TempTrain
        {
            public int syubetsuIndex;
            public string ressyaBangou = "";
            public string ekiJikokuRaw = "";
            public string houkou = ""; // "Kudari" / "Nobori" 等
        }


        #endregion

        /// <summary>
        /// oud2ファイルを変換してJSON文字列として返す
        /// </summary>
        public static string ConvertToJsonString(string oud2Path)
        {
            var root = ConvertToObject(oud2Path);

            var opt = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            return JsonSerializer.Serialize(root, opt);
        }

        /// <summary>
        /// oud2ファイルを変換してオブジェクトとして返す（OudiaSecondParserの結果を利用して可読性UP）
        /// </summary>
        public static TrainCompatRoot ConvertToObject(string oud2Path)
        {
            // まず汎用パーサでツリー化
            var parsed = OudiaSecondParser.ParseToJsonCompatibleObject(oud2Path);
            var root = AsDict(parsed) ?? new Dictionary<string, object>();

            // 駅/種別は基本 Rosen 配下を見る（なければroot直下をfallback）
            var rosen = FindFirstNode(root, "Rosen") ?? root;

            // 駅名 + 駅ごとの番線名(TrackRyakusyou)
            var stationNames = ExtractStationNames(rosen);
            var stationTrackNames = ExtractStationTrackNames(rosen);

            // 種別テーブル
            var trainClassTable = ExtractTrainClassTable(rosen);

            // Commentから駅ID表（Commentは末尾でもOKなので全体走査）
            var stationIdMap = ExtractStationIdMap(root);

            // ★ Commentからキロ程(stationPos)表を抽出
            var stationPosMap = ExtractStationPosMap(root);

            // ★ キロ程を駅数ぶんに補間展開
            var stationPosList = BuildStationPosList(stationNames.Count, stationPosMap);

            // 列車を全部拾って互換構造に変換
            var result = new TrainCompatRoot();

            // ★ stationInfoList を作る
            for (int i = 0; i < stationNames.Count; i++)
            {
                var stationId = stationIdMap.TryGetValue(i, out var sid) ? sid : i.ToString();
                var pos = stationPosList[i];

                var tracks = (i >= 0 && i < stationTrackNames.Count)
                    ? stationTrackNames[i]
                    : new List<string>();

                result.stationInfoList.Add(new StationInfoCompat
                {
                    stationID = stationId,
                    stationName = stationNames[i],
                    staPos = pos,
                    trackList = tracks
                });
            }

            foreach (var ressya in FindAllNodes(root, "Ressya"))
            {
                var trainNumber = GetString(ressya, "Ressyabangou");
                var houkou = GetString(ressya, "Houkou");
                var syubetsuIndex = GetInt(ressya, "Syubetsu", -1);
                var ekiJikokuRaw = GetRawCommaValue(ressya, "EkiJikoku");

                bool reverse = string.Equals(houkou, "Nobori", StringComparison.OrdinalIgnoreCase);

                var train = new TrainCompat
                {
                    trainNumber = trainNumber,
                    trainClass = ResolveTrainClass(trainClassTable, syubetsuIndex),
                    staList = BuildStaList(stationNames, stationTrackNames, stationIdMap, ekiJikokuRaw, reverse),
                    operationRaw = ExtractOperationRaw(ressya)
                };

                result.trainList.Add(train);
            }

            MergeNextTrainConnectionType3(result.trainList);

            return result;
        }

        /// <summary>
        /// Ressya直下の Operation**** を全部拾う
        /// </summary>
        private static Dictionary<string, string> ExtractOperationRaw(Dictionary<string, object> ressya)
        {
            var dic = new Dictionary<string, string>();

            foreach (var kv in ressya)
            {
                if (!kv.Key.StartsWith("Operation", StringComparison.Ordinal)) continue;

                // Operationはstring想定（パーサ都合でobjectでもToStringで拾う）
                dic[kv.Key] = kv.Value?.ToString() ?? "";
            }

            return dic;
        }

        #region 汎用ユーティリティ（JSON互換ツリーを安全に読む）

        /// <summary>
        /// objectをDictionaryに変換する
        /// </summary>
        private static Dictionary<string, object>? AsDict(object? obj)
            => obj as Dictionary<string, object>;

        /// <summary>
        /// objectをListに変換する
        /// </summary>
        private static List<object>? AsList(object? obj)
            => obj as List<object>;

        /// <summary>
        /// dict[key] を string として取得する（なければ空）
        /// </summary>
        private static string GetString(Dictionary<string, object> dict, string key, string def = "")
        {
            if (!dict.TryGetValue(key, out var v) || v == null) return def;

            if (v is string s) return s;
            if (v is List<string> ls) return string.Join(",", ls);
            if (v is List<object> lo) return string.Join(",", lo.Select(x => x?.ToString() ?? ""));
            return v.ToString() ?? def;
        }

        /// <summary>
        /// dict[key] を int として取得する
        /// </summary>
        private static int GetInt(Dictionary<string, object> dict, string key, int def = 0)
        {
            var s = GetString(dict, key, "");
            return int.TryParse(s.Trim(), out var n) ? n : def;
        }

        /// <summary>
        /// dict[key] の値が配列化されてても、","区切りの生文字列に戻す（空要素も保持）
        /// </summary>
        private static string GetRawCommaValue(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var v) || v == null) return "";

            // OudiaSecondParserは "," を List<string> にする :contentReference[oaicite:2]{index=2}
            if (v is List<string> ls) return string.Join(",", ls);
            if (v is List<object> lo) return string.Join(",", lo.Select(x => x?.ToString() ?? ""));
            if (v is string s) return s;

            return v.ToString() ?? "";
        }

        /// <summary>
        /// ツリーから「最初のノード」を探す（key名一致）
        /// </summary>
        private static Dictionary<string, object>? FindFirstNode(Dictionary<string, object> root, string keyName)
        {
            foreach (var node in FindAllNodes(root, keyName))
                return node;
            return null;
        }

        /// <summary>
        /// ツリーを再帰的に辿って、指定キー名のノードを全部拾う
        /// </summary>
        private static IEnumerable<Dictionary<string, object>> FindAllNodes(object? any, string keyName)
        {
            if (any is Dictionary<string, object> dict)
            {
                // 自分の子として持ってる場合
                if (dict.TryGetValue(keyName, out var found))
                {
                    // found が単体dict or list of dict
                    if (found is Dictionary<string, object> fd)
                        yield return fd;

                    if (found is List<object> fl)
                    {
                        foreach (var x in fl)
                        {
                            var d = AsDict(x);
                            if (d != null) yield return d;
                        }
                    }
                }

                // 再帰
                foreach (var v in dict.Values)
                {
                    foreach (var r in FindAllNodes(v, keyName))
                        yield return r;
                }
            }
            else if (any is List<object> list)
            {
                foreach (var x in list)
                {
                    foreach (var r in FindAllNodes(x, keyName))
                        yield return r;
                }
            }
        }

        #endregion

        #region 抽出処理（駅/番線/種別/Comment）

        /// <summary>
        /// Rosen配下の駅名一覧を抽出する（Ekiの並び順を保持）
        /// </summary>
        private static List<string> ExtractStationNames(Dictionary<string, object> rosen)
        {
            var names = new List<string>();

            // Rosen -> Eki が list or dict の可能性があるので FindAllNodesではなく直接取る
            if (rosen.TryGetValue("Eki", out var ekiObj))
            {
                if (ekiObj is List<object> ekis)
                {
                    foreach (var e in ekis)
                    {
                        var d = AsDict(e);
                        if (d == null) continue;
                        names.Add(GetString(d, "Ekimei"));
                    }
                }
                else
                {
                    var d = AsDict(ekiObj);
                    if (d != null) names.Add(GetString(d, "Ekimei"));
                }
            }

            return names;
        }

        /// <summary>
        /// Rosen配下の「駅ごとの番線名(TrackRyakusyou)」を抽出する
        /// Eki.EkiTrack2Cont[].EkiTrack2.TrackRyakusyou
        /// </summary>
        private static List<List<string>> ExtractStationTrackNames(Dictionary<string, object> rosen)
        {
            var result = new List<List<string>>();

            if (!rosen.TryGetValue("Eki", out var ekiObj))
                return result;

            // 駅配列
            var ekiList = AsList(ekiObj) ?? new List<object> { ekiObj };

            foreach (var ekiAny in ekiList)
            {
                var eki = AsDict(ekiAny);
                if (eki == null)
                {
                    result.Add(new List<string>());
                    continue;
                }

                var tracks = new List<string>();

                if (eki.TryGetValue("EkiTrack2Cont", out var contObj))
                {
                    var contList = AsList(contObj) ?? new List<object> { contObj };

                    foreach (var contAny in contList)
                    {
                        var cont = AsDict(contAny);
                        if (cont == null) continue;

                        if (!cont.TryGetValue("EkiTrack2", out var track2Obj)) continue;

                        // EkiTrack2 が単体/配列どっちもありうる
                        var track2List = AsList(track2Obj) ?? new List<object> { track2Obj };
                        foreach (var track2Any in track2List)
                        {
                            var track2 = AsDict(track2Any);
                            if (track2 == null) continue;

                            tracks.Add(GetString(track2, "TrackRyakusyou"));
                        }
                    }
                }

                result.Add(tracks);
            }

            return result;
        }

        /// <summary>
        /// Rosen配下の種別テーブルを抽出する（Syubetsu=Index参照用）
        /// </summary>
        private static List<string> ExtractTrainClassTable(Dictionary<string, object> rosen)
        {
            var table = new List<string>();

            if (rosen.TryGetValue("Ressyasyubetsu", out var obj))
            {
                var list = AsList(obj) ?? new List<object> { obj };

                foreach (var any in list)
                {
                    var d = AsDict(any);
                    if (d == null) continue;
                    table.Add(GetString(d, "Syubetsumei"));
                }
            }

            return table;
        }

        /// <summary>
        /// ツリー全体からCommentを拾い、stationIndexマップを構築する（末尾でもOK）
        /// </summary>
        private static Dictionary<int, string> ExtractStationIdMap(Dictionary<string, object> root)
        {
            var map = new Dictionary<int, string>();

            foreach (var commentOwner in FindAllNodes(root, "Comment"))
            {
                // FindAllNodesは「ノード検索」なのでここでは使わない
            }

            // Commentは props（文字列）なので dict全体を走査して拾う
            foreach (var comment in CollectAllPropertyStrings(root, "Comment"))
            {
                var parsed = ParseStationIdMapFromComment(comment);
                foreach (var kv in parsed)
                    map[kv.Key] = kv.Value; // 後勝ち
            }

            return map;
        }

        /// <summary>
        /// ツリー全体からCommentを拾い、stationPosマップ(index→キロ程)を構築する
        /// </summary>
        private static Dictionary<int, double> ExtractStationPosMap(Dictionary<string, object> root)
        {
            var map = new Dictionary<int, double>();

            foreach (var comment in CollectAllPropertyStrings(root, "Comment"))
            {
                var parsed = ParseStationPosMapFromComment(comment);
                foreach (var kv in parsed)
                    map[kv.Key] = kv.Value; // 後勝ち
            }

            return map;
        }


        /// <summary>
        /// ツリーから指定キーの文字列プロパティを全部拾う
        /// </summary>
        private static IEnumerable<string> CollectAllPropertyStrings(object? any, string keyName)
        {
            if (any is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue(keyName, out var v) && v is string s)
                    yield return s;

                foreach (var x in dict.Values)
                {
                    foreach (var r in CollectAllPropertyStrings(x, keyName))
                        yield return r;
                }
            }
            else if (any is List<object> list)
            {
                foreach (var x in list)
                {
                    foreach (var r in CollectAllPropertyStrings(x, keyName))
                        yield return r;
                }
            }
        }

        #endregion


        /// <summary>
        /// 種別Indexを種別名に変換する
        /// </summary>
        private static string ResolveTrainClass(List<string> trainClassTable, int syubetsuIndex)
        {
            if (syubetsuIndex < 0) return "";
            if (syubetsuIndex >= trainClassTable.Count) return syubetsuIndex.ToString();
            return trainClassTable[syubetsuIndex];
        }

        /// <summary>
        /// EkiJikokuをstaListに変換する（駅番目対応）
        /// Houkou=Nobori の場合は駅順が逆なので反転する
        /// bansen は駅ごとのTrackRyakusyou配列を使って実番線に変換する
        /// </summary>
        private static List<StaCompat> BuildStaList(
            List<string> stationNames,
            List<List<string>> stationTrackNames,
            Dictionary<int, string> stationIdMap,
            string ekiJikokuRaw,
            bool reverseOrder)
        {
            var list = new List<StaCompat>();
            if (string.IsNullOrEmpty(ekiJikokuRaw)) return list;

            var tokens = ekiJikokuRaw.Split(',');
            var stationCount = stationNames.Count;
            var max = Math.Min(tokens.Length, stationCount);

            for (int i = 0; i < max; i++)
            {
                var token = tokens[i].Trim();
                if (string.IsNullOrEmpty(token)) continue;

                var parsed = ParseEkiJikokuToken(token);
                if (parsed == null) continue;

                int stationIndex = reverseOrder ? (stationCount - 1 - i) : i;

                parsed.stationID = stationIdMap.TryGetValue(stationIndex, out var sid) ? sid : stationIndex.ToString();
                parsed.stationName = stationNames[stationIndex];

                // ★番線を index -> TrackRyakusyou に変換
                parsed.bansen = ResolveBansenName(
                    parsed.bansen,
                    stationIndex,
                    stationTrackNames
                );

                list.Add(parsed);
            }

            return list;
        }

        /// <summary>
        /// bansen(番線index) を TrackRyakusyou に変換する
        /// </summary>
        private static string ResolveBansenName(string bansenIndexRaw, int stationIndex, List<List<string>> stationTrackNames)
        {
            if (string.IsNullOrEmpty(bansenIndexRaw)) return "";

            // $の右側は "1" とか "4" の数字で来る前提
            if (!int.TryParse(bansenIndexRaw.Trim(), out var trackIndex))
                return bansenIndexRaw;

            if (stationIndex < 0 || stationIndex >= stationTrackNames.Count)
                return bansenIndexRaw;

            var tracks = stationTrackNames[stationIndex];
            if (tracks == null || tracks.Count == 0)
                return bansenIndexRaw;

            // oud2のindexが 0開始/1開始 どっちか不明なので両対応する（先に0開始を試す）
            if (trackIndex >= 0 && trackIndex < tracks.Count)
                return tracks[trackIndex];

            if (trackIndex - 1 >= 0 && trackIndex - 1 < tracks.Count)
                return tracks[trackIndex - 1];

            return bansenIndexRaw;
        }


        /// <summary>
        /// EkiJikokuの要素1つを解析する
        /// </summary>
        private static StaCompat? ParseEkiJikokuToken(string token)
        {
            // まず番線($)を抜く
            string bansen = "";
            string main = token;

            var dollar = token.LastIndexOf('$');
            if (dollar >= 0 && dollar + 1 < token.Length)
            {
                bansen = token.Substring(dollar + 1).Trim();
                main = token.Substring(0, dollar).Trim();
            }

            // "1;52345/52405" の ";" を抜く（停止種別などは今は捨てる）
            var sem = main.IndexOf(';');
            if (sem >= 0 && sem + 1 < main.Length)
            {
                main = main.Substring(sem + 1).Trim();
            }

            if (string.IsNullOrEmpty(main))
            {
                // 時刻情報が無いケース（例: "2$1" のみ等）
                return new StaCompat
                {
                    bansen = bansen,
                    arrivalTime = new TimeObj(),
                    departureTime = new TimeObj()
                };
            }

            // 到着/発車
            TimeObj arr;
            TimeObj dep;

            var slash = main.IndexOf('/');
            if (slash >= 0)
            {
                var a = main.Substring(0, slash).Trim();
                var d = main.Substring(slash + 1).Trim();

                // "50650/" みたいに後ろが空もあるのでケア
                arr = ParseTime(a);
                dep = string.IsNullOrEmpty(d) ? arr : ParseTime(d);
            }
            else
            {
                arr = ParseTime(main);
                dep = arr;
            }

            return new StaCompat
            {
                bansen = bansen,
                arrivalTime = arr,
                departureTime = dep
            };
        }

        /// <summary>
        /// oud2の時刻表記をTimeObjに変換する
        /// 通過などで時刻が無い場合は -1:-1:-1 を返す
        /// </summary>
        private static TimeObj ParseTime(string digits)
        {
            digits = (digits ?? "").Trim();

            // ★時刻が無い（通過など）
            if (digits.Length == 0)
            {
                return new TimeObj { h = -1, m = -1, s = -1 };
            }

            // 数字以外が混ざってたら無効扱い
            if (!digits.All(char.IsDigit))
            {
                return new TimeObj { h = -1, m = -1, s = -1 };
            }

            int h = 0, m = 0, s = 0;

            if (digits.Length == 3)
            {
                h = int.Parse(digits.Substring(0, 1));
                m = int.Parse(digits.Substring(1, 2));
                s = 0;
            }
            else if (digits.Length == 4)
            {
                h = int.Parse(digits.Substring(0, 2));
                m = int.Parse(digits.Substring(2, 2));
                s = 0;
            }
            else if (digits.Length == 5)
            {
                h = int.Parse(digits.Substring(0, 1));
                m = int.Parse(digits.Substring(1, 2));
                s = int.Parse(digits.Substring(3, 2));
            }
            else if (digits.Length == 6)
            {
                h = int.Parse(digits.Substring(0, 2));
                m = int.Parse(digits.Substring(2, 2));
                s = int.Parse(digits.Substring(4, 2));
            }
            else
            {
                return new TimeObj { h = -1, m = -1, s = -1 };
            }

            return new TimeObj { h = h, m = m, s = s };
        }


        /// <summary>
        /// Comment文字列から [stationIndex] を解析して index→stationID の辞書を作る
        /// </summary>
        private static Dictionary<int, string> ParseStationIdMapFromComment(string commentRaw)
        {
            var result = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(commentRaw)) return result;

            var normalized = commentRaw
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\n");

            var startTag = "[stationIndex]";
            var nextTag = "[stationPos]";
            var endTag = "[end]";

            var s = normalized.IndexOf(startTag, StringComparison.Ordinal);
            if (s < 0) return result;

            // ★ stationIndexセクションの終端は [stationPos] があればそこまで、なければ [end] まで
            var e1 = normalized.IndexOf(nextTag, s, StringComparison.Ordinal);
            var e2 = normalized.IndexOf(endTag, s, StringComparison.Ordinal);

            int e;
            if (e1 >= 0) e = e1;
            else if (e2 >= 0) e = e2;
            else return result;

            var body = normalized.Substring(s + startTag.Length, e - (s + startTag.Length));

            var lines = body.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                var eq = line.IndexOf('=');
                if (eq < 1) continue;

                var left = line.Substring(0, eq).Trim();
                var right = line.Substring(eq + 1).Trim();

                // 範囲指定 3..55=TH<02..> の対応
                if (left.Contains(".."))
                {
                    var parts = left.Split(new[] { ".." }, StringSplitOptions.None);
                    if (parts.Length != 2) continue;

                    if (!int.TryParse(parts[0], out var from)) continue;
                    if (!int.TryParse(parts[1], out var to)) continue;
                    if (to < from) continue;

                    if (TryParseAngleRangePattern(right, out var prefix, out var startNumber, out var width))
                    {
                        for (int idx = from; idx <= to; idx++)
                        {
                            var num = startNumber + (idx - from);
                            result[idx] = prefix + num.ToString().PadLeft(width, '0');
                        }
                    }
                    else
                    {
                        for (int idx = from; idx <= to; idx++)
                            result[idx] = right;
                    }
                }
                else
                {
                    if (!int.TryParse(left, out var index)) continue;
                    result[index] = right;
                }
            }

            return result;
        }

        /// <summary>
        /// Comment文字列から [stationPos]～[end] を解析して index→キロ程(double) の辞書を作る
        /// </summary>
        private static Dictionary<int, double> ParseStationPosMapFromComment(string commentRaw)
        {
            var result = new Dictionary<int, double>();
            if (string.IsNullOrEmpty(commentRaw)) return result;

            var normalized = commentRaw
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\n");

            var startTag = "[stationPos]";
            var endTag = "[end]";

            var s = normalized.IndexOf(startTag, StringComparison.Ordinal);
            if (s < 0) return result;

            var e = normalized.IndexOf(endTag, s, StringComparison.Ordinal);
            if (e < 0) return result;

            var body = normalized.Substring(s + startTag.Length, e - (s + startTag.Length));

            var lines = body.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                var eq = line.IndexOf('=');
                if (eq < 1) continue;

                var left = line.Substring(0, eq).Trim();
                var right = line.Substring(eq + 1).Trim();

                if (!int.TryParse(left, out var index)) continue;

                // 小数点入り想定（-47.6 とか）
                if (!double.TryParse(right, out var pos)) continue;

                result[index] = pos;
            }

            return result;
        }


        /// <summary>
        /// TH<02..> を prefix="TH" startNumber=2 width=2 に変換する
        /// </summary>
        private static bool TryParseAngleRangePattern(string input, out string prefix, out int startNumber, out int width)
        {
            prefix = "";
            startNumber = 0;
            width = 0;

            // 形式: (prefix)<(number)..>
            var lt = input.IndexOf('<');
            var dots = input.IndexOf("..", StringComparison.Ordinal);
            var gt = input.IndexOf('>');

            if (lt < 0 || dots < 0 || gt < 0) return false;
            if (!(lt < dots && dots < gt)) return false;

            prefix = input.Substring(0, lt).Trim();

            var numStr = input.Substring(lt + 1, dots - (lt + 1)).Trim();
            if (!int.TryParse(numStr, out startNumber)) return false;

            width = numStr.Length;
            return true;
        }
        /// <summary>
        /// 「次列車接続タイプ=3(同一扱)」の列車を、同一列番の次列車と結合する
        /// </summary>
        public static void MergeNextTrainConnectionType3(List<TrainCompat> trains)
        {
            // trainNumberごとにまとめる
            var groups = trains
                .Where(t => !string.IsNullOrEmpty(t.trainNumber))
                .GroupBy(t => t.trainNumber)
                .ToDictionary(g => g.Key, g => g.ToList());

            var removeSet = new HashSet<TrainCompat>();

            foreach (var kv in groups)
            {
                var list = kv.Value;

                // 時刻順に並べる（始発時刻）
                list.Sort((a, b) => GetStartTimeSec(a).CompareTo(GetStartTimeSec(b)));

                for (int i = 0; i < list.Count; i++)
                {
                    var cur = list[i];
                    if (removeSet.Contains(cur)) continue;

                    // curが「次列車接続タイプ3」を持っているか
                    if (!HasNextTrainConnectionType3(cur))
                        continue;

                    var curEnd = GetEndTimeSec(cur);
                    if (curEnd < 0) continue;

                    // 次列車候補を探す（同じ列番の中で、開始が未来で一番近い）
                    TrainCompat? next = null;
                    int nextStart = int.MaxValue;

                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var cand = list[j];
                        if (removeSet.Contains(cand)) continue;

                        var s = GetStartTimeSec(cand);
                        if (s < 0) continue;

                        if (s >= curEnd && s < nextStart)
                        {
                            next = cand;
                            nextStart = s;
                        }
                    }

                    if (next == null) continue;

                    // 結合
                    MergeTrain(cur, next);

                    // nextは消す
                    removeSet.Add(next);
                }
            }

            // リストから削除
            trains.RemoveAll(t => removeSet.Contains(t));
        }

        /// <summary>
        /// 「次列車接続 5/[hhmmss]$[0-3]」でタイプ3があるか
        /// (時刻が空の "5/$0" みたいなのにも対応)
        /// </summary>
        private static bool HasNextTrainConnectionType3(TrainCompat train)
        {
            if (train.operationRaw == null) return false;

            foreach (var op in train.operationRaw.Values)
            {
                if (string.IsNullOrEmpty(op)) continue;

                // 作業はカンマ区切り
                var works = op.Split(',');
                foreach (var w in works)
                {
                    var s = w.Trim();

                    // 次列車接続は "5/....$3"
                    if (!s.StartsWith("5/", StringComparison.Ordinal)) continue;

                    var dollar = s.LastIndexOf('$');
                    if (dollar < 0) continue;

                    var right = s.Substring(dollar + 1).Trim();

                    // タイプ3だけ検出
                    if (right == "3") return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 列車同士を結合する（接続駅は「着=前列車」「発=後列車」で合成）
        /// </summary>
        private static void MergeTrain(TrainCompat cur, TrainCompat next)
        {
            if (cur.staList == null) cur.staList = new List<StaCompat>();
            if (next.staList == null || next.staList.Count == 0) return;

            // 末尾駅と次列車の先頭駅が同じなら、到着＝前列車・発車＝後列車として合成
            if (cur.staList.Count > 0)
            {
                var last = cur.staList[cur.staList.Count - 1];
                var first = next.staList[0];

                if (!string.IsNullOrEmpty(last.stationID) &&
                    last.stationID == first.stationID)
                {
                    // 到着は前列車（last）、発車は後列車（first）
                    last.arrivalTime = last.arrivalTime ?? new TimeObj();
                    last.departureTime = first.departureTime ?? new TimeObj();

                    // 番線は「発車側（後列車）」を優先（必要なら好みで逆でもOK）
                    if (!string.IsNullOrEmpty(first.bansen))
                        last.bansen = first.bansen;

                    // 先頭駅を除外して連結
                    cur.staList.AddRange(next.staList.Skip(1));
                    return;
                }
            }

            // 駅が違うなら普通に連結
            cur.staList.AddRange(next.staList);
        }


        /// <summary>
        /// 始発時刻(秒)を求める（最初に時刻が入ってる駅を採用）
        /// </summary>
        private static int GetStartTimeSec(TrainCompat train)
        {
            if (train.staList == null) return -1;

            foreach (var s in train.staList)
            {
                var t = FirstValidTime(s);
                if (t >= 0) return t;
            }

            return -1;
        }

        /// <summary>
        /// 終着時刻(秒)を求める（最後に時刻が入ってる駅を採用）
        /// </summary>
        private static int GetEndTimeSec(TrainCompat train)
        {
            if (train.staList == null) return -1;

            for (int i = train.staList.Count - 1; i >= 0; i--)
            {
                var t = LastValidTime(train.staList[i]);
                if (t >= 0) return t;
            }

            return -1;
        }

        /// <summary>
        /// 駅の到着/発車から「最初に使える時刻」を取る
        /// </summary>
        private static int FirstValidTime(StaCompat s)
        {
            var dep = ToSec(s.departureTime);
            if (dep >= 0) return dep;

            var arr = ToSec(s.arrivalTime);
            if (arr >= 0) return arr;

            return -1;
        }

        /// <summary>
        /// 駅の到着/発車から「最後に使える時刻」を取る
        /// </summary>
        private static int LastValidTime(StaCompat s)
        {
            var dep = ToSec(s.departureTime);
            if (dep >= 0) return dep;

            var arr = ToSec(s.arrivalTime);
            if (arr >= 0) return arr;

            return -1;
        }

        /// <summary>
        /// TimeObjを秒に変換（空っぽなら-1）
        /// </summary>
        private static int ToSec(TimeObj t)
        {
            if (t == null) return -1;

            // h,m,sが全部0でも「本当に0時」と区別が付かないけど、
            // oud2のデータ的には大抵0:00は運用上出にくいので、ここは0を許容する
            if (t.h < 0 || t.m < 0 || t.s < 0) return -1;
            if (t.m >= 60 || t.s >= 60) return -1;

            return t.h * 3600 + t.m * 60 + t.s;
        }
        /// <summary>
        /// stationPosMap（部分）を駅数ぶんの配列に展開し、不足分を線形補間して埋める
        /// </summary>
        private static List<double> BuildStationPosList(int stationCount, Dictionary<int, double> stationPosMap)
        {
            var posList = new double[stationCount];

            // 既知点ゼロなら駅順でフォールバック（とりあえず重ならない）
            if (stationPosMap == null || stationPosMap.Count == 0)
            {
                for (int i = 0; i < stationCount; i++)
                    posList[i] = i;
                return posList.ToList();
            }

            // 既知点を index 昇順で並べる
            var keys = stationPosMap.Keys
                .Where(k => k >= 0 && k < stationCount)
                .OrderBy(k => k)
                .ToList();

            if (keys.Count == 0)
            {
                for (int i = 0; i < stationCount; i++)
                    posList[i] = i;
                return posList.ToList();
            }

            // 先頭側（最初の既知点より前）は最初の値で埋める
            int firstK = keys[0];
            double firstV = stationPosMap[firstK];

            for (int i = 0; i <= firstK; i++)
                posList[i] = firstV;

            // 既知点同士の間を線形補間
            for (int p = 0; p < keys.Count - 1; p++)
            {
                int k1 = keys[p];
                int k2 = keys[p + 1];

                double v1 = stationPosMap[k1];
                double v2 = stationPosMap[k2];

                int span = k2 - k1;
                if (span <= 0) continue;

                // k1 自身
                posList[k1] = v1;

                // k1+1 ～ k2-1 を補間
                for (int i = k1 + 1; i < k2; i++)
                {
                    double t = (double)(i - k1) / span;
                    posList[i] = v1 + (v2 - v1) * t;
                }

                // k2 自身
                posList[k2] = v2;
            }

            // 末尾側（最後の既知点より後）は最後の値で埋める
            int lastK = keys[^1];
            double lastV = stationPosMap[lastK];

            for (int i = lastK; i < stationCount; i++)
                posList[i] = lastV;

            return posList.ToList();
        }

    }
}
