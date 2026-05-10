using StaffGenerator.Model;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;

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
        private const int ROW_HEIGHT_EMPTY = 7;

        /// <summary>
        /// 種別変更スクリプトのプレフィックス
        /// </summary>
        private const string ScriptPrefixChangeClass = "ChangeClass:";

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
        /// 備考フォント
        /// </summary>
        private readonly Font _fontFooter;

        /// <summary>
        /// 罫線ペン
        /// </summary>
        private readonly Pen _linePen;

        /// <summary>
        /// 文字ブラシ
        /// </summary>
        private readonly Brush _textBrush;


        private readonly Dictionary<string, Color> _trainTypeColors;

        private readonly Dictionary<string, Color> _trainTypeOpColors;

        /// <summary>
        /// スタフ分割候補駅名（優先順）
        /// </summary>
        private static readonly string[] SplitCandidateNames = ["大道寺", "赤山町"];

        /// <summary>
        /// script変更指示のパース結果
        /// </summary>
        private record ScriptChangeResult(
            /// <summary>新種別名（ChangeClass未指定時はnull）</summary>
            string? NewTrainType,
            /// <summary>新列番（ChangeName未指定時はnull）</summary>
            string? NewTrainName);

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
            _fontFooter = new Font("Yu Gothic", 14, FontStyle.Bold);

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
                { "回送", Color.FromArgb(220, 220, 220) },
                { "試運転", Color.FromArgb(220, 220, 220) },
            };

            _trainTypeOpColors = new()
            {
                { "普通", Color.FromArgb(190, 190, 190) },
                { "準急", Color.FromArgb(190, 190, 190) },
                { "区急", Color.FromArgb(190, 190, 190) },
                { "急行", Color.FromArgb(190, 190, 190) },
                { "快急", Color.FromArgb(190, 190, 190) },
                { "特急", Color.FromArgb(190, 190, 190) },
                { "回送", Color.FromArgb(190, 190, 190) },
                { "試運転", Color.FromArgb(190, 190, 190) },
            };
        }

        #endregion

        #region Public

        /// <summary>
        /// スタフ画像を描画（種別変更・溢れ分割対応）
        /// </summary>
        /// <param name="train">列車情報</param>
        /// <param name="allTrains">全列車リスト（前列車検索用）</param>
        /// <returns>描画済みBitmapリスト</returns>
        public List<Bitmap> Render(StaffTrain train, IReadOnlyList<StaffTrain> allTrains)
        {
            // 大路駅フィルタ適用
            var filteredTrain = FilterOmizuStation(train, allTrains);

            // 種別変更で分割 → 各セグメントを溢れ分割 → 全rawセグメントを確定
            var rawSegments = new List<(StaffTrain Header, List<StaffStation> Stations)>();
            foreach (var (header, stations) in SplitByClassChange(filteredTrain))
            {
                foreach (var ov in SplitSegments(stations, SplitCandidateNames))
                    rawSegments.Add((header, ov));
            }

            int total = rawSegments.Count;
            var result = new List<Bitmap>();

            for (int i = 0; i < total; i++)
            {
                var (header, rawStations) = rawSegments[i];
                var stations = new List<StaffStation>(rawStations);

                // 先頭に「続き」マーカー挿入（2枚目以降）
                if (i > 0)
                    stations.Insert(0, CreateContinuationMarker($"▼（{i}枚目から続き）▼"));

                // 末尾に「続く」マーカー挿入（最終枚以外）
                if (i < total - 1)
                    stations.Add(CreateContinuationMarker($"▼（{i + 2}枚目へ続く）▼"));

                result.Add(RenderSingle(header, stations, allTrains, i + 1, total));
            }

            return result;
        }

        /// <summary>
        /// 単一セグメントを描画
        /// </summary>
        /// <param name="train">列車情報（ヘッダ用）</param>
        /// <param name="stations">描画対象駅リスト</param>
        /// <param name="pageNum">このスタフの枚数（1始まり）</param>
        /// <param name="totalPages">総枚数</param>
        /// <returns>描画済みBitmap</returns>
        private Bitmap RenderSingle(
            StaffTrain train,
            List<StaffStation> stations,
            IReadOnlyList<StaffTrain> allTrains,
            int pageNum,
            int totalPages)
        {
            Bitmap bmp = new Bitmap(_templateBitmap);
            using Graphics g = Graphics.FromImage(bmp);

            InitializeGraphics(g);
            DrawBackground(g);
            DrawTrainHeader(g, train, pageNum, totalPages);

            var layouts = Measure(stations);

            for (int i = 0; i < stations.Count; i++)
            {
                DrawStation(g, train.TrainTypeImgName, stations[i], layouts[i], i, stations.Count);
            }

            if (pageNum == totalPages && layouts.Count > 0)
                DrawFooter(g, train, layouts[^1], allTrains);

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
                if ((i == 0 || i == stations.Count - 1) && !station.IsDepShunting)
                {
                    rowHeight = ROW_HEIGHT_SMALL;
                }

                rowHeight = Math.Max(rowHeight, GetMinHeight(station));

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
                if (station.ArrivalTime.HasValue && station.IsTimingPoint && station.StopType != StopType.Pass)
                {
                    if ((i == 0) && !station.IsDepShunting)
                    {
                        layout.IsDrawArrival = false;
                    }
                    else
                    {
                        layout.IsDrawArrival = true;
                        int hour = station.ArrivalTime.Value.Hours;

                        if (lastHour != hour)
                        {
                            layout.IsDrawArrivalHours = true;

                            lastHour = hour;
                        }
                    }
                }

                //
                // 出発時刻
                //
                if (station.DepartureTime.HasValue && (station.IsTimingPoint || station.StopType != StopType.Pass))
                {
                    if ((i == stations.Count - 1) && !station.IsDepShunting)
                    {
                        layout.IsDrawDeparture = false;
                    }
                    else
                    {
                        layout.IsDrawDeparture = true;
                        int hour = station.DepartureTime.Value.Hours;

                        if (lastHour != hour)
                        {
                            layout.IsDrawDepartureHours = true;

                            lastHour = hour;
                        }
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
            StaffTrain train,
            int pageNum,
            int totalPages)
        {
            SetTextRenderMode(g);

            StringFormat sfl = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
            };

            StringFormat sfr = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Near,
            };
            // 列車区
            g.DrawString(
                "大道寺列車区",
                _fontHeader,
                _textBrush,
                new RectangleF(
                    45,
                    80,
                    110,
                    30),
                sfl);

            // 列車番号
            g.DrawString(
                "列車番号",
                _fontHeader,
                _textBrush,
                new RectangleF(
                    40,
                    130,
                    110,
                    30),
                sfr);

            g.DrawString(
                TrainNumberFormat(train.TrainName),
                _fontHeader,
                _textBrush,
                new RectangleF(
                    166,
                    130,
                    150,
                    30),
                sfl);

            // 種別       
            g.DrawString(
                "種別",
                _fontHeader,
                _textBrush,
                new RectangleF(
                    40,
                    155,
                    110,
                    30),
                sfr);

            g.DrawString(
                train.TrainType,
                _fontHeader,
                _textBrush,
                new RectangleF(
                    166,
                    155,
                    150,
                    30),
                sfl);

            // 行先      
            g.DrawString(
                "行先",
                _fontHeader,
                _textBrush,
                new RectangleF(
                    40,
                    180,
                    110,
                    30),
                sfr);

            g.DrawString(
                train.TrainDestination,
                _fontHeader,
                _textBrush,
                new RectangleF(
                    166,
                    180,
                    150,
                    30),
                sfl);

            // 備考    
            g.DrawString(
                "備考",
                _fontHeader,
                _textBrush,
                new RectangleF(
                    40,
                    205,
                    110,
                    30),
                sfr);

            g.DrawString(
                train.TrainNote,
                _fontHeader,
                _textBrush,
                new RectangleF(
                    166,
                    205,
                    150,
                    90),
                sfl);

            // 複数枚のとき枚数を表示
            if (totalPages > 1)
            {
                g.DrawString(
                    $"（{pageNum}/{totalPages}枚目）",
                    _fontNote,
                    _textBrush,
                    255,
                    85);
            }

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
            // 継続マーカーはテキストのみ描画
            if (station.StopType == StopType.SplitContinuation)
            {
                DrawContinuationMarker(g, station, layout);
                return;
            }

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
        /// 継続マーカー行を描画
        /// </summary>
        private void DrawContinuationMarker(
            Graphics g,
            StaffStation station,
            StaffStationLayout layout)
        {
            SetTextRenderMode(g);
            StringFormat sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(
                station.DisplayName,
                _fontTime,
                _textBrush,
                new RectangleF(
                    LEFT_X + layout.XOffset,
                    layout.Top + 5,
                    RIGHT_X - LEFT_X,
                    layout.Bottom - layout.Top),
                sf);
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
                Brush brush = new SolidBrush(GetTrainTypeColor(trainClass, station.StopType == StopType.OpStop));
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


            //
            // 到着時刻描画
            //
            if (layout.IsDrawArrival)
            {
                bool isLarge =
                    !station.DepartureTime.HasValue;

                int offsetY = isLarge ? 0 : -4;

                // 入換描画
                if (station.IsArrShunting)
                {

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

                else
                {
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
            }

            //
            // 出発時刻描画
            //
            if (layout.IsDrawDeparture)
            {
                bool isLarge =
                    station.StopType != StopType.Pass;

                int offsetY;

                // 下側へ描画   
                offsetY = isLarge ? layout.Bottom - layout.Top - 30 : layout.Bottom - layout.Top - 30;


                // 入換描画
                if (station.IsDepShunting)
                {

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
                else
                {
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
                LEFT_X + layout.XOffset,
                layout.Top,
                RIGHT_X + layout.XOffset,
                layout.Top);

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
        /// フッタ（折返・備考）描画
        /// </summary>
        /// <param name="g">Graphics</param>
        /// <param name="train">列車情報</param>
        /// <param name="lastLayout">最終駅レイアウト</param>
        /// <param name="allTrains">全列車リスト</param>
        private void DrawFooter(
            Graphics g,
            StaffTrain train,
            StaffStationLayout lastLayout,
            IReadOnlyList<StaffTrain> allTrains)
        {
            var lines = BuildFooterLines(train, allTrains);
            if (lines.Count == 0) return;

            SetTextRenderMode(g);

            StringFormat sf = new()
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
            };

            int y = lastLayout.Bottom + 5;
            int x = LEFT_X + lastLayout.XOffset + 3;

            foreach (var line in lines)
            {
                g.DrawString(
                    line,
                    _fontFooter,
                    _textBrush,
                    new RectangleF(x, y, RIGHT_X - LEFT_X, 23),
                    sf);

                y += 23;
            }
        }

        /// <summary>
        /// フッタに表示する備考行リストを生成
        /// </summary>
        /// <param name="train">列車情報</param>
        /// <param name="allTrains">全列車リスト（折返列車検索用）</param>
        /// <returns>表示行リスト</returns>
        private static List<string> BuildFooterLines(StaffTrain train, IReadOnlyList<StaffTrain> allTrains)
        {
            var lines = new List<string>();

            var lastSta = train.StaffStations[^1];

            //交代の場合
            if (lastSta.IsDriverChange)
            {
                var depTimeText = "--:--";
                if (lastSta.DepartureTime is TimeSpan dt)
                    depTimeText = dt.ToString(@"hh\:mm");
                var depTailText = (bool)(lastSta.IsArrShunting) ? "入換" : "発";

                lines.Add($"引継：{TrainNumberFormat(train.TrainName)}");
                lines.Add($"　　　{train.TrainType}　{train.TrainDestination}　行き");
            }

            // 折返列車を列番で検索
            else if (!string.IsNullOrEmpty(train.NextTrainNumber))
            {
                var next = allTrains.FirstOrDefault(t => t.TrainName == train.NextTrainNumber);
                if (next != null)
                {
                    var first = next.StaffStations.FirstOrDefault();
                    var depTimeText = "--:--";
                    if (first?.DepartureTime is TimeSpan dt)
                        depTimeText = dt.ToString(@"hh\:mm");
                    var depTailText = (bool)(first?.IsArrShunting) ? "入換" : "発";

                    lines.Add($"折返：{TrainNumberFormat(next.TrainName)}（{depTimeText}{depTailText}）");
                    lines.Add($"　　　{next.TrainType}　{next.TrainDestination}　行き");
                }
                else
                    lines.Add($"折返：{TrainNumberFormat(train.NextTrainNumber)}");
            }

            //江原　検の場合
            else if (lastSta.DisplayName == "江原　検")
            {
                lines.Add($"引継：構内運転士へ");
            }

            else
            {
                //Todo：翌日運用の記載
                lines.Add($"滞泊");
            }


            return lines;
        }

        #endregion

        #region Utility
        /// <summary>
        /// 大路駅フィルタ処理
        /// 上り：行先が"大路"でないとき大路駅を削除
        /// 下り：前列車の行先が"大路"でないとき大路駅を削除   
        /// 削除時は"新大路"駅のIsDriverChangeをtrueにする
        /// </summary>
        /// <param name="train">列車情報</param>
        /// <param name="allTrains">全列車リスト</param>
        /// <returns>フィルタ済み列車情報</returns>
        private static StaffTrain FilterOmizuStation(
            StaffTrain train,
            IReadOnlyList<StaffTrain> allTrains)
        {
            bool shouldRemove;

            if (train.IsDownward)
            {
                // 下り：前列車を列番で検索し、行先が"大路"でないとき削除
                var prev = allTrains.FirstOrDefault(t =>
                    t.TrainName == train.PreviousTrainNumber);
                shouldRemove = prev == null || (prev != null && prev.TrainDestination != "大路");
            }
            else
            {
                // 上り：自列車の行先が"大路"でないとき削除
                shouldRemove = train.TrainDestination != "大路";
            }

            if (!shouldRemove) return train;

            var filteredStations = train.StaffStations
                .Where(s => s.DisplayName != "大路")
                .ToList();

            // 大路削除時は新大路のIsDriverChangeをtrueにする
            var shinOmizu = filteredStations.FirstOrDefault(s => s.DisplayName == "新大路");
            if (shinOmizu != null)
                shinOmizu.IsDriverChange = true;

            return CloneTrainWithStations(train, filteredStations);
        }

        /// <summary>
        /// 列車情報を駅リストのみ変更してクローン
        /// </summary>
        /// <param name="src">元列車情報</param>
        /// <param name="stations">新駅リスト</param>
        /// <returns>クローン列車情報</returns>
        private static StaffTrain CloneTrainWithStations(
            StaffTrain src,
            List<StaffStation> stations) => new()
            {
                OperationNumber = src.OperationNumber,
                TrainName = src.TrainName,
                TrainType = src.TrainType,
                TrainTypeImgName = src.TrainTypeImgName,
                TrainDestination = src.TrainDestination,
                TrainNote = src.TrainNote,
                IsDownward = src.IsDownward,
                StaffStations = stations,
                PreviousTrainNumber = src.PreviousTrainNumber,
                NextTrainNumber = src.NextTrainNumber,
            };

        /// <summary>
        /// 種別変更scriptに基づいてセグメント分割
        /// </summary>
        /// <param name="train">列車情報</param>
        /// <returns>（ヘッダー, 駅リスト）のセグメントリスト</returns>
        private static List<(StaffTrain Header, List<StaffStation> Stations)> SplitByClassChange(
            StaffTrain train)
        {
            var result = new List<(StaffTrain, List<StaffStation>)>();
            var currentHeader = train;
            var currentStations = new List<StaffStation>();

            foreach (var sta in train.StaffStations)
            {
                var change = ParseScriptChanges(sta);

                if (change != null && currentStations.Count > 0)
                {
                    currentStations.Add(sta);
                    result.Add((currentHeader, currentStations));

                    currentHeader = CloneTrainWithChanges(train, change);
                    currentStations = [sta];
                    continue;
                }

                currentStations.Add(sta);
            }

            if (currentStations.Count > 0)
                result.Add((currentHeader, currentStations));

            return result;
        }

        /// <summary>
        /// 駅のscriptからChangeClass・ChangeNameを取得
        /// </summary>
        /// <param name="station">対象駅</param>
        /// <returns>変更指示（どちらも未指定時はnull）</returns>
        private static ScriptChangeResult? ParseScriptChanges(StaffStation station)
        {
            if (string.IsNullOrEmpty(station.Script)) return null;

            string? newTrainType = null;
            string? newTrainName = null;

            foreach (var line in station.Script.Split('\n'))
            {
                if (line.StartsWith("ChangeClass:"))
                {
                    var parts = line.Split(':');
                    if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
                        newTrainType = parts[1];
                }
                else if (line.StartsWith("ChangeName:"))
                {
                    var parts = line.Split(':');
                    if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
                        newTrainName = parts[1];
                }
            }

            // どちらも未指定なら変更なし
            if (newTrainType == null && newTrainName == null) return null;

            return new ScriptChangeResult(newTrainType, newTrainName);
        }

        /// <summary>
        /// 列車情報をscript変更指示に基づいてクローン
        /// </summary>
        /// <param name="src">元列車情報</param>
        /// <param name="change">変更指示</param>
        /// <returns>クローン列車情報</returns>
        private static StaffTrain CloneTrainWithChanges(
            StaffTrain src,
            ScriptChangeResult change) => new()
            {
                OperationNumber = src.OperationNumber,
                TrainName = change.NewTrainName ?? src.TrainName,
                TrainType = change.NewTrainType ?? src.TrainType,
                TrainTypeImgName = change.NewTrainType ?? src.TrainTypeImgName,
                TrainDestination = src.TrainDestination,
                TrainNote = src.TrainNote,
                IsDownward = src.IsDownward,
                PreviousTrainNumber = src.PreviousTrainNumber,
                NextTrainNumber = src.NextTrainNumber,
                StaffStations = src.StaffStations,
            };

        /// <summary>
        /// 駅リストを必要に応じて分割して返す（マーカーなし・再帰）
        /// </summary>
        /// <param name="stations">対象駅リスト</param>
        /// <param name="candidates">残りの分割候補駅名</param>
        /// <returns>分割済みセグメントリスト</returns>
        private List<List<StaffStation>> SplitSegments(
            List<StaffStation> stations,
            IReadOnlyList<string> candidates)
        {
            // マーカーを前後に仮挿入して測定（実際の描画と同条件で判定）
            var withMarkers = new List<StaffStation>(stations.Count + 2);
            withMarkers.Add(CreateContinuationMarker("▼"));
            withMarkers.AddRange(stations);
            withMarkers.Add(CreateContinuationMarker("▼"));

            if (MeasureMaxPage(withMarkers) <= 1)
                return [stations];

            foreach (var candidateName in candidates)
            {
                int splitIdx = stations.FindIndex(s =>
                    s.DisplayName == candidateName &&
                    s.StopType != StopType.SplitContinuation);

                if (splitIdx < 0) continue;

                var front = stations.Take(splitIdx + 1).ToList();
                var back = stations.Skip(splitIdx).ToList();

                var remainingCandidates = candidates
                    .SkipWhile(c => c != candidateName)
                    .Skip(1)
                    .ToList();

                return [front, .. SplitSegments(back, remainingCandidates)];
            }

            return [stations];
        }

        /// <summary>
        /// 指定ページ数を超えるか（0始まりのPageIndex最大値を返す）
        /// </summary>
        private int MeasureMaxPage(List<StaffStation> stations)
        {
            var layouts = Measure(stations);
            return layouts.Count > 0 ? layouts.Max(l => l.PageIndex) : 0;
        }

        /// <summary>
        /// 分割継続マーカー駅を生成
        /// </summary>
        /// <param name="text">表示テキスト</param>
        private static StaffStation CreateContinuationMarker(string text) => new()
        {
            DisplayName = text,
            StopType = StopType.SplitContinuation,
            IsTimingPoint = false,
            TrackNumber = "0",
            Note = "",
        };

        /// <summary>
        /// 標準行高さ取得
        /// </summary>
        /// <param name="station">駅情報</param>
        /// <returns>行高さ</returns>
        private int GetRowHeight(StaffStation station)
        {
            // Todo: 備考による上限超え対応
            if (station.StopType == StopType.SplitContinuation)
            {
                return ROW_HEIGHT_SMALL;
            }

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
                if (station.IsDepShunting)
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
        /// 最少高さ取得
        /// </summary>
        /// <param name="station">駅情報</param>
        /// <returns>行高さ</returns>
        private int GetMinHeight(StaffStation station)
        {
            //空行になる条件の箇所は対象外  
            if (station.StopType == StopType.Pass && station.IsTimingPoint == false)
            {
                return 0;
            }

            int r = 2;

            if (station.TrackNumber != "0")
            {
                r += 22;
            }

            if (station.Note == "")
            {
                return r;
            }

            r += 14 * station.Note.Split("\n").Count();

            return r;
        }



        /// <summary>
        /// 種別色取得
        /// </summary>
        /// <param name="trainType">種別名</param>
        /// <param name="isOpStop">運転停車か</param>
        /// <returns>種別色</returns>
        private Color GetTrainTypeColor(string trainType, bool isOpStop)
        {
            if (isOpStop)
            {
                if (_trainTypeOpColors.TryGetValue(
                    trainType,
                    out Color color))
                {
                    return color;
                }
            }
            else
            {
                if (_trainTypeColors.TryGetValue(
                    trainType,
                    out Color color))
                {
                    return color;
                }
            }

            //
            // 未定義種別時は白
            //
            return Color.White;
        }

        /// <summary>
        /// 列番を整形する（文字が入りうるがない箇所をスペース補完）
        /// </summary>
        /// <param name="trainName">元の列番</param>
        /// <returns>整形済み列番</returns>
        public static string TrainNumberFormat(string trainName)
        {
            Regex TrainNumberRegex = new(
            @"^(回|臨|臨回|検|試)?([0-9]{3,4})([TS]?[ABCDK]?[XYZ]?)?$",
            RegexOptions.Compiled);

            var m = TrainNumberRegex.Match(trainName);

            // 規則外の列番はそのまま返す
            if (!m.Success) return trainName;

            string prefix = m.Groups[1].Value;
            string number = m.Groups[2].Value;
            string suffix = m.Groups[3].Value;

            // プレフィックスを最大文字数（2文字：臨回）に合わせて全角スペース補完
            int prefixPadding = 1 - prefix.Length;
            string paddedPrefix = new string('　', Math.Max(0, prefixPadding)) + prefix;

            // 番号を最大桁数（4桁）に合わせてスペース補完
            string paddedNumber = number.PadLeft(4);

            // サフィックスを[TS][ABCDK][XYZ]の各1文字に分解してスペース補完
            char ts = suffix.FirstOrDefault(c => c is 'T' or 'S');
            char abcdk = suffix.FirstOrDefault(c => c is 'A' or 'B' or 'C' or 'D' or 'K');
            char xyz = suffix.FirstOrDefault(c => c is 'X' or 'Y' or 'Z');

            string paddedSuffix =
                (ts == '\0' ? "" : ts.ToString()) +
                (abcdk == '\0' ? " " : abcdk.ToString()) +
                (xyz == '\0' ? " " : xyz.ToString());

            return $"{paddedPrefix}{paddedNumber}{paddedSuffix}";
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