namespace StaffGenerator.Model
{
    /// <summary>
    /// 種別・行先の1字省略対応表（種別・行先共用）
    /// </summary>
    public class StaffNoteAbbreviationTable
    {
        private readonly Dictionary<string, string> _table;

        public StaffNoteAbbreviationTable(Dictionary<string, string> table)
        {
            _table = table;
        }

        /// <summary>種別の省略文字を返す（未登録なら先頭1字）</summary>
        public string Class(string trainClass)
        {
            if (string.IsNullOrEmpty(trainClass)) return "";
            return _table.TryGetValue(trainClass, out var v) ? v : trainClass[..1];
        }

        /// <summary>行先の省略文字を返す（未登録なら先頭1字）</summary>
        public string Dest(string destination)
        {
            if (string.IsNullOrEmpty(destination)) return "";
            return _table.TryGetValue(destination, out var v) ? v : destination[..1];
        }

        /// <summary>TSVファイルから生成する</summary>
        public static StaffNoteAbbreviationTable LoadFromFile(string tsvPath)
            => LoadFromTsv(File.ReadAllText(tsvPath));

        /// <summary>TSV文字列から生成する（空行・#コメント行を無視）</summary>
        public static StaffNoteAbbreviationTable LoadFromTsv(string tsv)
        {
            var table = new Dictionary<string, string>();
            foreach (var line in tsv.Split('\n'))
            {
                var l = line.Trim();
                if (string.IsNullOrEmpty(l) || l.StartsWith('#')) continue;
                var cols = l.Split('\t');
                if (cols.Length < 2) continue;
                table[cols[0]] = cols[1];
            }
            return new StaffNoteAbbreviationTable(table);
        }
    }
}