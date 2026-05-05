namespace StaffGenerator.Model
{
    /// <summary>
    /// 路線設定（単線区間）
    /// </summary>
    public class RouteConfig
    {
        private readonly HashSet<string> _singleTrackSet;

        public RouteConfig(HashSet<string> singleTrackSet)
        {
            _singleTrackSet = singleTrackSet;
        }

        /// <summary>2駅間が単線区間かどうかを返す</summary>
        public bool IsSingleTrackBetween(string staIdA, string staIdB)
            => _singleTrackSet.Contains(SectionKey(staIdA, staIdB));

        /// <summary>TSVファイルから生成する</summary>
        public static RouteConfig LoadFromFile(string tsvPath)
            => LoadFromTsv(File.ReadAllText(tsvPath));

        /// <summary>TSV文字列から生成する（空行・#コメント行を無視）</summary>
        public static RouteConfig LoadFromTsv(string tsv)
        {
            var set = new HashSet<string>();
            foreach (var line in tsv.Split('\n'))
            {
                var l = line.Trim();
                if (string.IsNullOrEmpty(l) || l.StartsWith('#')) continue;
                var cols = l.Split('\t');
                if (cols.Length < 2) continue;
                set.Add(SectionKey(cols[0], cols[1]));
            }
            return new RouteConfig(set);
        }

        private static string SectionKey(string a, string b)
            => string.Compare(a, b, StringComparison.Ordinal) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
    }
}