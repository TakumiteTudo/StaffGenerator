using StaffGenerator.Model;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Formats.Asn1;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StaffGenerator.Render
{
    /// <summary>
    /// スタフ描画クラス
    /// </summary>
    public sealed class StaffRenderer : IDisposable
    {
        #region 定数

        /// <summary>
        /// 左列開始X
        /// </summary>
        private const int LEFT_X = 39;

        /// <summary>
        /// 右列開始X
        /// </summary>
        private const int RIGHT_X = 348;

        /// <summary>
        /// 初期Y
        /// </summary>
        private const int START_Y = 259;

        /// <summary>
        /// ページ下限Y
        /// </summary>
        private const int Y_LIMIT = 891;

        /// <summary>
        /// ページ送り幅
        /// </summary>
        private const int PAGE_OFFSET_X = 309;

        /// <summary>
        /// 長行高さ
        /// </summary>
        private const int ROW_HEIGHT_LARGE = 63;

        /// <summary>
        /// 短行高さ
        /// </summary>
        private const int ROW_HEIGHT_SMALL = 36;

        /// <summary>
        /// 通過行高さ
        /// </summary>
        private const int ROW_HEIGHT_PASS = 27;

        /// <summary>
        /// 空行高さ
        /// </summary>
        private const int ROW_HEIGHT_EMPTY = 15;

        #endregion

        #region フィールド

        /// <summary>
        /// テンプレート画像
        /// </summary>
        private readonly Bitmap _templateBitmap;

        /// <summary>
        /// ヘッダフォント
        /// </summary>
        private readonly Font _fontHeader;

        /// <summary>
        /// 駅名フォント
        /// </summary>
        private readonly Font _fontStation;

        /// <summary>
        /// 通過駅フォント
        /// </summary>
        private readonly Font _fontPass;

        /// <summary>
        /// 時刻フォント
        /// </summary>
        private readonly Font _fontTime;

        /// <summary>
        /// 時刻フォント大
        /// </summary>
        private readonly Font _fontTimeBig;

        /// <summary>
        /// 備考フォント
        /// </summary>
        private readonly Font _fontNote;

        /// <summary>
        /// 罫線ペン
        /// </summary>
        private readonly Pen _linePen;

        /// <summary>
        /// 文字ブラシ
        /// </summary>
        private readonly Brush _textBrush;

        #endregion

        #region コンストラクタ

        /// <summary>
        /// スタフ描画クラスを初期化
        /// </summary>
        /// <param name="templatePath">テンプレート画像パス</param>
        public StaffRenderer(string templatePath)
        {
            _templateBitmap = new Bitmap(templatePath);

            _fontHeader = new Font("Yu Gothic", 12, FontStyle.Bold);
            _fontStation = new Font("Yu Gothic", 18, FontStyle.Bold);
            _fontPass = new Font("Yu Gothic", 14, FontStyle.Bold);
            _fontTime = new Font("Yu Gothic", 18, FontStyle.Bold);
            _fontTimeBig = new Font("Yu Gothic", 18, FontStyle.Bold);
            _fontNote = new Font("Yu Gothic", 8, FontStyle.Bold);

            _linePen = new Pen(Color.Black, 1);
            _textBrush = Brushes.Black;
        }

        #endregion

        #region Public

        /// <summary>
        /// スタフ画像を描画
        /// </summary>
        /// <param name="stations">駅リスト</param>
        /// <returns>描画済みBitmap</returns>
        public Bitmap Render(StaffTrain train)
        {
            // テンプレート複製
            Bitmap bmp = new Bitmap(_templateBitmap);

            using Graphics g = Graphics.FromImage(bmp);

            InitializeGraphics(g);

            List<StaffStationLayout> layouts =
                Measure(train.StaffStations);

            // 背景描画
            DrawBackground(g);

            DrawTrainHeader(g, train);

            for (int i = 0; i < train.StaffStations.Count; i++)
            {
                DrawStation(
                    g,
                    train.StaffStations[i],
                    layouts[i],
                    i,
                    train.StaffStations.Count);
            }

            // フッタ描画
            DrawFooter(g);

            return bmp;
        }

        #endregion

        #region Measure

        /// <summary>
        /// レイアウト計算
        /// </summary>
        /// <param name="stations">駅リスト</param>
        /// <returns>レイアウト一覧</returns>
        private List<StaffStationLayout> Measure(
            IReadOnlyList<StaffStation> stations)
        {
            List<StaffStationLayout> result = new();

            int currentY = START_Y;
            int currentXOffset = 0;
            int currentPage = 0;

            foreach (StaffStation station in stations)
            {
                int rowHeight = GetRowHeight(station);

                // 改ページ判定
                if (currentY + rowHeight > Y_LIMIT)
                {
                    currentXOffset += PAGE_OFFSET_X;
                    currentPage++;

                    currentY = START_Y;
                }

                StaffStationLayout layout = new()
                {
                    Top = currentY,
                    Bottom = currentY + rowHeight,
                    XOffset = currentXOffset,
                    PageIndex = currentPage
                };

                result.Add(layout);

                currentY += rowHeight;
            }

            return result;
        }

        #endregion

        #region Draw

        /// <summary>
        /// Graphics初期化
        /// </summary>
        /// <param name="g">Graphics</param>
        private void InitializeGraphics(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        }

        /// <summary>
        /// テキスト描画モード設定
        /// </summary>
        /// <param name="g">Graphics</param>
        private void SetTextRenderMode(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.HighQuality;

            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            g.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        }

        /// <summary>
        /// 罫線描画モード設定
        /// </summary>
        /// <param name="g">Graphics</param>
        private void SetLineRenderMode(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.None;

            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        }

        /// <summary>
        /// 背景描画
        /// </summary>
        /// <param name="g">Graphics</param>
        private void DrawBackground(Graphics g)
        {
            // TODO:
        }

        /// <summary>
        /// 列車ヘッダ描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="train">列車情報</param>
        private void DrawTrainHeader(
            Graphics g,
            StaffTrain train)
        {
            SetTextRenderMode(g);

            // 列車番号
            g.DrawString(
                train.TrainName,
                _fontHeader,
                _textBrush,
                166,
                132);

            // 種別
            g.DrawString(
                train.TrainType,
                _fontHeader,
                _textBrush,
                166,
                158);

            // 行先
            g.DrawString(
                train.TrainDestination,
                _fontHeader,
                _textBrush,
                166,
                184);

            // 備考
            g.DrawString(
                train.TrainNote,
                _fontHeader,
                _textBrush,
                166,
                210);

            DrawTrainTypeImage(g, train);
        }

        private void DrawTrainTypeImage(
            Graphics g,
            StaffTrain train)
        {
            string path =
                Path.Combine(
                    "Image",
                    $"スタフ種別_{train.TrainTypeImgName}.png");

            if (!File.Exists(path))
        {
                return;
        }

            using Bitmap bmp = new Bitmap(path);

            g.DrawImage(bmp, new Rectangle(332, 63, 321, 187));
        }

        /// <summary>
        /// 駅描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="station">駅情報</param>
        /// <param name="layout">レイアウト</param>
        /// <param name="index">駅Index</param>
        /// <param name="count">総数</param>
        private void DrawStation(
            Graphics g,
            StaffStation station,
            StaffStationLayout layout,
            int index,
            int count)
        {
            DrawStationName(g, station, layout);

            DrawArrivalTime(g, station, layout);

            DrawDepartureTime(g, station, layout);

            DrawTrack(g, station, layout);

            DrawNote(g, station, layout);

            DrawBorder(g, layout);
        }

        /// <summary>
        /// 駅名描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="station">駅情報</param>
        /// <param name="layout">レイアウト</param>
        private void DrawStationName(
            Graphics g,
            StaffStation station,
            StaffStationLayout layout)
        {
            // TODO:
        }

        /// <summary>
        /// 到着時刻描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="station">駅情報</param>
        /// <param name="layout">レイアウト</param>
        private void DrawArrivalTime(
            Graphics g,
            StaffStation station,
            StaffStationLayout layout)
        {
            // TODO:
        }

        /// <summary>
        /// 出発時刻描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="station">駅情報</param>
        /// <param name="layout">レイアウト</param>
        private void DrawDepartureTime(
            Graphics g,
            StaffStation station,
            StaffStationLayout layout)
        {
            // TODO:
        }

        /// <summary>
        /// 番線描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="station">駅情報</param>
        /// <param name="layout">レイアウト</param>
        private void DrawTrack(
            Graphics g,
            StaffStation station,
            StaffStationLayout layout)
        {
            // TODO:
        }

        /// <summary>
        /// 備考描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="station">駅情報</param>
        /// <param name="layout">レイアウト</param>
        private void DrawNote(
            Graphics g,
            StaffStation station,
            StaffStationLayout layout)
        {
            // TODO:
        }

        /// <summary>
        /// 罫線描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="layout">レイアウト</param>
        private void DrawBorder(
            Graphics g,
            StaffStationLayout layout)
        {
            int y = layout.Bottom;

            g.DrawLine(
                _linePen,
                LEFT_X + layout.XOffset,
                y,
                RIGHT_X + layout.XOffset,
                y);
        }

        /// <summary>
        /// フッタ描画
        /// </summary>
        /// <param name="g">Graphics</param>
        private void DrawFooter(Graphics g)
        {
            // TODO:
        }

        #endregion

        #region Utility

        /// <summary>
        /// 行高さ取得
        /// </summary>
        /// <param name="station">駅情報</param>
        /// <returns>行高さ</returns>
        private int GetRowHeight(StaffStation station)
        {
            // Todo: 備考による上限超え対応

            if (station.StopType == StopType.Pass)
            {
                return ROW_HEIGHT_PASS;
            }

            if (station.ArrivalTime == null || station.DepartureTime == null)
            {
                //始点・終点等時刻1つ
                if (station.IsShunting)
                {
                    //入換ありは2行
                    return ROW_HEIGHT_LARGE;
                }

                //基本1行
                return ROW_HEIGHT_SMALL;
            }
            if (station.IsTimingPoint = false)
            {
                //非採時は1行
                return ROW_HEIGHT_SMALL;
            }

            return ROW_HEIGHT_LARGE;
        }

        #endregion

        #region Dispose

        /// <summary>
        /// リソース解放
        /// </summary>
        public void Dispose()
        {
            _templateBitmap.Dispose();

            _fontStation.Dispose();
            _fontPass.Dispose();
            _fontTime.Dispose();
            _fontNote.Dispose();

            _linePen.Dispose();
        }

        #endregion
    }
}