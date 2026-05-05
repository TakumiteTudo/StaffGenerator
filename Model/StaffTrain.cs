namespace StaffGenerator.Model
{
    public class StaffTrain
    {
        public string TrainName = "";
        public string TrainType = "";
        public string TrainTypeImgName = "";
        public string TrainDestination = "";
        public string TrainNote = "";
        public bool IsDownward = true; // 下り方向フラグ
        public List<StaffStation> StaffStations = new List<StaffStation>();
    }
}
