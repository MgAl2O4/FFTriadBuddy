using System;
using System.Collections.Generic;
using System.IO;

#if DEBUG

namespace FFTriadBuddy.Datamine
{
    public class CsvLocalizedData
    {
        public readonly static string DefaultLanguage = "en";

        public Dictionary<string, CsvData> mapLanguages = new Dictionary<string, CsvData>();
        public CsvData data;

        public int GetNumRows() { return (data != null) ? data.rows.Count : 0; }
        public int GetNumColumns() { return (data != null && data.rows.Count > 0 && data.rows[0] != null) ? data.rows[0].Length : 0; }
        public int GetNumLanguages() { return mapLanguages.Count; }

        public LocString GetLocalizedText(int rowIdx, int columnIdx)
        {
            LocString textData = new LocString();
            foreach (var kvp in mapLanguages)
            {
                // not all locs are build on the same client version, but Ids/rowIdx are in sync
                int langIdx = Array.IndexOf(LocalizationDB.Languages, kvp.Key);
                textData.Text[langIdx] = (rowIdx < kvp.Value.rows.Count) ? kvp.Value.rows[rowIdx][columnIdx] : null;
            }

            return textData;
        }

        public static CsvLocalizedData LoadFrom(string path, int numRowsToSkip = 2)
        {
            CsvLocalizedData resultOb = new CsvLocalizedData();

            if (File.Exists(path))
            {
                resultOb.data = CsvData.LoadFrom(path, numRowsToSkip);
                resultOb.mapLanguages[DefaultLanguage] = resultOb.data;
            }
            else
            {
                foreach (string lang in LocalizationDB.Languages)
                {
                    string locPath = path.Replace(".csv", "." + lang + ".csv");
                    if (File.Exists(locPath))
                    {
                        resultOb.mapLanguages.Add(lang, CsvData.LoadFrom(locPath, numRowsToSkip));
                    }
                }

                if (resultOb.mapLanguages.ContainsKey(DefaultLanguage))
                {
                    resultOb.data = resultOb.mapLanguages[DefaultLanguage];
                }
            }

            return resultOb;
        }
    }

    public class CsvData
    {
        public List<string[]> rows = new List<string[]>();

        public static CsvData LoadFrom(string path, int numRowsToSkip = 2)
        {
            var resultOb = new CsvData();
            int lineIdx = 0;
            int skipCounter = numRowsToSkip;

            using (StreamReader reader = new StreamReader(path))
            {
                string header = reader.ReadLine();
                int numCols = header.Split(',').Length;
                lineIdx++;

                while (!reader.EndOfStream)
                {
                    string row = reader.ReadLine();
                    lineIdx++;
                    if (skipCounter > 0)
                    {
                        skipCounter--;
                        continue;
                    }

                    if (row.Length > 0)
                    {
                        string[] cols = SplitRow(row);
                        if (cols.Length != numCols)
                        {
                            continue;
                            //throw new Exception("Column count mismatch at '" + path + "' line:" + lineIdx);
                        }

                        resultOb.rows.Add(cols);
                    }
                }
            }

            return resultOb;
        }

        private static string[] SplitRow(string csvRow)
        {
            List<string> tokens = new List<string>();
            int sepIdx = csvRow.IndexOf(',');
            int lastSep = -1;
            string prevToken = "";
            bool waitingForString = false;

            while (sepIdx >= 0)
            {
                string token = (sepIdx < 1) ? "" : csvRow.Substring(lastSep + 1, sepIdx - lastSep - 1);
                lastSep = sepIdx;

                if (waitingForString)
                {
                    prevToken += "," + token;

                    if (token.EndsWith("\""))
                    {
                        waitingForString = false;
                        prevToken = prevToken.Substring(1, prevToken.Length - 2);

                        tokens.Add(prevToken);
                    }
                }
                else
                {
                    if (token.Length > 0 && token[0] == '"' && !token.EndsWith("\""))
                    {
                        prevToken = token;
                        waitingForString = true;
                    }
                    else
                    {
                        if (token.Length > 0 && token[0] == '"')
                        {
                            token = (token.Length > 2) ? token.Substring(1, token.Length - 2) : "";
                        }

                        tokens.Add(token);
                    }
                }

                sepIdx = csvRow.IndexOf(',', lastSep + 1);
            }

            string lastToken = (lastSep < 0) ? csvRow : csvRow.Substring(lastSep + 1);
            if (lastToken.Length > 0 && lastToken[0] == '"')
            {
                lastToken = lastToken.Substring(1, lastToken.Length - 2);
            }

            tokens.Add(lastToken);
            return tokens.ToArray();
        }
    }
}

#endif // DEBUG
