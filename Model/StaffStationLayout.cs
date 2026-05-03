using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
