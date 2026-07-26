using System.Text;

namespace PassTheFear.Thai;

/// <summary>
/// key-TAB-value table, loaded from a loose file so translations can be edited without a
/// rebuild. UTF-8, '#' comments, LF or CRLF. A literal tab in a value is impossible — use \t.
/// </summary>
internal sealed class ThaiTable
{
    private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);

    public int Count => _map.Count;
    public IEnumerable<KeyValuePair<string, string>> Entries => _map;

    public static ThaiTable Load(string path)
    {
        var table = new ThaiTable();

        if (!File.Exists(path))
        {
            Plugin.Logger?.LogWarning($"No translation file at {path} — injecting nothing.");
            return table;
        }

        int duplicates = 0, unparsable = 0, lineNo = 0;

        foreach (var raw in File.ReadAllLines(path, new UTF8Encoding(false)))
        {
            lineNo++;
            var line = raw;
            if (lineNo == 1 && line.Length > 0 && line[0] == '﻿') line = line[1..];
            if (line.Length == 0 || line[0] == '#') continue;

            int tab = line.IndexOf('\t');
            if (tab <= 0) { unparsable++; continue; }

            var key = line[..tab];
            if (table._map.ContainsKey(key)) duplicates++;
            table._map[key] = Unescape(line[(tab + 1)..]);
        }

        if (duplicates > 0 || unparsable > 0)
        {
            Plugin.Logger?.LogWarning(
                $"{Path.GetFileName(path)}: {duplicates} duplicate keys, {unparsable} unparsable lines.");
        }

        return table;
    }

    private static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;

        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 >= s.Length) { sb.Append(s[i]); continue; }
            switch (s[++i])
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case '\\': sb.Append('\\'); break;
                default: sb.Append('\\').Append(s[i]); break;
            }
        }
        return sb.ToString();
    }
}
