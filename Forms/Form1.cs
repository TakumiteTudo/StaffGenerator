using StaffGenerator.Forms;
using StaffGenerator.Model;
using StaffGenerator.Parser;
using StaffGenerator.Render;
using System.Security.Cryptography;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace StaffGenerator
{
    public partial class Form1 : Form
    {
        StaffRenderer StaffRenderer;
        StationMasterTable MasterTable;
        StaffNoteAbbreviationTable AbbrTable;
        RouteConfig RouteConfig;
        string _file;

        List<StaffTrain> _loadedTrains = new();
        int _currentIndex = 0;

        /// <summary>ExportTrain列番リストパス</summary>
        private const string ExportTrainListPath = "Data/ExportTrain.txt";

        /// <summary>スタフ出力フォルダ（指定列車）</summary>
        private const string ExportDirTrain = "Staff";

        /// <summary>スタフ出力フォルダ（全列車）</summary>
        private const string ExportDirAll = "Staff/All";

        /// <summary>現在表示中のスタフ画像リスト</summary>
        private List<Bitmap> _currentBitmaps = [];

        /// <summary>現在表示中の枚目（0始まり）</summary>
        private int _currentPageIndex = 0;


        public Form1()
        {
            InitializeComponent();
            StaffRenderer = new StaffRenderer("Image/行路表テンプレート.png");

            // 駅マスタを起動時に読み込む
            MasterTable = StationMasterTable.LoadFromFile("Data/station_master.json");
            AbbrTable = StaffNoteAbbreviationTable.LoadFromFile("Data/abbreviation.txt");
            RouteConfig = RouteConfig.LoadFromFile("Data/route_config.txt");

            _file = "E:\\Takumite\\Desktop\\ttc_data_train.txt";
            LoadTrains(_file);
            _currentIndex = 0;

            // ComboBoxを更新
            comboBox1.Items.Clear();
            foreach (var train in _loadedTrains)
            {
                comboBox1.Items.Add($"{train.TrainName}");
            }

            if (comboBox1.Items.Count > 0)
                comboBox1.SelectedIndex = 0;
        }

        private void PictureBox1_Render(List<Bitmap> bitmaps)
        {
            pictureBox1.Image = bitmaps[0];
        }

        /// <summary>
        /// 描画結果をセットしてページ表示を初期化
        /// </summary>
        /// <param name="bitmaps">描画済み画像リスト</param>
        private void SetBitmaps(List<Bitmap> bitmaps)
        {
            _currentBitmaps = bitmaps;
            _currentPageIndex = 0;
            UpdateDisplay();
        }

        /// <summary>
        /// 現在のページ表示を更新
        /// </summary>
        private void UpdateDisplay()
        {
            if (_currentBitmaps.Count == 0) return;

            pictureBox1.Image = _currentBitmaps[_currentPageIndex];
            labelPage.Text = $"{_currentPageIndex + 1} / {_currentBitmaps.Count} 枚";
            button1.Enabled = _currentPageIndex > 0;
            button3.Enabled = _currentPageIndex < _currentBitmaps.Count - 1;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "TTCデータファイルを選択",
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            if (dialog.FileName == "") return;
            _file = dialog.FileName;

            LoadTrains(_file);

            _currentIndex = 0;

            // ComboBoxを更新
            comboBox1.Items.Clear();
            foreach (var train in _loadedTrains)
            {
                comboBox1.Items.Add($"{train.TrainName}");
            }

            if (comboBox1.Items.Count > 0)
                comboBox1.SelectedIndex = 0;
        }

        private void LoadTrains(string jsonPath)
        {
            _loadedTrains = TtcStaffConverter.ConvertFromFile(jsonPath, MasterTable);
            StaffNoteGenerator.Apply(_loadedTrains, AbbrTable, RouteConfig); // 追加
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                UpdateDisplay();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (_currentPageIndex < _currentBitmaps.Count - 1)
            {
                _currentPageIndex++;
                UpdateDisplay();
            }
        }


        /// <summary>
        /// 選択中の列車を描画する
        /// </summary>
        private void button4_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex < 0 || comboBox1.SelectedIndex >= _loadedTrains.Count)
                return;

            var train = _loadedTrains[comboBox1.SelectedIndex];
            var bitmaps = StaffRenderer.Render(train, _loadedTrains);
            SetBitmaps(bitmaps); // 修正：戻り値変更に合わせて
        }

        private void button5_Click(object sender, EventArgs e)
        {
            MasterTable = StationMasterTable.LoadFromFile("Data/station_master.json");
            AbbrTable = StaffNoteAbbreviationTable.LoadFromFile("Data/abbreviation.txt");
            RouteConfig = RouteConfig.LoadFromFile("Data/route_config.txt");

            LoadTrains(_file);
            StaffRenderer = new StaffRenderer("Image/行路表テンプレート.png");

            if (comboBox1.SelectedIndex < 0 || comboBox1.SelectedIndex >= _loadedTrains.Count)
                return;

            var train = _loadedTrains[comboBox1.SelectedIndex];
            var bitmaps = StaffRenderer.Render(train, _loadedTrains);
            SetBitmaps(bitmaps); // 修正：戻り値変更に合わせて
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = checkBox1.Checked;
        }
        /// <summary>
        /// 指定列車リストのスタフをファイル出力する
        /// </summary>
        /// <param name="targets">出力対象列車リスト</param>
        /// <param name="outputDir">出力フォルダ</param>
        /// <param name="title">進捗ウィンドウタイトル</param>
        private void ExportStaffs(List<StaffTrain> targets, string outputDir, string title)
        {
            Directory.CreateDirectory(outputDir);

            using var progress = new ExportProgressForm(targets.Count, title);
            progress.Show(this);

            foreach (var train in targets)
            {
                var bitmaps = StaffRenderer.Render(train, _loadedTrains);
                for (int i = 0; i < bitmaps.Count; i++)
                {
                    string fileName = i == 0
                        ? $"{train.TrainName}.png"
                        : $"{train.TrainName}_{i + 1}.png";
                    bitmaps[i].Save(Path.Combine(outputDir, fileName),
                        System.Drawing.Imaging.ImageFormat.Png);
                }
                progress.Step(train.TrainName);
            }
        }

        /// <summary>
        /// ExportTrain.txtに記載された列番のスタフを出力
        /// </summary>
        private void button6_Click(object sender, EventArgs e)
        {
            if (!File.Exists(ExportTrainListPath))
            {
                MessageBox.Show($"{ExportTrainListPath} が見つかりません。", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var targetNames = File.ReadAllLines(ExportTrainListPath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToHashSet();

            var targets = _loadedTrains
                .Where(t => targetNames.Contains(t.TrainName))
                .ToList();

            if (targets.Count == 0)
            {
                MessageBox.Show("出力対象の列車が見つかりませんでした。", "情報",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ExportStaffs(targets, ExportDirTrain, "スタフ出力中（指定列車）");
            MessageBox.Show($"{targets.Count} 列車のスタフを出力しました。", "完了",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 全列車のスタフを出力
        /// </summary>
        private void button7_Click(object sender, EventArgs e)
        {
            if (_loadedTrains.Count == 0)
            {
                MessageBox.Show("列車データが読み込まれていません。", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ExportStaffs(_loadedTrains, ExportDirAll, "スタフ出力中（全列車）");
            MessageBox.Show($"{_loadedTrains.Count} 列車のスタフを出力しました。", "完了",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
