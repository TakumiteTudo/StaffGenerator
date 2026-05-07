using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StaffGenerator.Model
{
    /// <summary>
    /// スタフ描画用駅情報
    /// </summary>
    public class StaffStation
    {
        /// <summary>
        /// 駅ID（スタフ備考生成に使用）
        /// </summary>
        public string StationID { get; set; } = "";

        /// <summary>
        /// 表示駅名
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 到着時刻
        /// </summary>
        public TimeSpan? ArrivalTime { get; set; }

        /// <summary>
        /// 出発時刻
        /// </summary>
        public TimeSpan? DepartureTime { get; set; }

        /// <summary>
        /// 採時対象
        /// </summary>
        public bool IsTimingPoint { get; set; }

        /// <summary>
        /// 停車種別
        /// </summary>
        public StopType StopType { get; set; }

        /// <summary>
        /// 番線表示
        /// </summary>
        public string TrackNumber { get; set; }

        /// <summary>
        /// 開扉表示
        /// </summary>
        public DoorDirection DoorDirection { get; set; }

        /// <summary>
        /// 備考
        /// </summary>
        public string Note { get; set; }

        /// <summary>
        /// 入換表示
        /// </summary>
        public bool IsArrShunting { get; set; }
        /// <summary>
        /// 入換表示
        /// </summary>
        public bool IsDepShunting { get; set; }

        /// <summary>
        /// 制御スクリプト
        /// </summary>
        public string Script { get; set; }
    }
}
