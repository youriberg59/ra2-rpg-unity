using System;
using System.Collections.Generic;
using System.IO;

namespace RA2RPG.RA2
{
    /// <summary>
    /// Builds one in-memory hash index for all top-level and known nested RA2 MIX archives.
    /// Asset lookups after construction are O(1) and do not reopen every archive.
    /// </summary>
    public sealed class RA2ArchiveIndex
    {
        private static readonly string[] NestedArchiveNames =
        {
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

        private readonly Dictionary<uint, string> firstPathByHash =
            new Dictionary<uint, string>();

        public int IndexedEntries { get; private set; }
        public int OpenedArchives { get; private set; }
        public int UniqueHashes => firstPathByHash.Count;

        public static RA2ArchiveIndex Build(
            string localRa2Directory,
            RA2AssetLocator.SearchTrace trace = null,
            int maxDepth = 3
        )
        {
            if (string.IsNullOrWhiteSpace(localRa2Directory) ||
                !Directory.Exists(localRa2Directory))
                throw new DirectoryNotFoundException(localRa2Directory);

            var index = new RA2ArchiveIndex();

            var roots = new List<string>();
            roots.AddRange(Directory.GetFiles(
                localRa2Directory,
                "*.mix",
                SearchOption.AllDirectories
            ));

            roots.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string rootPath in roots)
            {
                try
                {
                    using var root = new MixArchive(rootPath);
                    string relative = Path.GetRelativePath(localRa2Directory, rootPath);
                    index.IndexArchive(root, relative, 0, maxDepth, trace);
                }
                catch (Exception ex)
                {
                    trace?.Add($"INDEX SKIP {Path.GetFileName(rootPath)} : {ex.Message}");
                }
            }

            return index;
        }

        public bool Contains(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return false;

            return firstPathByHash.ContainsKey(
                WestwoodCrc32.HashFilename(filename)
            );
        }

        public string FindPath(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            firstPathByHash.TryGetValue(
                WestwoodCrc32.HashFilename(filename),
                out string path
            );

            return path == null ? null : path + " → " + filename.ToUpperInvariant();
        }

        private void IndexArchive(
            MixArchive archive,
            string archivePath,
            int depth,
            int maxDepth,
            RA2AssetLocator.SearchTrace trace
        )
        {
            OpenedArchives++;
            trace?.Add($"INDEX {archivePath} : {archive.EntryCount} entries");

            foreach (var entry in archive.Entries)
            {
                IndexedEntries++;

                if (!firstPathByHash.ContainsKey(entry.Hash))
                    firstPathByHash[entry.Hash] = archivePath;
            }

            if (depth >= maxDepth)
                return;

            foreach (string nestedName in NestedArchiveNames)
            {
                if (!archive.TryGetEntry(nestedName, out var nestedEntry))
                    continue;

                try
                {
                    byte[] bytes = archive.ReadEntry(nestedEntry, nestedName);
                    using var nested = new MixArchive(bytes, nestedName);

                    IndexArchive(
                        nested,
                        archivePath + " → " + nestedName,
                        depth + 1,
                        maxDepth,
                        trace
                    );
                }
                catch (Exception ex)
                {
                    trace?.Add(
                        $"INDEX SKIP {archivePath} → {nestedName} : {ex.Message}"
                    );
                }
            }
        }
    }
}
