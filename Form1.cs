using StaffGenerator.Model;
using StaffGenerator.Parser;
using StaffGenerator.Render;
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
            var train1242 = new StaffTrain
            {
                TrainName = "1242",
                TrainType = "普通",
                TrainTypeImgName = "普通",
                TrainDestination = "大路",
                TrainNote = "ワンマン",
                StaffStations = new List<StaffStation>
                {
                    new StaffStation
                    {
                        DisplayName = "江原車庫",
                        ArrivalTime = null,
                        DepartureTime = TimeSpan.Parse("12:20:00"),
                        IsTimingPoint = true,
                        StopType = StopType.OpStop,
                        TrackNumber = "13",
                        DoorDirection = DoorDirection.Null,
                        Note = "",
                        IsShunting = true,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "大道寺",
                        ArrivalTime = TimeSpan.Parse("12:21:45"),
                        DepartureTime = TimeSpan.Parse("12:23:00"),
                        IsTimingPoint = true,
                        StopType = StopType.Stop,
                        TrackNumber = "1",
                        DoorDirection = DoorDirection.Right,
                        Note = "(接)準 道 ②20着\n× 普 道 20着",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "藤江",
                        ArrivalTime = TimeSpan.Parse("12:25:20"),
                        DepartureTime = TimeSpan.Parse("12:25:40"),
                        IsTimingPoint = true,
                        StopType = StopType.Stop,
                        TrackNumber = "2",
                        DoorDirection = DoorDirection.Left,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "水越",
                        ArrivalTime = TimeSpan.Parse("12:27:25"),
                        DepartureTime = TimeSpan.Parse("12:27:45"),
                        IsTimingPoint = true,
                        StopType = StopType.Stop,
                        TrackNumber = "1",
                        DoorDirection = DoorDirection.Left,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "高見沢",
                        ArrivalTime = TimeSpan.Parse("12:30:00"),
                        DepartureTime = TimeSpan.Parse("12:34:00"),
                        IsTimingPoint = true,
                        StopType = StopType.Stop,
                        TrackNumber = "2",
                        DoorDirection = DoorDirection.Left,
                        Note = "× 特 館 31通\n× 普 道 33着",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "日野森",
                        ArrivalTime = TimeSpan.Parse("12:36:05"),
                        DepartureTime = TimeSpan.Parse("12:36:25"),
                        IsTimingPoint = true,
                        StopType = StopType.Stop,
                        TrackNumber = "2",
                        DoorDirection = DoorDirection.Left,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "奥峯口",
                        ArrivalTime = TimeSpan.Parse("12:38:00"),
                        DepartureTime = TimeSpan.Parse("12:38:20"),
                        IsTimingPoint = false,
                        StopType = StopType.Stop,
                        TrackNumber = "",
                        DoorDirection = DoorDirection.Left,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                }
            };
            var bitmap = StaffRenderer.Render(train1242);
            PictureBox1_Render(bitmap);
        }

        private void button3_Click(object sender, EventArgs e)
        {

            var train1242 = new StaffTrain
            {
                TrainName = "1215A",
                TrainType = "C特1",
                TrainTypeImgName = "特急",
                TrainDestination = "館浜",
                TrainNote = "",
                StaffStations = new List<StaffStation>
                {
                    new StaffStation
                    {
                        DisplayName = "新大路",
                        ArrivalTime = TimeSpan.Parse("12:33:50"),
                        DepartureTime = TimeSpan.Parse("12:36:00"),
                        IsTimingPoint = true,
                        StopType = StopType.Stop,
                        TrackNumber = "4",
                        DoorDirection = DoorDirection.Right,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "桜坂",
                        ArrivalTime = null,
                        DepartureTime = TimeSpan.Parse("12:23:00"),
                        IsTimingPoint = false,
                        StopType = StopType.Pass,
                        TrackNumber = "",
                        DoorDirection = DoorDirection.Null,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "東井",
                        ArrivalTime = null,
                        DepartureTime = TimeSpan.Parse("12:38:05"),
                        IsTimingPoint = true,
                        StopType = StopType.Pass,
                        TrackNumber = "5",
                        DoorDirection = DoorDirection.Null,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "白石町",
                        ArrivalTime = null,
                        DepartureTime = TimeSpan.Parse("12:23:00"),
                        IsTimingPoint = false,
                        StopType = StopType.Pass,
                        TrackNumber = "",
                        DoorDirection = DoorDirection.Null,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "二木戸",
                        ArrivalTime = null,
                        DepartureTime = TimeSpan.Parse("12:39:35"),
                        IsTimingPoint = true,
                        StopType = StopType.Pass,
                        TrackNumber = "",
                        DoorDirection = DoorDirection.Null,
                        Note = "✕ 普通 大路",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "三石",
                        ArrivalTime = null,
                        DepartureTime = TimeSpan.Parse("12:40:40"),
                        IsTimingPoint = true,
                        StopType = StopType.Pass,
                        TrackNumber = "2",
                        DoorDirection = DoorDirection.Null,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                    new StaffStation
                    {
                        DisplayName = "名田",
                        ArrivalTime = TimeSpan.Parse("12:41:50"),
                        DepartureTime = TimeSpan.Parse("12:42:50"),
                        IsTimingPoint = true,
                        StopType = StopType.Stop,
                        TrackNumber = "2",
                        DoorDirection = DoorDirection.Right,
                        Note = "",
                        IsShunting = false,
                        Script = ""
                    },
                }
            };
            var bitmap = StaffRenderer.Render(train1242);
            PictureBox1_Render(bitmap);
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
    }
}
