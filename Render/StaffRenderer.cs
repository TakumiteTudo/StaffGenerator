using StaffGenerator.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Formats.Asn1;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        private const int ROW_HEIGHT_LARGE = 56;

        /// <summary>
        /// 短行高さ
        /// </summary>
        private const int ROW_HEIGHT_SMALL = 30;

        /// <summary>
        /// 通過行高さ
        /// </summary>
        private const int ROW_HEIGHT_PASS = 26;

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


        private readonly Dictionary<string, Color> _trainTypeColors;

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
            _fontTime = new Font("Yu Gothic", 14, FontStyle.Bold);
            _fontTimeBig = new Font("Yu Gothic", 18, FontStyle.Bold);
            _fontNote = new Font("Yu Gothic", 8, FontStyle.Bold);

            _linePen = new Pen(Color.Black, 1);
            _textBrush = Brushes.Black;

            _trainTypeColors = new()
            {
                { "普通", Color.White },
                { "準急", Color.FromArgb(113, 185, 255) },
                { "区急", Color.FromArgb(100, 200, 50) },
                { "急行", Color.FromArgb(255, 171, 34) },
                { "快急", Color.FromArgb(222, 116, 255) },
                { "特急", Color.FromArgb(255, 100, 100) },
            };
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
            Bitmap bmp = new Bitmap(_templateBitmap);

            using Graphics g = Graphics.FromImage(bmp);

            InitializeGraphics(g);

            List<StaffStationLayout> layouts =
                Measure(train.StaffStations);

            DrawBackground(g);

            DrawTrainHeader(g, train);

            for (int i = 0; i < train.StaffStations.Count; i++)
            {
                DrawStation(
                    g,
                    train.TrainTypeImgName,
                    train.StaffStations[i],
                    layouts[i],
                    i,
                    train.StaffStations.Count);
            }


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

            int? lastHour = null;

            for (int i = 0; i < stations.Count; i++)
            {
                StaffStation station = stations[i];

                int rowHeight = GetRowHeight(station);

                // 改ページ判定
                if (currentY + rowHeight > Y_LIMIT)
                {
                    currentXOffset += PAGE_OFFSET_X;
                    currentPage++;

                    currentY = START_Y;

                    // ページ跨ぎ時は時刻再表示
                    lastHour = null;
                }

                StaffStationLayout layout = new()
                {
                    Top = currentY,
                    Bottom = currentY + rowHeight,
                    XOffset = currentXOffset,
                    PageIndex = currentPage
                };

                //
                // 到着時刻
                //
                if (station.ArrivalTime.HasValue && station.IsTimingPoint)
                {
                    int hour = station.ArrivalTime.Value.Hours;

                    if (lastHour != hour)
                    {
                        layout.IsDrawArrivalHours = true;

                        lastHour = hour;
                    }
                }

                //
                // 出発時刻
                //
                if (station.DepartureTime.HasValue)
                {
                    int hour = station.DepartureTime.Value.Hours;

                    if (lastHour != hour)
                    {
                        layout.IsDrawDepartureHours = true;

                        lastHour = hour;
                    }
                }

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
            string trainClass,
            StaffStation station,
            StaffStationLayout layout,
            int index,
            int count)
        {
            if (!(station.StopType == StopType.Pass && !station.IsTimingPoint))
            {
                DrawStationName(g, trainClass, station, layout);

                DrawTime(g, station, layout);

                DrawTrack(g, station, layout);

                DrawStationNote(g, station, layout);
            }

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
            string trainClass,
            StaffStation station,
            StaffStationLayout layout)
        {
            SetTextRenderMode(g);

            if (station.StopType != StopType.Pass)
            {
                Brush brush = new SolidBrush(GetTrainTypeColor(trainClass));
                g.FillRectangle(brush, new RectangleF(LEFT_X + layout.XOffset + 1, layout.Top, 112, layout.Bottom - layout.Top));
            }

            StringFormat sf = new StringFormat();

            //
            // 横：左揃え
            //
            sf.Alignment = StringAlignment.Near;

            //
            // 縦：中央揃え
            //
            sf.LineAlignment = StringAlignment.Center;

            //
            // はみ出し防止
            //
            sf.FormatFlags = StringFormatFlags.NoWrap;

            RectangleF rect = new RectangleF(
                LEFT_X + layout.XOffset + 2,
                layout.Top - 5,
                110,
                layout.Bottom - layout.Top + 22);

            if (station.StopType == StopType.Pass)
            {
                g.DrawString(
                    " (" + station.DisplayName + ")",
                    _fontPass,
                    _textBrush,
                    rect,
                    sf);
            }
            else
            {
                g.DrawString(
                    station.DisplayName,
                    _fontStation,
                    _textBrush,
                    rect,
                    sf);
            }
        }

        /// <summary>
        /// 時刻描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="station">駅情報</param>
        /// <param name="layout">レイアウト</param>
        private void DrawTime(
            Graphics g,
            StaffStation station,
            StaffStationLayout layout)
        {
            SetTextRenderMode(g);

            // 入換描画
            if (station.IsShunting)
            {
                int offsetY = 0;
                if (station.ArrivalTime.HasValue)
                {
                    //発側入換 
                    offsetY += 27;
                }

                DrawTimeText(
                    g,
                    layout,
                    "",
                    "入",
                    "換",
                    false,
                    false,
                    offsetY);
            }

            //
            // 到着時刻描画
            //
            if (station.StopType != StopType.Pass && station.ArrivalTime.HasValue && station.IsTimingPoint)
            {
                bool isLarge =
                    !station.DepartureTime.HasValue;

                int offsetY = isLarge ? 0 : -4;

                DrawTimeText(
                    g,
                    layout,
                    station.ArrivalTime.Value.Hours.ToString("00"),
                    station.ArrivalTime.Value.Minutes.ToString("00"),
                    station.ArrivalTime.Value.Seconds.ToString("00"),
                    layout.IsDrawArrivalHours,
                    isLarge,
                    offsetY);
            }

            //
            // 出発時刻描画
            //
            if (station.DepartureTime.HasValue)
            {
                bool isLarge =
                    station.StopType != StopType.Pass;

                int offsetY;

                // 下側へ描画   
                offsetY = isLarge ? layout.Bottom - layout.Top - 30 : layout.Bottom - layout.Top - 30;

                DrawTimeText(
                    g,
                    layout,
                    station.DepartureTime.Value.Hours.ToString("00"),
                    station.DepartureTime.Value.Minutes.ToString("00"),
                    station.DepartureTime.Value.Seconds.ToString("00"),
                    layout.IsDrawDepartureHours,
                    isLarge,
                    offsetY);
            }
        }

        /// <summary>
        /// 時刻文字描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="layout">レイアウト</param>
        /// <param name="hourText">時文字列</param>
        /// <param name="minuteText">分文字列</param>
        /// <param name="secondText">秒文字列</param>
        /// <param name="isDrawHour">時描画有無</param>
        /// <param name="isLarge">大文字描画か</param>
        /// <param name="offsetY">Yオフセット</param>
        private void DrawTimeText(
            Graphics g,
            StaffStationLayout layout,
            string hourText,
            string minuteText,
            string secondText,
            bool isDrawHour,
            bool isLarge,
            int offsetY)
        {
            int drawY =
                layout.Top + offsetY;

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Far;

            //
            // 時
            //
            if (isDrawHour)
            {
                g.DrawString(hourText, _fontTime, _textBrush, new RectangleF(LEFT_X + layout.XOffset + 114, drawY + 6, 30, 39), sf);
            }

            //
            // 分
            //
            if (isLarge)
            {
                g.DrawString(minuteText, _fontTimeBig, _textBrush, new RectangleF(LEFT_X + layout.XOffset + 114 + 29, drawY + 0, 41, 39), sf);
            }
            else
            {
                g.DrawString(minuteText, _fontTime, _textBrush, new RectangleF(LEFT_X + layout.XOffset + 114 + 29, drawY + 6, 41, 39), sf);
            }

            //
            // 秒
            //              
            g.DrawString(secondText, _fontTime, _textBrush, new RectangleF(LEFT_X + layout.XOffset + 114 + 61, drawY + 6, 41, 39), sf);
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

            //番線番号
            var trackText = ConvertToCircledNumber(station.TrackNumber);
            //開扉方向   
            if (trackText != "")
            {
                if (station.DoorDirection == DoorDirection.Left && station.StopType == StopType.Stop)
                {
                    trackText = "◀" + trackText;
                }
                else if (station.DoorDirection == DoorDirection.Right && station.StopType == StopType.Stop)
                {
                    trackText = "　" + trackText + "▶";
                }
                else
                {
                    trackText = "　" + trackText;
                }
            }

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Near;

            g.DrawString(trackText, _fontTime, _textBrush, new RectangleF(LEFT_X + layout.XOffset + 218, layout.Top + 2, 92, 29), sf);
        }

        /// <summary>
        /// 備考描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="station">駅情報</param>
        /// <param name="layout">レイアウト</param>
        private void DrawStationNote(
            Graphics g,
            StaffStation station,
            StaffStationLayout layout)
        {
            // TODO:   

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Near;

            var noteList = station.Note.Split("\n").Reverse();

            int yoff = 15;

            foreach (var n in noteList)
            {
                g.DrawString(n, _fontNote, _textBrush, new RectangleF(LEFT_X + layout.XOffset + 218, layout.Bottom - yoff, 92, 20), sf);
                yoff += 14;
            }

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
            //
            // 罫線描画
            //
            SetLineRenderMode(g);

            g.DrawLine(
                _linePen,
                LEFT_X + layout.XOffset + 114,
                layout.Top,
                LEFT_X + layout.XOffset + 114,
                layout.Bottom);

            g.DrawLine(
                _linePen,
                LEFT_X + layout.XOffset + 145,
                layout.Top,
                LEFT_X + layout.XOffset + 145,
                layout.Bottom);

            g.DrawLine(
                _linePen,
                LEFT_X + layout.XOffset + 186,
                layout.Top,
                LEFT_X + layout.XOffset + 186,
                layout.Bottom);

            g.DrawLine(
                _linePen,
                LEFT_X + layout.XOffset + 217,
                layout.Top,
                LEFT_X + layout.XOffset + 217,
                layout.Bottom);

            int y = layout.Bottom;

            g.DrawLine(
                _linePen,
                LEFT_X + layout.XOffset,
                layout.Bottom,
                RIGHT_X + layout.XOffset,
                layout.Bottom);
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
                if (station.IsTimingPoint == false)
                {
                    //非採時通過は空行
                    return ROW_HEIGHT_EMPTY;
                }
                return ROW_HEIGHT_PASS;
            }

            if (station.IsTimingPoint == false)
            {
                //非採時は1行
                return ROW_HEIGHT_SMALL;
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

            return ROW_HEIGHT_LARGE;
        }


        /// <summary>
        /// 種別色取得
        /// </summary>
        /// <param name="trainType">種別名</param>
        /// <returns>種別色</returns>
        private Color GetTrainTypeColor(string trainType)
        {
            if (_trainTypeColors.TryGetValue(
                trainType,
                out Color color))
            {
                return color;
            }

            //
            // 未定義種別時は白
            //
            return Color.White;
        }

        /// <summary>
        /// 数字文字列を丸付き数字へ変換
        /// </summary>
        /// <param name="text">変換元文字列</param>
        /// <returns>丸付き数字文字列</returns>
        private string ConvertToCircledNumber(string text)
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