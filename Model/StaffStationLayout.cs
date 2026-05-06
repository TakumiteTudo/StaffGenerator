namespace StaffGenerator.Model
{
    /// <summary>
    /// 駅描画レイアウト情報
    /// </summary>
    public class StaffStationLayout
    {
        /// <summary>
        /// 描画開始Y
        /// </summary>
        public int Top { get; set; }

        /// <summary>
        /// 描画終了Y
        /// </summary>
        public int Bottom { get; set; }

        /// <summary>
        /// ページ番号
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// 列番号
        /// </summary>
        public int ColumnIndex { get; set; }

        /// <summary>
        /// Xオフセット
        /// </summary>
        public int XOffset { get; set; }

        /// <summary>
        /// 到着時刻の表示有無
        /// </summary>
        public bool IsDrawArrival { get; set; }

        /// <summary>
        /// 到着時刻での時間表示有無
        /// </summary>
        public bool IsDrawArrivalHours { get; set; }

        /// <summary>
        /// 到着時刻の表示有無
        /// </summary>
        public bool IsDrawDeparture { get; set; }

        /// <summary>
        /// 出発時刻での時間表示有無
        /// </summary>
        public bool IsDrawDepartureHours { get; set; }
    }
}
