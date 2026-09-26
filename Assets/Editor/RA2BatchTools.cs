using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using RA2RPG.RA2;

namespace RA2RPG.EditorTools
{
    public static class RA2BatchTools
    {
        [MenuItem("RA2 RPG/Run Full Import Diagnostic")]
        public static void RunFullImportDiagnosticFromMenu()
        {
            RunFullImportDiagnostic();
        }

        public static void RunFullImportDiagnostic()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string localRa2 = Path.Combine(projectRoot, "LocalRA2");
            string logsDir = Path.Combine(projectRoot, "Logs");
            string reportPath = Path.Combine(logsDir, "ra2-import-report.txt");

            Directory.CreateDirectory(logsDir);

            var sb = new StringBuilder();
            sb.AppendLine("RA2 RPG Unity - Full Import Diagnostic");
            sb.AppendLine("====================================");
            sb.AppendLine($"Unity: {Application.unityVersion}");
            sb.AppendLine($"Project: {projectRoot}");
            sb.AppendLine($"LocalRA2: {localRa2}");
            sb.AppendLine($"Timestamp: {DateTime.Now:O}");
            sb.AppendLine();

            if (!Directory.Exists(localRa2))
            {
                sb.AppendLine("ERROR: LocalRA2 directory does not exist.");
                File.WriteAllText(reportPath, sb.ToString());
                Debug.LogError($"RA2 diagnostic failed. Report: {reportPath}");
                return;
            }

            var files = Directory.GetFiles(localRa2, "*.*", SearchOption.AllDirectories)
                .Where(p =>
                    p.EndsWith(".mix", StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            sb.AppendLine($"Top-level MIX/DAT candidates: {files.Length}");
            foreach (string file in files)
                sb.AppendLine($"  {Path.GetRelativePath(localRa2, file)}");
            sb.AppendLine();

            uint e2Hash = WestwoodCrc32.HashFilename("E2.SHP");
            sb.AppendLine($"E2.SHP hash = 0x{e2Hash:X8}");
            sb.AppendLine();

            string ra2Mix = files.FirstOrDefault(p =>
                string.Equals(Path.GetFileName(p), "ra2.mix", StringComparison.OrdinalIgnoreCase));

            if (ra2Mix == null)
            {
                sb.AppendLine("ERROR: ra2.mix not found under LocalRA2.");
                File.WriteAllText(reportPath, sb.ToString());
                Debug.LogError($"RA2 diagnostic failed. Report: {reportPath}");
                return;
            }

            try
            {
                using var root = new MixArchive(ra2Mix);
                sb.AppendLine($"OPEN ra2.mix : {root.EntryCount} entries");
                sb.AppendLine($"Contains conquer.mix: {root.Contains("conquer.mix")}");
                sb.AppendLine($"Contains E2.SHP directly: {root.Contains("E2.SHP")}");

                if (root.Contains("conquer.mix"))
                {
                    byte[] conquerBytes = root.ReadFile("conquer.mix");
                    sb.AppendLine($"Extracted conquer.mix bytes: {conquerBytes.Length}");

                    using var conquer = new MixArchive(conquerBytes, "conquer.mix");
                    sb.AppendLine($"OPEN conquer.mix : {conquer.EntryCount} entries");
                    sb.AppendLine($"Contains E2.SHP: {conquer.Contains("E2.SHP")}");
                    sb.AppendLine();

                    sb.AppendLine("conquer.mix raw entry hashes:");
                    foreach (var entry in conquer.Entries.OrderBy(e => e.Hash))
                    {
                        string marker = entry.Hash == e2Hash ? "  <== E2.SHP HASH MATCH" : "";
                        sb.AppendLine(
                            $"0x{entry.Hash:X8} offset={entry.Offset} length={entry.Length}{marker}"
                        );
                    }

                    sb.AppendLine();

                    string[] probes =
                    {
                        "E2.SHP",
                        "E2",
                        "CONS.SHP",
                        "CONSCRIPT.SHP",
                        "CONSCRIPT",
                        "CONSCRIP.SHP",
                        "CONSCRPT.SHP",
                        "CONSS.SHP",
                        "SOVINF.SHP"
                    };

                    sb.AppendLine("Conscript filename probes:");
                    foreach (string probe in probes)
                    {
                        uint h = WestwoodCrc32.HashFilename(probe);
                        sb.AppendLine(
                            $"{probe,-16} 0x{h:X8} {(conquer.Contains(probe) ? "FOUND" : "-")}"
                        );
                    }
                }
                else
                {
                    sb.AppendLine("ERROR: ra2.mix does not contain conquer.mix.");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine("EXCEPTION:");
                sb.AppendLine(ex.ToString());
            }

            sb.AppendLine();
            sb.AppendLine("Global recursive search:");
            try
            {
                var trace = new RA2AssetLocator.SearchTrace();
                var result = RA2AssetLocator.FindInDirectory(localRa2, "E2.SHP", 4, trace);
                sb.AppendLine(result == null ? "NOT FOUND" : $"FOUND: {result.Path}");
                sb.AppendLine(trace.ToString());
            }
            catch (Exception ex)
            {
                sb.AppendLine("Global search exception:");
                sb.AppendLine(ex.ToString());
            }

            File.WriteAllText(reportPath, sb.ToString());
            Debug.Log($"RA2 diagnostic complete: {reportPath}");
        }
    }
}
