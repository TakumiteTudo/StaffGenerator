using StaffGenerator.Model;
using System.Diagnostics;

namespace StaffGenerator.Parser
{
    /// <summary>
    /// 全列車のスタフ備考を生成・付加する
    /// </summary>
    public static class StaffNoteGenerator
    {
        /// <summary>交換の時間ウィンドウ（分）</summary>
        private const int CrossingWindowMinutes = 12;

        /// <summary>接続の最大時間差（分）</summary>        
        private const int GeneralConnectionMaxMinutes = 30;
        private const int ExpressConnectionMaxMinutes = 30;
        private const int OvertakeMaxMinutes = 20;

        /// <summary>着発時刻のどちらを使うかの指定</summary>
        private enum TimeType
        {
            /// <summary>到着時刻（着）</summary>
            Arrival,
            /// <summary>出発時刻（発）</summary>
            Departure,
            /// <summary>通過時刻（通）※到着→出発の順でnullでない方を使用</summary>
            Pass
        }

        /// <summary>
        /// 接続ペアの種別
        /// </summary>
        public enum ConnectionType
        {
            /// <summary>終着駅での接続・連絡</summary>
            Terminal,
            /// <summary>途中通過駅カバーのための接続</summary>
            SkipCover,
        }

        private record StationEntry(StaffTrain Train, StaffStation Station, int StaIndex);
        private record PendingNote(StaffStation Station, TimeSpan Time, string Note, StaffTrain? OtherTrain = null);

        /// <summary>一般接続ペア</summary>
        private record GeneralConnectionPair(
            StaffTrain ArrivalTrain, StaffStation ArrivalStation,
            StaffTrain DepartureTrain, StaffStation DepartureStation,
            ConnectionType ConnectionType
            );

        /// <summary>特急へ接続ペア</summary>
        private record ToExpressPair(
            StaffTrain IncomingTrain, StaffStation IncomingStation,
            StaffTrain ExpressTrain, StaffStation ExpressStation);

        /// <summary>特急から接続ペア</summary>
        private record FromExpressPair(
            StaffTrain ExpressTrain, StaffStation ExpressStation,
            StaffTrain OutgoingTrain, StaffStation OutgoingStation);

        /// <summary>待避ペア</summary>
        private record OvertakePair(
            StaffTrain WaitingTrain, StaffStation WaitingStation,
            StaffTrain PassingTrain, StaffStation PassingStation);

        private record PassCoverPair(
            StaffTrain SelfTrain,
            StaffStation SelfStation,
            StaffTrain CoveringTrain,
            StaffStation CoveringStation);


        // ─────────────────────────────
        // 公開メソッド
        // ─────────────────────────────

        /// <summary>
        /// 全列車にスタフ備考を付加する
        /// 全種別の備考を収集したうえで時刻順にまとめて付加する
        /// </summary>
        public static void Apply(
            List<StaffTrain> trains,
            StaffNoteAbbreviationTable abbrTable,
            RouteConfig routeConfig)
        {
            var idx = BuildIndex(trains);
            var notes = new List<PendingNote>();

            foreach (var train in trains)
                CollectCrossingNotes(train, idx, abbrTable, routeConfig, notes);

            var generalPairs = ExtractGeneralConnectionPairs(trains, idx);
            var toExpress = ExtractToExpressPairs(trains, idx);
            var fromExpress = ExtractFromExpressPairs(trains, idx);
            var overtakePairs = ExtractOvertakePairs(trains, idx);

            CollectGeneralConnectionNotes(generalPairs, abbrTable, notes);
            CollectToExpressNotes(toExpress, abbrTable, notes);
            CollectFromExpressNotes(fromExpress, abbrTable, notes);
            CollectOvertakeNotes(overtakePairs, abbrTable, notes);

            foreach (var group in notes.GroupBy(n => n.Station))
            {
                var deduped = DeduplicateNotes(group.ToList());
                AppendNote(group.Key, deduped.OrderBy(n => n.Time).Select(n => n.Note));
            }
        }

        // ─────────────────────────────
        // ペア抽出
        // ─────────────────────────────

