using StaffGenerator.Model;
using StaffGenerator.Parser;
using StaffGenerator.Render;
using System.Security.Cryptography;
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
                comboBox1.Items.Add($"{train.TrainName} {train.TrainType} {train.TrainDestination}行");
            }

            if (comboBox1.Items.Count > 0)
                comboBox1.SelectedIndex = 0;
        }

        private void PictureBox1_Render(Bitmap bitmap)
        {
            pictureBox1.Image = bitmap;
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
                comboBox1.Items.Add($"{train.TrainName} {train.TrainType} {train.TrainDestination}行");
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
        }

        private void button3_Click(object sender, EventArgs e)
        {
        }


        /// <summary>
        /// 選択中の列車を描画する
        /// </summary>
        private void button4_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex < 0 || comboBox1.SelectedIndex >= _loadedTrains.Count)
                return;

            var train = _loadedTrains[comboBox1.SelectedIndex];
            var bitmap = StaffRenderer.Render(train);
            PictureBox1_Render(bitmap);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            LoadTrains(_file);
            StaffRenderer = new StaffRenderer("Image/行路表テンプレート.png");

            if (comboBox1.SelectedIndex < 0 || comboBox1.SelectedIndex >= _loadedTrains.Count)
                return;

            var train = _loadedTrains[comboBox1.SelectedIndex];
            var bitmap = StaffRenderer.Render(train);
            PictureBox1_Render(bitmap);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = checkBox1.Checked;
        }
    }
}
