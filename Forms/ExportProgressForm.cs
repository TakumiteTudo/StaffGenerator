using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace StaffGenerator.Forms
{
    public partial class ExportProgressForm : Form
    {
        /// <summary>
        /// 進捗ウィンドウを初期化
        /// </summary>
        /// <param name="total">総件数</param>
        /// <param name="title">ウィンドウタイトル</param>
        public ExportProgressForm(int total, string title)
        {
            InitializeComponent();
            Text = title;
            progressBar.Maximum = total;
            progressBar.Value = 0;
            labelProgress.Text = $"0 / {total} 件";
        }

        /// <summary>
        /// 進捗を1件分進める
        /// </summary>
        /// <param name="trainName">処理中の列番</param>
        public void Step(string trainName)
        {
            int next = Math.Min(progressBar.Value + 1, progressBar.Maximum);

            // アニメーションラグ回避：一瞬+1してから戻すことで即時描画
            if (next < progressBar.Maximum)
            {
                progressBar.Value = next + 1;
                progressBar.Value = next;
            }
            else
            {
                // 最大値では+1できないのでMarqueeスタイルと同様に直接セット
                progressBar.Value = next;
                progressBar.Value = next - 1;
                progressBar.Value = next;
            }

            labelProgress.Text = $"{next} / {progressBar.Maximum} 件";
            labelCurrent.Text = $"出力中：{trainName}";
            Application.DoEvents();
        }
    }
}
