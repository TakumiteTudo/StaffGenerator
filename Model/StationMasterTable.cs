using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StaffGenerator.Model;

namespace StaffGenerator.Model
{
    /// <summary>
    /// 番線ごとの開扉方向（下り/上り別）
    /// </summary>
    public class TrackDoorDirectionEntry
    {
        /// <summary>下り方向の開扉方向</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DoorDirection Kudari { get; set; } = DoorDirection.Null;

        /// <summary>上り方向の開扉方向</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DoorDirection Nobori { get; set; } = DoorDirection.Null;
    }

    /// <summary>
    /// 駅マスタの1レコード
    /// </summary>
    public class StationMasterRecord
    {
        /// <summary>駅ID</summary>
        public string StationID { get; set; } = "";

        /// <summary>スタフ上の表示駅名</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 番線ごとの開扉方向（キー：番線文字列）
        /// 定義がない番線はNullとして扱う
        /// </summary>
        public Dictionary<string, TrackDoorDirectionEntry> TrackDoorDirections { get; set; } = new();
    }

    /// <summary>
    /// stationIDをキーにDisplayName・DoorDirectionを引くマスタテーブル
    /// </summary>
    public class StationMasterTable
    {
        private readonly Dictionary<string, StationMasterRecord> _table;

        public StationMasterTable(IEnumerable<StationMasterRecord> records)
        {
            _table = records.ToDictionary(r => r.StationID, r => r);
        }

        /// <summary>
        /// JSONファイルから生成する
        /// </summary>
        public static StationMasterTable LoadFromFile(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            return LoadFromJson(json);
        }

        /// <summary>
        /// JSON文字列から生成する
        /// </summary>
        public static StationMasterTable LoadFromJson(string json)
        {
            var opt = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Converters = { new JsonStringEnumConverter() }
            };
            var list = JsonSerializer.Deserialize<List<StationMasterRecord>>(json, opt)
                       ?? new List<StationMasterRecord>();
            return new StationMasterTable(list);
        }

        /// <summary>
        /// stationIDからDisplayNameを取得する（マスタになければfallbackを返す）
        /// </summary>
        public string ResolveDisplayName(string stationID, string fallback)
        {
            if (_table.TryGetValue(stationID, out var rec) && !string.IsNullOrEmpty(rec.DisplayName))
                return rec.DisplayName;
            return fallback;
        }

        /// <summary>
        /// stationID・番線・方向からDoorDirectionを取得する
        /// 駅未登録・番線未定義の場合はNullを返す
        /// </summary>
        public DoorDirection ResolveDoorDirection(string stationID, string bansen, bool isDownward)
        {
            if (!_table.TryGetValue(stationID, out var rec)) return DoorDirection.Null;
            if (string.IsNullOrEmpty(bansen)) return DoorDirection.Null;
            if (!rec.TrackDoorDirections.TryGetValue(bansen, out var entry)) return DoorDirection.Null;
            return isDownward ? entry.Kudari : entry.Nobori;
        }
    }
}