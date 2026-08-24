using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace StudentInfoSystem.Portal.Services;

public static class SimpleScheduleParser
{
    public static List<string> ParseScheduleSummary(string html)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(html)) return result;

        var pattern = @"activity\s*=\s*new\s*TaskActivity\(([^;]+)\);\s*index\s*=(\d+)\*unitCount\+(\d+);";
        var matches = Regex.Matches(html, pattern);

        foreach (Match match in matches)
        {
            try
            {
                var parts = SplitParameters(match.Groups[1].Value);
                if (parts.Count < 4) continue;

                var teacher = parts[1].Trim('\'', '"');
                var courseName = parts[3].Trim('\'', '"');
                var dayIndex = int.Parse(match.Groups[2].Value);
                var periodIndex = int.Parse(match.Groups[3].Value);

                result.Add($"{courseName} | 周{dayIndex + 1} 第{periodIndex + 1}节 | {teacher}");
            }
            catch
            {
                // ignore malformed entries
            }
        }

        return result;
    }

    private static List<string> SplitParameters(string parameters)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inString = false;
        char delimiter = '\0';
        int nested = 0;

        for (int i = 0; i < parameters.Length; i++)
        {
            var c = parameters[i];
            if ((c == '\'' || c == '"') && (i == 0 || parameters[i - 1] != '\\'))
            {
                if (!inString)
                {
                    inString = true;
                    delimiter = c;
                }
                else if (c == delimiter)
                {
                    inString = false;
                }
                current.Append(c);
            }
            else if (c == '(' && !inString)
            {
                nested++;
                current.Append(c);
            }
            else if (c == ')' && !inString)
            {
                nested--;
                current.Append(c);
            }
            else if (c == ',' && !inString && nested == 0)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0) result.Add(current.ToString().Trim());
        return result;
    }
}
