using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StaffGenerator.Model
{
    /// <summary>
    /// TTC形式JSON - 列車ごとの情報
    /// </summary>
    [System.Serializable]
    public class TTC_Train
    {
        public int operationNumber;
        public string trainNumber;
        public string previousTrainNumber;
        public string nextTrainNumber;
        public string trainClass;
        public string originStationID;
        public string originStationName;
        public string destinationStationID;
        public string destinationStationName;
        public List<TTC_StationData> staList = new List<TTC_StationData>();
        public string[] temporaryStopStations;
        public bool isRegularService = true;
        public int carCount = 4;
    }

    /// <summary>
    /// TTC形式JSON - 停車駅の情報
    /// </summary>
    [System.Serializable]
    public class TTC_StationData
    {
        public string stationID = "";
        public string stationName;
        public string stopPosName;
        public string bansen = "";
        public string script = "";
        public bool isSaiji = true;
        public string biko = "";
        public StopType StopType;
        public TTC_TimeOfDay arrivalTime;
        public TTC_TimeOfDay departureTime;
    }

    /// <summary>
    /// TTC形式JSON - 時刻（h==-1で時刻なし）
    /// </summary>
    [System.Serializable]
    public class TTC_TimeOfDay
    {
        public int h;
        public int m;
        public int s;
    }
}
