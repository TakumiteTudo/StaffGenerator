using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using StaffGenerator.Model;

namespace StaffGenerator.Parser
{
    public static class TtcStaffConverter
    {
        public static List<StaffTrain> ConvertFromFile(string jsonPath, StationMasterTable master)
        {
            var json = File.ReadAllText(jsonPath);
            return ConvertFromJson(json, master);
        }

        public static List<StaffTrain> ConvertFromJson(string json, StationMasterTable master)
        {
            var opt = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                IncludeFields = true // ← ここを追加：public フィールドを含める
            };

            if (string.IsNullOrWhiteSpace(json))
                throw new JsonException("入力JSONが空です。trainListプロパティを含むJSONを指定してください。");

            var trimmed = json.TrimStart();

            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("JSONのルートはオブジェクトである必要があります。trainListプロパティを含めてください。");

            if (!root.TryGetProperty("trainList", out var arr) || arr.ValueKind != JsonValueKind.Array)
                throw new JsonException("必須プロパティ 'trainList' が存在しないか配列ではありません。読み込みを中止します。");

            var ttcList = JsonSerializer.Deserialize<List<TTC_Train>>(arr.GetRawText(), opt)
                          ?? new List<TTC_Train>();

            return ttcList.Select(t => ConvertTrain(t, master)).ToList();
        }

        /// <summary>
        /// TTC_TrainをStaffTrainに変換する
        /// </summary>
        private static StaffTrain ConvertTrain(TTC_Train src, StationMasterTable master)
        {
            var staList = src.staList ?? new List<TTC_StationData>();
            var isDownward = ResolveIsDownward(staList);
            var mergedList = MergeShuntingStations(staList);

            //Debug.WriteLine($"{src.trainNumber}");

            return new StaffTrain
            {
                TrainName = src.trainNumber ?? "",
                TrainType = src.trainClass ?? "",
                TrainTypeImgName = src.trainClass ?? "",
                TrainDestination = src.destinationStationName ?? "",
                TrainNote = "",
                StaffStations = mergedList
                    .Select(s => ConvertStation(s, master, isDownward))
                    .ToList()
            };
        }

        /// <summary>
        /// stopPosNameの「下り/上り」から列車方向を判定する（デフォルト：下り）
        /// </summary>
        private static bool ResolveIsDownward(List<TTC_StationData> staList)
        {
            foreach (var s in staList)
            {
                if (string.IsNullOrEmpty(s.stopPosName)) continue;
                if (s.stopPosName.Contains("下り")) return true;
                if (s.stopPosName.Contains("上り")) return false;
            }
            return true;
        }

        /// <summary>
        /// 同一stationIDが連続する場合を入換として統合する
        /// 前エントリの時刻は破棄し、後エントリの発車時刻のみ残す
        /// </summary>
        private static List<(TTC_StationData data, bool isShunting)> MergeShuntingStations(
            List<TTC_StationData> src)
        {
            var result = new List<(TTC_StationData, bool)>();

            for (int i = 0; i < src.Count; i++)
            {
                var cur = src[i];

                if (i + 1 < src.Count && src[i + 1].stationID == cur.stationID)
                {
                    var next = src[i + 1];
                    var merged = new TTC_StationData
                    {
                        stationID = next.stationID,
                        stationName = next.stationName,
                        stopPosName = next.stopPosName,
                        bansen = next.bansen,
                        script = next.script,
                        isSaiji = next.isSaiji,
                        biko = next.biko,
                        arrivalTime = new TTC_TimeOfDay { h = -1 }, // 到着時刻破棄
                        departureTime = next.departureTime
                    };

                    result.Add((merged, isShunting: true));
                    i++;
                }
                else
                {
                    result.Add((cur, isShunting: false));
                }
            }

            return result;
        }

        /// <summary>
        /// TTC_StationDataをStaffStationに変換する
        /// </summary>
        private static StaffStation ConvertStation(
            (TTC_StationData data, bool isShunting) src,
            StationMasterTable master,
            bool isDownward)
        {
            var (sta, isShunting) = src;
            var arr = ToTimeSpan(sta.arrivalTime);
            var dep = ToTimeSpan(sta.departureTime);

            return new StaffStation
            {
                DisplayName = master.ResolveDisplayName(sta.stationID, sta.stationName ?? ""),
                ArrivalTime = arr,
                DepartureTime = dep,
                IsTimingPoint = sta.isSaiji,
                StopType = sta.StopType,
                TrackNumber = sta.bansen ?? "",
                DoorDirection = master.ResolveDoorDirection(sta.stationID, sta.bansen ?? "", isDownward),
                Note = sta.biko ?? "",
                IsShunting = isShunting,
                Script = sta.script ?? ""
            };
        }

        /// <summary>
        /// TTC_TimeOfDayをTimeSpan?に変換する（h==-1は時刻なし → null）
        /// </summary>
        private static TimeSpan? ToTimeSpan(TTC_TimeOfDay t)
        {
            if (t == null || t.h < 0) return null;
            return new TimeSpan(t.h, t.m, t.s);
        }

        /// <summary>
        /// 着発時刻の有無からStopTypeを推定する
        /// 両方nullなら通過、片方でも有効なら停車
        /// </summary>
        private static StopType ResolveStopType(TimeSpan? arr, TimeSpan? dep)
        {
            return (arr == null && dep == null) ? StopType.Pass : StopType.Stop;
        }
    }
}