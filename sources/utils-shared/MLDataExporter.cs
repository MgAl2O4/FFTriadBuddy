using System;
using System.IO;
using System.Text;

namespace MgAl2O4.Utils
{
    public class MLDataExporter
    {
        private StringBuilder exportSB = new StringBuilder();
        public string exportPath;

        public void StartDataExport(string logDesc)
        {
            Logger.WriteLine("Starting ML export for: {0}...", logDesc);

            exportSB.Clear();
            exportSB.Append("{\"dataset\":[");
        }

        public void FinishDataExport(string fileName)
        {
            var exportJson = exportSB.ToString();
            exportSB.Clear();

            exportJson = exportJson.Remove(exportJson.Length - 1, 1);
            exportJson += "\n]}";

            try
            {
                Directory.CreateDirectory(exportPath);
                File.WriteAllText(Path.Combine(exportPath, fileName), exportJson);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed! Exception: {0}", ex);
            }
        }

        public void ExportValues(float[] values, int classId)
        {
            exportSB.Append("\n{\"input\":[");
            exportSB.Append(string.Join(",", values));
            exportSB.Append("], \"output\":");
            exportSB.Append(classId);
            exportSB.Append("},");
        }

        public void ExportValuesWithContext(float[] values, int classId, int contextId)
        {
            exportSB.Append("\n{\"input\":[");
            exportSB.Append(string.Join(",", values));
            exportSB.Append("], \"output\":");
            exportSB.Append(classId);
            exportSB.Append(", \"ctx\":");
            exportSB.Append(contextId);
            exportSB.Append("},");
        }
    }
}
