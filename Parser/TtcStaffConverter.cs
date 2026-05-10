using StaffGenerator.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

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

            var type = src.trainClass ?? "";

            if (type == "特急")
            {
                var match = Regex.Match(src.trainTemplate, @"（([^ ）]+)");
                if (match.Success)
                {
                    type = match.Groups[1].Value;
                }
            }

            return new StaffTrain
            {
                OperationNumber = src.operationNumber,
                TrainName = src.trainNumber ?? "",
                TrainType = type,
                TrainTypeImgName = src.trainClass ?? "",
                TrainDestination = src.destinationStationName ?? "",
                TrainNote = src.staffComment,
                PreviousTrainNumber = src.previousTrainNumber ?? "",
                NextTrainNumber = src.nextTrainNumber ?? "",
                IsDownward = isDownward,   // 追加
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
        /// 先頭の入換：後エントリの発時刻のみ残す
        /// 末尾の入換：前エントリの着時刻のみ残す
        /// </summary>
        private static List<(TTC_StationData data, bool isArrShunting, bool isDepShunting)> MergeShuntingStations(
            List<TTC_StationData> src)
        {
            var result = new List<(TTC_StationData, bool, bool)>();

            for (int i = 0; i < src.Count; i++)
            {
                var cur = src[i];

                if (i + 1 < src.Count && src[i + 1].stationID == cur.stationID)
                {
                    var next = src[i + 1];
                    var isLast = (i + 1 == src.Count - 1);

                    var merged = new TTC_StationData
                    {
                        stationID = isLast ? cur.stationID : next.stationID,
                        stationName = isLast ? cur.stationName : next.stationName,
                        stopPosName = isLast ? cur.stopPosName : next.stopPosName,
                        bansen = isLast ? cur.bansen : next.bansen,
                        script = isLast ? cur.script : next.script,
                        isSaiji = isLast ? cur.isSaiji : next.isSaiji,
                        biko = isLast ? cur.biko : next.biko,
                        StopType = isLast ? cur.StopType : next.StopType,
                        // 末尾：前エントリの着時刻のみ残す、先頭：後エントリの発時刻のみ残す
                        arrivalTime = isLast ? cur.arrivalTime : next.arrivalTime,
                        departureTime = isLast ? cur.departureTime : next.departureTime
                    };

                    result.Add((merged, isArrShunting: !isLast, isDepShunting: isLast));
                    i++;
                }
                else
                {
                    result.Add((cur, isArrShunting: false, isDepShunting: false));
                }
            }

            return result;
        }

        /// <summary>
        /// TTC_StationDataをStaffStationに変換する
        /// </summary>
        private static StaffStation ConvertStation(
            (TTC_StationData data, bool isArrShunting, bool isDepShunting) src,
            StationMasterTable master,
            bool isDownward)
        {
            var (sta, isArrShunting, isDepShunting) = src;
            var arr = ToTimeSpan(sta.arrivalTime);
            var dep = ToTimeSpan(sta.departureTime);

            return new StaffStation
            {
                StationID = sta.stationID,   // 追加
                DisplayName = master.ResolveDisplayName(sta.stationID, sta.stationName ?? ""),
                ArrivalTime = arr,
                DepartureTime = dep,
                IsTimingPoint = sta.isSaiji,
                StopType = sta.StopType,
                TrackNumber = sta.bansen ?? "",
                DoorDirection = master.ResolveDoorDirection(sta.stationID, sta.bansen ?? "", isDownward),
                Note = sta.biko ?? "",
                IsArrShunting = isArrShunting,
                IsDepShunting = isDepShunting,
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
    }
}