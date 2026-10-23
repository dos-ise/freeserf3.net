/*
 * ManifestParser.cs - Parsing of Ubisoft Connect game manifests
 *
 * Part of the Ubisoft Connect download feature for freeserf3.net.
 * Ported from freeserf.net (GPL v3).
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Mg.Protocol.Download;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Freeserf3.Ubisoft
{
    public static class ManifestParser
    {
        // The manifest file starts with a 356 byte header followed by a
        // zlib-compressed protobuf Manifest message.
        const int ManifestHeaderSize = 356;

        public static Manifest Parse(byte[] manifestBytes)
        {
            if (manifestBytes.Length <= ManifestHeaderSize)
                throw new UbisoftException("Manifest file is too short.");

            using (var input = new MemoryStream(manifestBytes))
            {
                input.Seek(ManifestHeaderSize, SeekOrigin.Begin);
                using (var decompressor = new ZLibStream(input, CompressionMode.Decompress))
                {
                    return Manifest.Parser.ParseFrom(decompressor);
                }
            }
        }

        // Finds the file entry with the given name (case-insensitive) in the
        // manifest and returns its slices. Matches the exact name or a path
        // ending with it (e.g. "loca/SPAE.PA").
        public static List<Slice>? FindFileSlices(Manifest manifest, string fileName)
        {
            foreach (var chunk in manifest.Chunks)
            {
                foreach (var file in chunk.Files)
                {
                    string name = file.Name ?? "";
                    if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith("\\" + fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new List<Slice>(file.SliceList);
                    }
                }
            }

            return null;
        }

        // Finds all file entries whose path starts with one of the given
        // directory prefixes (case-insensitive), e.g. "GFX", "Map", "SND".
        public static List<ManifestFile> FindFilesByDirectory(Manifest manifest, params string[] directoryPrefixes)
        {
            var result = new List<ManifestFile>();
            foreach (var chunk in manifest.Chunks)
            {
                foreach (var file in chunk.Files)
                {
                    string name = file.Name ?? "";
                    foreach (var prefix in directoryPrefixes)
                    {
                        if (name.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(name, prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(new ManifestFile(name, new List<Slice>(file.SliceList)));
                            break;
                        }
                    }
                }
            }
            return result;
        }

        // Builds the relative slice paths used to request download URLs.
        // Version 3 manifests use a sub-directory layout.
        public static List<string> GetSlicePaths(Manifest manifest, List<Slice> slices)
        {
            var paths = new List<string>(slices.Count);
            uint version = manifest.Version;

            foreach (var slice in slices)
            {
                // The CDN paths use uppercase hex (matching the reference
                // UplayKit implementation); lowercase returns 404.
                string sliceId = Convert.ToHexString(slice.DownloadSha1.ToByteArray());
                if (version == 3)
                {
                    char dir = FormatSliceHashChar(sliceId);
                    paths.Add($"slices_v3/{dir}/{sliceId}");
                }
                else
                {
                    paths.Add($"slices/{sliceId}");
                }
            }

            return paths;
        }

        // Computes the sub-directory character for version 3 slice paths.
        static char FormatSliceHashChar(string sliceId)
        {
            const string base32 = "0123456789abcdefghijklmnopqrstuv";
            byte reversedValue = byte.Parse($"{sliceId[1]}{sliceId[0]}", System.Globalization.NumberStyles.HexNumber);
            int offset = (int)Math.Floor((decimal)reversedValue / 16);
            int halfOffset = reversedValue % 2 == 0 ? 0 : 16;
            return base32[offset + halfOffset];
        }
    }

    // A file entry from the manifest with its slices.
    public class ManifestFile
    {
        public string Name { get; }
        public List<Slice> Slices { get; }

        public ManifestFile(string name, List<Slice> slices)
        {
            Name = name;
            Slices = slices;
        }
    }
}
