namespace StaffGenerator.Model
{
    public class StaffTrain
    {
        public int OperationNumber { get; set; }
        public string TrainName { get; set; } = "";
        public string TrainType { get; set; } = "";
        public string TrainTypeImgName { get; set; } = "";
        public string TrainDestination { get; set; } = "";
        public string TrainNote { get; set; } = "";
        public bool IsDownward { get; set; } = true; // 下り方向フラグ               
        public string PreviousTrainNumber { get; set; } = "";
        public string NextTrainNumber { get; set; } = "";

        public List<StaffStation> StaffStations { get; set; } = new List<StaffStation>();
    }
}
