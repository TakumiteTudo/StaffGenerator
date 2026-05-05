using StaffGenerator.Model;
using System.CodeDom;
using System.Diagnostics;

namespace StaffGenerator.Parser
{
    /// <summary>
    /// 全列車のスタフ備考を生成・付加する
    /// </summary>
    public static class StaffNoteGenerator
    {
        private record StationEntry(StaffTrain Train, StaffStation Station, int StaIndex);

        private static TimeSpan MAX_KOUKAN_MINUTE = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 全列車にスタフ備考を付加する
        /// 既存備考がある場合は\n区切りで末尾に連結する
        /// </summary>
        public static void Apply(
            List<StaffTrain> trains,
            StaffNoteAbbreviationTable abbrTable,
            RouteConfig routeConfig)
        {
            var idx = BuildIndex(trains);
            foreach (var selftrain in trains)
            {
                foreach (var othertrain in trains)
                {
                    if (selftrain == othertrain) continue;

                    //運転時間が被っていない場合は交換は起こらない
                    if (
                        selftrain.StaffStations[0].DepartureTime - MAX_KOUKAN_MINUTE < othertrain.StaffStations[othertrain.StaffStations.Count - 1].ArrivalTime
                        &&
                        othertrain.StaffStations[0].DepartureTime < selftrain.StaffStations[selftrain.StaffStations.Count - 1].ArrivalTime
                        )
                    {
                        Debug.WriteLine($"☆{selftrain.TrainName}/{othertrain.TrainName}");

                    }
                }
            }
        }

        // ─────────────────────────────
        // 交換
        // ─────────────────────────────

        /// <summary>交換備考を生成する（対向列車が発時刻5分前以内に現駅に存在）</summary>
        private static IEnumerable<string> GetCrossNotes(
            StaffTrain self,
            StaffStation selfSta,
            Dictionary<string, List<StationEntry>> idx,
            StaffNoteAbbreviationTable abbr)
        {
            var dep = selfSta.DepartureTime!.Value;
            var windowStart = dep - TimeSpan.FromMinutes(5);

            if (!idx.TryGetValue(selfSta.StationID, out var entries)) yield break;

            foreach (var e in entries)
            {
                if (e.Train == self) continue;
                if (e.Train.IsDownward == self.IsDownward) continue; // 対向列車のみ

                TimeSpan? checkTime;
                string suffix;

                if (e.Station.StopType == StopType.Pass)
                {
                    // 通過列車：通過時刻を使用
                    checkTime = e.Station.ArrivalTime ?? e.Station.DepartureTime;
                    suffix = "通";
                }
                else
                {
                    // 停車列車：到着時刻を使用
                    checkTime = e.Station.ArrivalTime;
                    suffix = "着";
                }

                if (checkTime is not TimeSpan ct) continue;
                if (ct < windowStart || ct > dep) continue;

                yield return $"× {abbr.Class(e.Train.TrainType)} {abbr.Dest(e.Train.TrainDestination)} {ct.Minutes:D2}{suffix}";
            }
        }

        // ─────────────────────────────
        // インデックス構築
        // ─────────────────────────────

        private static Dictionary<string, List<StationEntry>> BuildIndex(List<StaffTrain> trains)
        {
            var dict = new Dictionary<string, List<StationEntry>>();
            foreach (var train in trains)
            {
                for (int i = 0; i < train.StaffStations.Count; i++)
                {
                    var sta = train.StaffStations[i];
                    if (string.IsNullOrEmpty(sta.StationID)) continue;
                    if (!dict.TryGetValue(sta.StationID, out var list))
                        dict[sta.StationID] = list = new();
                    list.Add(new StationEntry(train, sta, i));
                }
            }
            return dict;
        }


        // ─────────────────────────────
        // ユーティリティ
        // ─────────────────────────────

        private static void AppendNote(StaffStation sta, string note)
        {
            sta.Note = string.IsNullOrEmpty(sta.Note) ? note : sta.Note + "\n" + note;
        }

        private static void AppendNote(StaffStation sta, IEnumerable<string> notes)
        {
            foreach (var n in notes) AppendNote(sta, n);
        }

        /// <summary>
        /// 数字文字列を丸付き数字へ変換
        /// </summary>
        /// <param name="text">変換元文字列</param>
        /// <returns>丸付き数字文字列</returns>
        private static string ConvertToCircledNumber(string? text)
        {
            if (text == null) return "";
            if (!int.TryParse(text, out int number))
            {
                return text;
            }

            //
            // 0
            //
            if (number == 0)
            {
                return "";
            }

            //
            // ① ～ ⑳
            //
            if (number >= 1 && number <= 20)
            {
                return char.ConvertFromUtf32(
                    0x2460 + (number - 1));
            }

            //
            // ㉑ ～ ㉟
            //
            if (number >= 21 && number <= 35)
            {
                return char.ConvertFromUtf32(
                    0x3251 + (number - 21));
            }

            //
            // ㊱ ～ ㊿
            //
            if (number >= 36 && number <= 50)
            {
                return char.ConvertFromUtf32(
                    0x32B1 + (number - 36));
            }

            //
            // Unicode未定義範囲は元文字列返却
            //
            return text;
        }
    }
}