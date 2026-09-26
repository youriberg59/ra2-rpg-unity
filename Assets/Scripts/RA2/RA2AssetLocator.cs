using System;
using System.Collections.Generic;
using System.IO;

namespace RA2RPG.RA2
{
    public static class RA2AssetLocator
    {
        private static readonly string[] NestedMixCandidates =
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
