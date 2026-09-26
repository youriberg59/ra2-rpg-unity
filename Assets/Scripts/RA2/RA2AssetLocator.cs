using System;
using System.Collections.Generic;
using System.IO;

namespace RA2RPG.RA2
{
    public static class RA2AssetLocator
    {
        private static readonly string[] NestedMixCandidates =
        {
            "conquer.dat",
            "conquer.mix",
            "local.mix",
            "generic.mix",
            "cache.mix",
            "neutral.mix",
            "load.mix",
            "isogen.mix",
            "temperat.mix",
            "isotemp.mix",
            "snow.mix",
            "isosnow.mix",
            "urban.mix",
            "isourb.mix",
            "ubn.mix",
            "isoubn.mix",
            "desert.mix",
            "isodes.mix",
            "lunar.mix",
            "isolun.mix",
            "conqmd.mix",
            "localmd.mix",
            "genericmd.mix",
            "cachemd.mix",
            "neutralmd.mix",
            "isogenmd.mix"
        };

        public sealed class SearchResult
        {
            public string Path { get; }
            public byte[] Data { get; }

            public SearchResult(string path, byte[] data)
            {
                Path = path;
                Data = data;
            }
        }

        public sealed class SearchTrace
        {
            public readonly List<string> Lines = new List<string>();

            public void Add(string line)
            {
                Lines.Add(line);
            }

            public override string ToString()
            {
                return string.Join("\n", Lines);
            }
        }

        public static SearchResult FindInArchiveTree(
            string rootMixPath,
            string filename,
            int maxDepth = 3,
            SearchTrace trace = null
        )
        {
            using var root = new MixArchive(rootMixPath);
            trace?.Add($"OPEN {Path.GetFileName(rootMixPath)} : {root.EntryCount} entries");
            return FindRecursive(
                root,
                Path.GetFileName(rootMixPath),
                filename,
                0,
                maxDepth,
                trace
            );
        }

        public static SearchResult FindInDirectory(
            string rootDirectory,
            string filename,
            int maxDepth = 3,
            SearchTrace trace = null
        )
        {
            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
                return null;

            var archives = new List<string>();
            archives.AddRange(Directory.GetFiles(
                rootDirectory,
                "*.mix",
                SearchOption.AllDirectories
            ));
            archives.AddRange(Directory.GetFiles(
                rootDirectory,
                "*.dat",
                SearchOption.AllDirectories
            ));

            string[] archiveFiles = archives.ToArray();
            Array.Sort(archiveFiles, StringComparer.OrdinalIgnoreCase);

            foreach (string mixPath in archiveFiles)
            {
                try
                {
                    trace?.Add($"ROOT {Path.GetFileName(mixPath)}");
                    var result = FindInArchiveTree(mixPath, filename, maxDepth, trace);
                    if (result != null)
                    {
                        string relative = Path.GetRelativePath(rootDirectory, mixPath);
                        return new SearchResult(
                            relative + result.Path.Substring(Path.GetFileName(mixPath).Length),
                            result.Data
                        );
                    }
                }
                catch (Exception ex)
                {
                    trace?.Add($"ERROR {Path.GetFileName(mixPath)} : {ex.Message}");
                    // One unreadable/corrupt MIX must not stop the global search.
                }
            }

            return null;
        }

        private static SearchResult FindRecursive(
            MixArchive archive,
            string archivePath,
            string filename,
            int depth,
            int maxDepth,
            SearchTrace trace
        )
        {
            if (archive.Contains(filename))
            {
                return new SearchResult(
                    archivePath + " → " + filename,
                    archive.ReadFile(filename)
                );
            }

            if (depth >= maxDepth)
                return null;

            foreach (string childName in NestedMixCandidates)
            {
                if (!archive.Contains(childName))
                    continue;

                byte[] childBytes;
                try
                {
                    childBytes = archive.ReadFile(childName);
                }
                catch (Exception ex)
                {
                    trace?.Add($"  READ ERROR {archivePath} → {childName} : {ex.Message}");
                    continue;
                }

                try
                {
                    using var child = new MixArchive(childBytes, childName);
                    trace?.Add(
                        $"{new string(' ', (depth + 1) * 2)}OPEN {archivePath} → {childName} : {child.EntryCount} entries"
                    );

                    var found = FindRecursive(
                        child,
                        archivePath + " → " + childName,
                        filename,
                        depth + 1,
                        maxDepth,
                        trace
                    );

                    if (found != null)
                        return found;
                }
                catch (Exception ex)
                {
                    trace?.Add(
                        $"{new string(' ', (depth + 1) * 2)}OPEN ERROR {archivePath} → {childName} : {ex.Message}"
                    );
                    // Keep searching sibling containers.
                }
            }

            return null;
        }
    }
}
