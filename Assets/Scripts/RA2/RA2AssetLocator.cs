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

        public static SearchResult FindInArchiveTree(string rootMixPath, string filename, int maxDepth = 3)
        {
            using var root = new MixArchive(rootMixPath);
            return FindRecursive(root, Path.GetFileName(rootMixPath), filename, 0, maxDepth);
        }

        public static SearchResult FindInDirectory(string rootDirectory, string filename, int maxDepth = 3)
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
                    var result = FindInArchiveTree(mixPath, filename, maxDepth);
                    if (result != null)
                    {
                        string relative = Path.GetRelativePath(rootDirectory, mixPath);
                        return new SearchResult(
                            relative + result.Path.Substring(Path.GetFileName(mixPath).Length),
                            result.Data
                        );
                    }
                }
                catch
                {
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
            int maxDepth
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
                catch
                {
                    continue;
                }

                try
                {
                    using var child = new MixArchive(childBytes, childName);
                    var found = FindRecursive(
                        child,
                        archivePath + " → " + childName,
                        filename,
                        depth + 1,
                        maxDepth
                    );

                    if (found != null)
                        return found;
                }
                catch
                {
                    // A candidate filename hash may theoretically collide or
                    // the embedded data may not be a valid MIX. Ignore and continue.
                }
            }

            return null;
        }
    }
}
