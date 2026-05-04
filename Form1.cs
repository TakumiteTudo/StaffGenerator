using StaffGenerator.Model;
using StaffGenerator.Render;

namespace StaffGenerator
{
    public partial class Form1 : Form
    {
        StaffRenderer StaffRenderer;
        public Form1()
        {
            InitializeComponent();
            StaffRenderer = new StaffRenderer("Image/行路表テンプレート.png");
        }

        private void PictureBox1_Render(Bitmap bitmap)
        {
            pictureBox1.Image = bitmap;
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
                        Note = "",
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
                        Note = "✕ 特 館浜\n✕ 普 大道寺",
                        IsShunting = false,
                        Script = ""
                    },
                }
            };
            var bitmap = StaffRenderer.Render(train1242);
            PictureBox1_Render(bitmap);
        }
    }
}