        private static List<GeneralConnectionPair> ExtractGeneralConnectionPairs(
            List<StaffTrain> trains,
            Dictionary<string, List<StationEntry>> idx)
        {
            var candidates = new List<GeneralConnectionPair>();

            foreach (var self in trains)
            {

                if (IsExpress(self)) continue;
                if (IsDeadheadOrTest(self)) continue;

                foreach (var sta in self.StaffStations)
                {
                    // 終着駅かどうか判定
                    bool isTerminal = sta.DisplayName == self.TrainDestination;

                    // 次停車駅までの通過駅セット
                    var staIndex = self.StaffStations.IndexOf(sta);

                    var skippedStationIDs = self.StaffStations
                        .Skip(staIndex + 1)
                        .TakeWhile(s => s.StopType == StopType.Pass)
                        .Select(s => s.StationID)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToHashSet();

                    bool hasSkip = skippedStationIDs.Count > 0;

                    // 終着でも通過カバーでもなければスキップ
                    if (!isTerminal && !hasSkip) continue;
                    if (sta.StopType == StopType.Pass) continue;
                    if (sta.ArrivalTime is not TimeSpan selfArr) continue;
                    if (!idx.TryGetValue(sta.StationID, out var entries)) continue;

                    var connCandidates = entries
                        .Where(e =>
                            e.Train != self &&
                            e.Train.IsDownward == self.IsDownward &&
                            e.Station.StopType != StopType.Pass &&
                            !IsExpress(e.Train) &&
                            !IsDeadheadOrTest(e.Train) &&
                            e.Train.TrainDestination != sta.DisplayName &&
                            e.Station.DepartureTime is TimeSpan d &&
                            d > selfArr &&
                            (d - selfArr).TotalMinutes <= GeneralConnectionMaxMinutes)
                        .OrderBy(e => e.Station.DepartureTime)
                        .ToList();

                    // 終着接続：先発を先頭に通過カバーなしで追加
                    if (isTerminal)
                    {
                        var uncovered = new HashSet<string>(skippedStationIDs);
                        foreach (var conn in connCandidates)
                        {
                            var covered = skippedStationIDs
                                .Where(id => StopsAtStation(conn.Train, conn.Station.StationID, id))
                                .ToList();

                            bool isFirst = !candidates.Any(p =>
                                p.ArrivalTrain == self &&
                                p.ArrivalStation == sta &&
                                p.ConnectionType == ConnectionType.Terminal);

                            if (isFirst || covered.Any(id => uncovered.Contains(id)))
                            {
                                candidates.Add(new GeneralConnectionPair(
                                    self, sta, conn.Train, conn.Station,
                                    ConnectionType.Terminal));
                                foreach (var id in covered) uncovered.Remove(id);
                            }
                            if (uncovered.Count == 0) break;
                        }
                    }

                    // 通過カバー接続：一般優等列車が途中で通過駅を持つ場合
                    // 通過カバー接続：一般優等列車が途中で通過駅を持つ場合
                    if (hasSkip && !isTerminal)
                    {
                        var uncovered = new HashSet<string>(skippedStationIDs);
                        foreach (var conn in connCandidates)
                        {
                            var covered = skippedStationIDs
                                .Where(id => StopsAtStation(conn.Train, conn.Station.StationID, id))
                                .ToList();

                            // 修正：通過駅を1つ以上カバーする場合のみ追加（isFirst無条件追加を廃止）
                            if (covered.Any(id => uncovered.Contains(id)))
                            {
                                candidates.Add(new GeneralConnectionPair(
                                    self, sta, conn.Train, conn.Station,
                                    ConnectionType.SkipCover));
                                foreach (var id in covered) uncovered.Remove(id);
                            }

                            if (uncovered.Count == 0) break;
                        }
                    }
                }
            }

            // Terminal のみ重複除去フィルタを適用
            var terminalCandidates = candidates
                .Where(p => p.ConnectionType == ConnectionType.Terminal)
                .ToList();

            var skipCoverCandidates = candidates
                .Where(p => p.ConnectionType == ConnectionType.SkipCover)
                .ToList();

            // 同一出発列車・駅に対して最も遅い到着を残す
            var filtered = terminalCandidates
                .GroupBy(p => (p.DepartureTrain, p.DepartureStation.StationID))
                .Select(g => g.OrderByDescending(p => p.ArrivalStation.ArrivalTime).First())
                .ToList();

            // 同一接続元から同一種別・同一行先への出発が複数なら先発のみ残す
            filtered = filtered
                .GroupBy(p => (
                    p.ArrivalTrain,
                    p.ArrivalStation.StationID,
                    p.DepartureTrain.TrainType,
                    p.DepartureTrain.TrainDestination
                ))
                .Select(g => g.OrderBy(p => p.DepartureStation.DepartureTime).First())
                .ToList();

            // SkipCover はフィルタせずそのまま合流
            return ApplyMultiDepartureFilter([.. filtered, .. skipCoverCandidates], idx);
        }

