using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace StaffGenerator.Parser
{
    public static class TtcJsonValidator
    {
        public class Issue
        {
            public int TrainIndex { get; init; }
            public string Path { get; init; } = "";
            public string Message { get; init; } = "";
            public override string ToString() => $"Train[{TrainIndex}] {Path}: {Message}";
        }

        // JSON文字列を検査して、欠落・型不整合・配列長不一致などを報告する
        public static List<Issue> Validate(string json)
        {
            var issues = new List<Issue>();
            if (string.IsNullOrWhiteSpace(json))
            {
                issues.Add(new Issue { TrainIndex = -1, Path = "$", Message = "入力JSONが空です。" });
                return issues;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                issues.Add(new Issue { TrainIndex = -1, Path = "$", Message = $"JSONパース失敗: {ex.Message}" });
                return issues;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new Issue { TrainIndex = -1, Path = "$", Message = "ルートはオブジェクトである必要があります（trainList プロパティを期待）。" });
                    return issues;
                }

                if (!root.TryGetProperty("trainList", out var trainListElem))
                {
                    issues.Add(new Issue { TrainIndex = -1, Path = "$.trainList", Message = "必須プロパティが存在しません。" });
                    return issues;
                }

                if (trainListElem.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new Issue { TrainIndex = -1, Path = "$.trainList", Message = "trainList が配列ではありません。" });
                    return issues;
                }

                int ti = 0;
                foreach (var trainElem in trainListElem.EnumerateArray())
                {
                    // 基本的な必須フィールド存在チェック
                    CheckProp(trainElem, "operationNumber", JsonValueKind.Number, issues, ti);
                    CheckProp(trainElem, "trainNumber", JsonValueKind.String, issues, ti);
                    CheckProp(trainElem, "trainClass", JsonValueKind.String, issues, ti);
                    CheckProp(trainElem, "originStationID", JsonValueKind.String, issues, ti);
                    CheckProp(trainElem, "destinationStationID", JsonValueKind.String, issues, ti);
                    // staList は配列であることが期待
                    if (trainElem.TryGetProperty("staList", out var staList))
                    {
                        if (staList.ValueKind != JsonValueKind.Array)
                        {
                            issues.Add(new Issue { TrainIndex = ti, Path = "$.trainList[].staList", Message = "staList は配列であるべきです。" });
                        }
                        else
                        {
                            int si = 0;
                            foreach (var s in staList.EnumerateArray())
                            {
                                // 駅情報の必須チェック（少なくとも stationName, arrivalTime/departureTime の構造）
                                if (!s.TryGetProperty("stationName", out var _))
                                    issues.Add(new Issue { TrainIndex = ti, Path = $"$.trainList[{ti}].staList[{si}].stationName", Message = "欠落しています。" });

                                if (s.TryGetProperty("arrivalTime", out var at))
                                {
                                    if (at.ValueKind != JsonValueKind.Object)
                                        issues.Add(new Issue { TrainIndex = ti, Path = $"$.trainList[{ti}].staList[{si}].arrivalTime", Message = "オブジェクト型ではありません。" });
                                    else
                                    {
                                        if (!at.TryGetProperty("h", out var _)) issues.Add(new Issue { TrainIndex = ti, Path = $"$.trainList[{ti}].staList[{si}].arrivalTime.h", Message = "欠落しています。" });
                                        if (!at.TryGetProperty("m", out var _)) issues.Add(new Issue { TrainIndex = ti, Path = $"$.trainList[{ti}].staList[{si}].arrivalTime.m", Message = "欠落しています。" });
                                        if (!at.TryGetProperty("s", out var _)) issues.Add(new Issue { TrainIndex = ti, Path = $"$.trainList[{ti}].staList[{si}].arrivalTime.s", Message = "欠落しています。" });
                                    }
                                }
                                else
                                {
                                    // arrivalTime が存在しないのは必ずしもエラー（h==-1 が無い場合等）だが報告はする
                                    issues.Add(new Issue { TrainIndex = ti, Path = $"$.trainList[{ti}].staList[{si}].arrivalTime", Message = "存在しません（null/欠落）。" });
                                }

                                if (!s.TryGetProperty("departureTime", out var dt))
                                    issues.Add(new Issue { TrainIndex = ti, Path = $"$.trainList[{ti}].staList[{si}].departureTime", Message = "存在しません（null/欠落）。" });
                                else if (dt.ValueKind != JsonValueKind.Object)
                                    issues.Add(new Issue { TrainIndex = ti, Path = $"$.trainList[{ti}].staList[{si}].departureTime", Message = "オブジェクト型ではありません。" });

                                si++;
                            }
                        }
                    }
                    else
                    {
                        issues.Add(new Issue { TrainIndex = ti, Path = "$.trainList[].staList", Message = "欠落しています。" });
                    }

                    // 型が null になってしまう可能性のある nullable-like フィールドチェック
                    if (trainElem.TryGetProperty("temporaryStopStations", out var tss) && tss.ValueKind == JsonValueKind.Null)
                    {
                        issues.Add(new Issue { TrainIndex = ti, Path = "$.trainList[].temporaryStopStations", Message = "null です（期待する場合を除く）。" });
                    }

                    ti++;
                }
            }

            return issues;
        }

        private static void CheckProp(JsonElement elem, string name, JsonValueKind expected, List<Issue> issues, int trainIndex)
        {
            if (!elem.TryGetProperty(name, out var p))
            {
                issues.Add(new Issue { TrainIndex = trainIndex, Path = $"$.trainList[{trainIndex}].{name}", Message = "欠落しています。" });
                return;
            }
            if (p.ValueKind != expected && !(expected == JsonValueKind.String && p.ValueKind == JsonValueKind.Null))
            {
                // 文字列期待だが null の場合はレポート（モデルでは null 可のものがあるため）
                issues.Add(new Issue { TrainIndex = trainIndex, Path = $"$.trainList[{trainIndex}].{name}", Message = $"期待型: {expected}, 実際: {p.ValueKind}" });
            }
        }
    }
}