        private static List<ToExpressPair> ExtractToExpressPairs(
            List<StaffTrain> trains,
            Dictionary<string, List<StationEntry>> idx)
        {
            var result = new List<ToExpressPair>();

            foreach (var express in trains.Where(IsExpress))
            {
                Debug.WriteLine($"☆{express.TrainName}");
                var stas = express.StaffStations;
                for (int i = 0; i < stas.Count; i++)
                {
                    var sta = stas[i];
                    if (i == 0 || i == stas.Count - 1) continue;
                    if (sta.StopType == StopType.Pass) continue;
                    if (sta.StopType == StopType.OpStop) continue;
                    if (sta.DepartureTime is not TimeSpan expressDep) continue;

                    Debug.WriteLine($"　駅：{sta.DisplayName}");

                    var prevExpressArr = GetPrevExpressArrival(express, sta, idx);

                    Debug.WriteLine($"　　一本前：{prevExpressArr:hhmmss}");
                    Debug.WriteLine($"　　自列車：{expressDep:hhmmss}");

                    if (!idx.TryGetValue(sta.StationID, out var entries)) continue;

                    foreach (var e in entries)
                    {
                        if (e.Train == express) continue;
                        if (e.Train.IsDownward != express.IsDownward) continue;
                        if (e.Station.StopType != StopType.Stop) continue;
                        if (IsDeadheadOrTest(e.Train)) continue;
                        if (express.TrainDestination == sta.DisplayName) continue;
                        if (e.Station.ArrivalTime is not TimeSpan otherArr) continue;
                        if (prevExpressArr.HasValue && otherArr <= prevExpressArr.Value) continue;
                        if (otherArr >= expressDep) continue;
                        if ((expressDep - otherArr).TotalMinutes > ExpressConnectionMaxMinutes) continue;

                        result.Add(new ToExpressPair(e.Train, e.Station, express, sta));
                    }
                }
            }

            Debug.WriteLine(result.Count);

            return result;
        }

        private static List<FromExpressPair> ExtractFromExpressPairs(
            List<StaffTrain> trains,
            Dictionary<string, List<StationEntry>> idx)
        {
            var result = new List<FromExpressPair>();

            foreach (var express in trains.Where(IsExpress))
            {
                var stas = express.StaffStations;
                for (int i = 0; i < stas.Count; i++)
                {
                    var sta = stas[i];
                    if (i == 0 || i == stas.Count - 1) continue;
                    if (sta.StopType == StopType.Pass) continue;
                    if (sta.DepartureTime is not TimeSpan expressDep) continue;

                    if (!idx.TryGetValue(sta.StationID, out var entries)) continue;

                    // 特急がA→G間で通過する駅IDセット（B,C,D,E,F）
                    var skippedStationIDs = stas
                        .Skip(i + 1)
                        .TakeWhile(s => s.StopType == StopType.Pass)
                        .Select(s => s.StationID)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToHashSet();

                    // 時間制限内の候補を発車時刻順に列挙
                    var candidates = entries
                        .Where(e =>
                            e.Train != express &&
                            e.Train.IsDownward == express.IsDownward &&
                            !IsExpress(e.Train) &&
                            !IsDeadheadOrTest(e.Train) &&
                            e.Train.TrainDestination != sta.DisplayName &&
                            e.Station.StopType != StopType.Pass &&
                            e.Station.DepartureTime is TimeSpan d &&
                            d >= expressDep &&
                            (d - expressDep).TotalMinutes <= ExpressConnectionMaxMinutes)
                        .OrderBy(e => e.Station.DepartureTime)
                        .ToList();

                    if (candidates.Count == 0) continue;

                    // まだカバーされていない通過駅セット
                    var uncovered = new HashSet<string>(skippedStationIDs);

                    foreach (var candidate in candidates)
                    {
                        // この列車がカバーする通過駅（停車する通過駅）
                        var covered = skippedStationIDs
                            .Where(id => StopsAtStation(candidate.Train, candidate.Station.StationID, id))
                            .ToList();

                        bool isFirst = result.All(r =>
                            r.ExpressTrain != express || r.ExpressStation != sta);

                        // 先発（初回）は無条件で追加
                        // 以降は未カバー駅を新たにカバーする場合のみ追加
                        if (isFirst || covered.Any(id => uncovered.Contains(id)))
                        {
                            result.Add(new FromExpressPair(express, sta, candidate.Train, candidate.Station));
                            foreach (var id in covered) uncovered.Remove(id);
                        }

                        if (uncovered.Count == 0) break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 指定列車がfromStationIDより後でtargetStationIDに停車するか返す
        /// </summary>
        private static bool StopsAtStation(
            StaffTrain train, string fromStationID, string targetStationID)
        {
            bool found = false;
            foreach (var sta in train.StaffStations)
            {
                if (sta.StationID == fromStationID) { found = true; continue; }
                if (!found) continue;
                if (sta.StationID == targetStationID)
                    return sta.StopType != StopType.Pass;
            }
            return false;
        }

        private static List<OvertakePair> ExtractOvertakePairs(
            List<StaffTrain> trains,
            Dictionary<string, List<StationEntry>> idx)
        {
            var result = new List<OvertakePair>();

            foreach (var self in trains)
            {
                foreach (var sta in self.StaffStations)
                {
                    if (sta.StopType == StopType.Pass) continue;
                    if (sta.ArrivalTime is not TimeSpan selfArr) continue;
                    if (sta.DepartureTime is not TimeSpan selfDep) continue;

                    if (!idx.TryGetValue(sta.StationID, out var entries)) continue;

                    foreach (var e in entries)
                    {
                        if (e.Train == self) continue;
                        if (e.Train.IsDownward != self.IsDownward) continue;
                        if (IsTest(e.Train)) continue;
                        if (e.Station.StopType != StopType.Pass) continue;

                        var passTime = e.Station.ArrivalTime ?? e.Station.DepartureTime;
                        if (passTime is not TimeSpan pt) continue;
                        if (pt <= selfArr || pt >= selfDep) continue;
                        if ((selfDep - pt).TotalMinutes > OvertakeMaxMinutes) continue;

                        result.Add(new OvertakePair(self, sta, e.Train, e.Station));
                    }
                }
            }

            return result;
        }

        // ─────────────────────────────
        // 備考収集
        // ─────────────────────────────

        private static void CollectCrossingNotes(
            StaffTrain self,
            Dictionary<string, List<StationEntry>> idx,
            StaffNoteAbbreviationTable abbr,
            RouteConfig routeConfig,
            List<PendingNote> notes)
        {
            var stas = self.StaffStations;
            var candidates = new Dictionary<StaffTrain, (int SelfStaIndex, StationEntry Other)>();

            for (int i = 0; i < stas.Count - 1; i++)
            {
                var selfSta = stas[i];
                var nextSta = stas[i + 1];

                if (!routeConfig.IsSingleTrackBetween(selfSta.StationID, nextSta.StationID)) continue;
                if (selfSta.DepartureTime is not TimeSpan selfDep) continue;

                var windowStart = selfDep - TimeSpan.FromMinutes(CrossingWindowMinutes);

                if (!idx.TryGetValue(selfSta.StationID, out var entries)) continue;

                foreach (var e in entries)
                {
                    if (e.Train == self) continue;
                    if (e.Train.IsDownward == self.IsDownward) continue;
                    if (e.Train.OperationNumber == self.OperationNumber) continue;

                    if (!idx.TryGetValue(nextSta.StationID, out var nextEntries)) continue;
                    if (!nextEntries.Any(ne => ne.Train == e.Train)) continue;

                    var checkTime = e.Station.StopType == StopType.Pass
                        ? e.Station.ArrivalTime ?? e.Station.DepartureTime
                        : e.Station.ArrivalTime;

                    if (checkTime is not TimeSpan ct) continue;
                    if (ct < windowStart || ct > selfDep) continue;

                    if (candidates.TryGetValue(e.Train, out var existing))
                    {
                        if (i < existing.SelfStaIndex)
                            candidates[e.Train] = (i, e);
                    }
                    else
                    {
                        candidates[e.Train] = (i, e);
                    }
                }
            }

            foreach (var (_, (staIdx, other)) in candidates)
            {
                var selfSta = stas[staIdx];
                var timeType = other.Station.StopType == StopType.Pass ? TimeType.Pass : TimeType.Arrival;
                var info = FormatOtherTrainInfo(other.Train, other.Station, timeType, abbr);
                if (info is null) continue;

                var time = other.Station.StopType == StopType.Pass
                    ? other.Station.ArrivalTime ?? other.Station.DepartureTime
                    : other.Station.ArrivalTime;
                if (time is null) continue;

                notes.Add(new PendingNote(selfSta, time.Value, $" ×  {info}", other.Train));
            }
        }

        private static void CollectGeneralConnectionNotes(
    List<GeneralConnectionPair> pairs,
    StaffNoteAbbreviationTable abbr,
    List<PendingNote> notes)
        {
            foreach (var p in pairs)
            {
                var prefix = ConnectionPrefix(p.ArrivalStation, p.DepartureStation);
                bool isConnection = prefix == "(接)";
                bool isTerminal = p.ConnectionType == ConnectionType.Terminal;
                bool isFirstStop = IsFirstStopStation(p.DepartureTrain, p.DepartureStation);

                // 前列車（到着側）：相手の発時刻で常に記載
                var depInfo = FormatOtherTrainInfo(p.DepartureTrain, p.DepartureStation, TimeType.Departure, abbr);
                if (depInfo is not null && p.DepartureStation.DepartureTime is TimeSpan depTime)
                    notes.Add(new PendingNote(p.ArrivalStation, depTime, $"{prefix}{depInfo}", p.DepartureTrain));

                // 後列車（出発側）：条件分岐
                bool shouldAddNext = (isConnection, isTerminal) switch
                {
                    // ①A 接続・終点連絡：無条件記載
                    (true, true) => true,
                    // ①B 接続・通過連絡：営業始発駅を除き記載
                    (true, false) => !isFirstStop,
                    // ②A 連絡・終点連絡：営業始発駅のみ記載
                    (false, true) => isFirstStop,
                    // ②B 連絡・通過連絡：記載しない
                    (false, false) => false,
                };

                if (!shouldAddNext) continue;

                var arrInfo = FormatOtherTrainInfo(p.ArrivalTrain, p.ArrivalStation, TimeType.Arrival, abbr);
                if (arrInfo is not null && p.ArrivalStation.ArrivalTime is TimeSpan arrTime)
                    notes.Add(new PendingNote(p.DepartureStation, arrTime, $"{prefix}{arrInfo}", p.ArrivalTrain));
            }
        }

        private static void CollectToExpressNotes(
            List<ToExpressPair> pairs,
            StaffNoteAbbreviationTable abbr,
            List<PendingNote> notes)
        {
            foreach (var p in pairs)
            {

                var prefix = ConnectionPrefix(p.IncomingStation, p.ExpressStation);

                // 一般列車の営業始発駅では、連絡では記載しない
                if (prefix == "(連)" && IsFirstStopStation(p.IncomingTrain, p.IncomingStation)) continue;

                var depInfo = FormatOtherTrainInfo(p.ExpressTrain, p.ExpressStation, TimeType.Departure, abbr);
                if (depInfo is null) continue;
                if (p.ExpressStation.DepartureTime is not TimeSpan depTime) continue;

                notes.Add(new PendingNote(p.IncomingStation, depTime, $"{prefix}{depInfo}", p.ExpressTrain));
            }
        }

        private static void CollectFromExpressNotes(
            List<FromExpressPair> pairs,
            StaffNoteAbbreviationTable abbr,
            List<PendingNote> notes)
        {
            foreach (var p in pairs)
            {
                var prefix = ConnectionPrefix(p.ExpressStation, p.OutgoingStation);
                var depInfo = FormatOtherTrainInfo(p.OutgoingTrain, p.OutgoingStation, TimeType.Departure, abbr);
                if (depInfo is null) continue;
                if (p.OutgoingStation.DepartureTime is not TimeSpan depTime) continue;

                // 特急側は必ず記載                                                              
                notes.Add(new PendingNote(p.ExpressStation, depTime, $"{prefix}{depInfo}", p.OutgoingTrain));

                if (prefix == "(連)") continue;
                var arrInfo = FormatOtherTrainInfo(p.ExpressTrain, p.ExpressStation, TimeType.Arrival, abbr);
                if (arrInfo is null) continue;
                if (p.ExpressStation.ArrivalTime is not TimeSpan arrTime) continue;

                notes.Add(new PendingNote(p.OutgoingStation, arrTime, $"{prefix}{arrInfo}", p.ExpressTrain));
            }
        }

        private static void CollectOvertakeNotes(
            List<OvertakePair> pairs,
            StaffNoteAbbreviationTable abbr,
            List<PendingNote> notes)
        {
            foreach (var p in pairs)
            {
                var info = FormatOtherTrainInfo(p.PassingTrain, p.PassingStation, TimeType.Pass, abbr);
                if (info is null) continue;

                var time = p.PassingStation.ArrivalTime ?? p.PassingStation.DepartureTime;
                if (time is null) continue;

                // 待たされる列車側のみ記載                                                  
                notes.Add(new PendingNote(p.WaitingStation, time.Value, $"[待]{info}", p.PassingTrain));
            }
        }

        // ─────────────────────────────
        // ヘルパー
        // ─────────────────────────────

        /// <summary>
        /// 同一駅・同一相手列車の備考が複数ある場合、発時刻記載を優先して1つに絞る
        /// </summary>
        /// <param name="notes">対象備考リスト（同一駅のもの）</param>
        /// <returns>重複除去済み備考リスト</returns>
        private static List<PendingNote> DeduplicateNotes(List<PendingNote> notes)
        {
            var result = new List<PendingNote>();

            // 相手列車が特定できるものを列車ごとにグループ化して重複除去
            foreach (var group in notes.Where(n => n.OtherTrain != null).GroupBy(n => n.OtherTrain))
            {
                var list = group.ToList();

                // 発時刻記載（"発"を含む）を優先、なければ先頭を採用
                var preferred = list.FirstOrDefault(n => n.Note.Contains("発")) ?? list[0];
                result.Add(preferred);
            }

            // 相手列車不明のもの（交換など）はそのまま追加
            result.AddRange(notes.Where(n => n.OtherTrain == null));

            return result;
        }

        /// <summary>
        /// 2つの停車駅の停車時間帯に重なりがあるか返す
        /// </summary>
        private static bool HasStopOverlap(StaffStation a, StaffStation b)
        {
            if (a.ArrivalTime is not TimeSpan aArr) return false;
            if (a.DepartureTime is not TimeSpan aDep) return false;
            if (b.ArrivalTime is not TimeSpan bArr) return false;
            if (b.DepartureTime is not TimeSpan bDep) return false;
            return aArr < bDep && bArr < aDep;
        }

        /// <summary>停車時間の重複状況から接続・連絡プレフィックスを返す</summary>
        private static string ConnectionPrefix(StaffStation a, StaffStation b)
            => HasStopOverlap(a, b) ? "(接)" : "(連)";

        /// <summary>特急列車かどうかを返す</summary>
        private static bool IsExpress(StaffTrain train)
            => train.TrainType.Contains("特");

        /// <summary>回送・試運転列車かどうかを返す</summary>
        private static bool IsDeadheadOrTest(StaffTrain train)
            => train.TrainType.Contains("回送") || train.TrainType.Contains("試運転");

        /// <summary>試運転列車かどうかを返す</summary>
        private static bool IsTest(StaffTrain train)
            => train.TrainType.Contains("試運転");

        /// <summary>指定駅における1つ前の同方向特急の到着時刻を返す</summary>
        private static TimeSpan? GetPrevExpressArrival(
            StaffTrain express,
            StaffStation station,
            Dictionary<string, List<StationEntry>> idx)
        {
            if (!idx.TryGetValue(station.StationID, out var entries)) return null;
            if (station.ArrivalTime is not TimeSpan selfArr) return null;

            return entries
                .Where(e =>
                    e.Train != express &&
                    IsExpress(e.Train) &&
                    e.Train.IsDownward == express.IsDownward &&
                    e.Station.ArrivalTime is TimeSpan t &&
                    t < selfArr)
                .Select(e => e.Station.ArrivalTime!.Value)
                .OrderByDescending(t => t)
                .Cast<TimeSpan?>()
                .FirstOrDefault();
        }

        /// <summary>
        /// 指定列車がfromStationIDからtoStationIDの区間でtoStationIDを通過扱いするか返す
        /// </summary>
        private static bool PassesThroughStation(
            StaffTrain train, string fromStationID, string toStationID)
        {
            bool found = false;
            foreach (var sta in train.StaffStations)
            {
                if (sta.StationID == fromStationID) { found = true; continue; }
                if (!found) continue;
                if (sta.StationID == toStationID)
                    return sta.StopType == StopType.Pass;
            }
            // 到達しない（行かない路線）→ 通過とみなさない
            return false;
        }



        /// <summary>
        /// 駅IDをキーに全列車の駅エントリを引くインデックスを構築する
        /// </summary>
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

        /// <summary>
        /// 同一到着から複数出発がある場合、先発列車が止まらない駅に停車する列車のみ残す
        /// </summary>
        private static List<GeneralConnectionPair> ApplyMultiDepartureFilter(
    List<GeneralConnectionPair> pairs,
    Dictionary<string, List<StationEntry>> idx)
        {
            var toRemove = new HashSet<GeneralConnectionPair>(ReferenceEqualityComparer.Instance);

            var groups = pairs
                .GroupBy(p => (p.ArrivalTrain, p.ArrivalStation.StationID))
                .Where(g => g.Count() > 1);

            foreach (var group in groups)
            {
                var sorted = group.OrderBy(p => p.DepartureStation.DepartureTime).ToList();

                for (int i = 1; i < sorted.Count; i++)
                {
                    var other = sorted[i];
                    var otherStops = GetStopsAfter(other.DepartureTrain, other.DepartureStation.StationID);

                    // 先発列車の有効停車駅（接続先を含む）を計算
                    var firstStops = GetEffectiveStopsAfterWithConnections(
                        sorted[0].DepartureTrain,
                        sorted[0].DepartureStation.StationID,
                        pairs,
                        otherStops);

                    if (otherStops.All(s => firstStops.Contains(s)))
                        toRemove.Add(other);
                }
            }

            return pairs.Where(p => !toRemove.Contains(p)).ToList();
        }

        /// <summary>
        /// 先発列車の有効停車駅セットを返す
        /// 先発列車が通過する駅Xについて、比較列車がXに来ない場合かつ
        /// 接続列車CがXをカバーするなら、Cの停車駅を先発列車の網羅範囲に追加する
        /// </summary>
        private static HashSet<string> GetEffectiveStopsAfterWithConnections(
            StaffTrain train,
            string fromStationID,
            List<GeneralConnectionPair> pairs,
            HashSet<string> otherStops)
        {
            var result = GetStopsAfter(train, fromStationID);

            // trainの接続先列車を取得
            var connectionEntries = pairs
                .Where(p => p.ArrivalTrain == train)
                .Select(p => (p.DepartureTrain, p.DepartureStation.StationID))
                .ToList();

            bool found = false;
            foreach (var sta in train.StaffStations)
            {
                if (sta.StationID == fromStationID) { found = true; continue; }
                if (!found) continue;
                if (sta.StopType != StopType.Pass) continue;
                if (sta.StationID.EndsWith("S")) continue;

                // 比較列車がこの通過駅に来る場合はスキップ
                if (otherStops.Contains(sta.StationID)) continue;

                // 接続列車がこの通過駅をカバーするか
                foreach (var (connTrain, connFromID) in connectionEntries)
                {
                    var connStops = GetStopsAfter(connTrain, connFromID);
                    if (!connStops.Contains(sta.StationID)) continue;
                    foreach (var s in connStops) result.Add(s);
                    break;
                }
            }

            return result;
        }


        /// <summary>
        /// 指定stationIDより後の停車駅IDセットを返す（通過駅は除く）
        /// </summary>
        private static HashSet<string> GetStopsAfter(StaffTrain train, string fromStationID)
        {
            var result = new HashSet<string>();
            bool found = false;
            foreach (var sta in train.StaffStations)
            {
                if (found && sta.StopType != StopType.Pass)
                    result.Add(sta.StationID);
                if (sta.StationID == fromStationID)
                    found = true;
            }
            return result;
        }

        /// <summary>
        /// 指定駅が列車の営業始発駅（StopType.Stopの初エントリ）かどうかを返す
        /// </summary>
        private static bool IsFirstStopStation(StaffTrain train, StaffStation station)
        {
            var first = train.StaffStations.FirstOrDefault(s => s.StopType == StopType.Stop);
            return first == station;
        }

        /// <summary>
        /// 相手列車の情報を「種 行 ①mm着/発/通」形式に変換する
        /// 時刻が取得できない場合はnullを返す
        /// </summary>
        private static string? FormatOtherTrainInfo(
            StaffTrain train,
            StaffStation station,
            TimeType timeType,
            StaffNoteAbbreviationTable abbr)
        {
            TimeSpan? time;
            string suffix;

            switch (timeType)
            {
                case TimeType.Arrival:
                    if (station.ArrivalTime is not TimeSpan arr) return null;
                    time = arr;
                    suffix = "着";
                    break;

                case TimeType.Departure:
                    if (station.DepartureTime is not TimeSpan dep) return null;
                    time = dep;
                    suffix = "発";
                    break;

                case TimeType.Pass:
                    time = station.ArrivalTime ?? station.DepartureTime;
                    if (time is null) return null;
                    suffix = "通";
                    break;

                default: return null;
            }

            var trainClass = abbr.Class(train.TrainType);
            var dest = abbr.Dest(train.TrainDestination);
            var bansen = ConvertToCircledNumber(station.TrackNumber ?? "");
            var mm = ((TimeSpan)time).Minutes.ToString("D2");

            return $"{trainClass} {dest} {bansen}{mm}{suffix}";
        }

        /// <summary>
        /// 備考を\n区切りで末尾に連結する
        /// </summary>
        private static void AppendNote(StaffStation sta, IEnumerable<string> notes)
        {
            foreach (var n in notes)
            {
                if (string.IsNullOrEmpty(n)) continue;
                sta.Note = string.IsNullOrEmpty(sta.Note) ? n : sta.Note + "\n" + n;
            }
        }

        /// <summary>
        /// 数字文字列を丸付き数字へ変換
        /// </summary>
        /// <param name="text">変換元文字列</param>
        /// <returns>丸付き数字文字列</returns>
        private static string ConvertToCircledNumber(string text)
        {
            if (!int.TryParse(text, out int number))
            {
                return text;
            }

            //
            // 0
            //
            if (number == 0)
            {
                return "　";
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