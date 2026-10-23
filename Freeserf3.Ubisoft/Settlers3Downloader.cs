/*
 * Settlers3Downloader.cs - Downloads the Settlers 3 game data from Ubisoft Connect
 *
 * Part of the Ubisoft Connect download feature for freeserf3.net.
 * Ported from freeserf.net (GPL v3). Downloads the data directories needed
 * by the game (GFX, Map, SND, Theme) - not the whole game. Requires the
 * user to own "The Settlers 3 - History Edition" (product id 11784) on
 * their Ubisoft Connect account.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Mg.Protocol.Download;
using Mg.Protocol.Ownership;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ZstdSharp;

namespace Freeserf3.Ubisoft
{
    public class Settlers3Downloader
    {
        // "The Settlers 3 - History Edition" on Ubisoft Connect.
        public const uint Settlers3HistoryEditionProductId = 11784;

        // Data directories needed by the game (relative to the install root).
        public static readonly string[] DataDirectories = { "GFX", "Map", "SND", "Theme" };

        static readonly HttpClient httpClient = CreateHttpClient();

        public delegate void ProgressCallback(string status, int percent);

        readonly UbisoftLoginResponse login;
        readonly string targetDirectory;

        public Settlers3Downloader(UbisoftLoginResponse login, string targetDirectory)
        {
            this.login = login;
            this.targetDirectory = targetDirectory;
        }

        static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            return client;
        }

        // Runs the whole download flow. Returns the number of files written.
        public async Task<int> DownloadAsync(ProgressCallback progress, CancellationToken cancellationToken = default)
        {
            // 1. Connect to the demux socket and authenticate
            progress?.Invoke("Connecting...", 5);
            using (var demux = new DemuxClient())
            {
                await demux.ConnectAsync(cancellationToken).ConfigureAwait(false);
                bool authenticated = await demux.AuthenticateAsync(login.Ticket!, cancellationToken).ConfigureAwait(false);
                if (!authenticated)
                    throw new UbisoftException("Authentication failed.");

                // 2. Check ownership and get the ownership token
                progress?.Invoke("Checking ownership...", 10);
                var ownership = new OwnershipService(demux);
                await ownership.ConnectAsync(cancellationToken).ConfigureAwait(false);
                var ownedGames = await ownership.GetOwnedGamesAsync(login.Ticket!, login.SessionId!, cancellationToken).ConfigureAwait(false);

                OwnedGame? settlers = null;
                foreach (var game in ownedGames)
                {
                    if (game.ProductId == Settlers3HistoryEditionProductId)
                    {
                        settlers = game;
                        break;
                    }
                }

                if (settlers == null)
                    throw new UbisoftException("You do not own The Settlers 3 - History Edition on this account.");

                if (string.IsNullOrEmpty(settlers.LatestManifest))
                    throw new UbisoftException("No manifest available for this game.");

                var (ownershipToken, _) = await ownership.GetOwnershipTokenAsync(Settlers3HistoryEditionProductId,
                    login.Ticket!, login.SessionId!, cancellationToken).ConfigureAwait(false);

                // 3. Initialize the download service
                progress?.Invoke("Initializing download...", 15);
                var download = new DownloadService(demux);
                await download.ConnectAsync(cancellationToken).ConfigureAwait(false);
                bool initialized = await download.InitializeAsync(ownershipToken, cancellationToken).ConfigureAwait(false);
                if (!initialized)
                    throw new UbisoftException("Download initialization failed.");

                // 4. Download and parse the manifest
                progress?.Invoke("Downloading manifest...", 20);
                byte[] manifestBytes = await DownloadManifestAsync(download, settlers.LatestManifest, cancellationToken).ConfigureAwait(false);
                var manifest = ManifestParser.Parse(manifestBytes);

                // 5. Find the data files
                var files = ManifestParser.FindFilesByDirectory(manifest, DataDirectories);
                if (files.Count == 0)
                    throw new UbisoftException("No game data files found in the manifest.");

                // 6. Download all files
                int totalSlices = 0;
                foreach (var file in files)
                    totalSlices += file.Slices.Count;

                int written = 0;
                int sliceIndex = 0;
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Manifest paths may use "/" or "\" as separators.
                    string relative = file.Name.Replace('\\', '/');
                    string targetPath = Path.Combine(targetDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                    var slicePaths = ManifestParser.GetSlicePaths(manifest, file.Slices);
                    var urls = await download.GetUrlsAsync(Settlers3HistoryEditionProductId, slicePaths, cancellationToken).ConfigureAwait(false);
                    if (urls.Count != file.Slices.Count)
                        throw new UbisoftException("Download URL count mismatch.");

                    using (var output = System.IO.File.Create(targetPath))
                    {
                        for (int i = 0; i < file.Slices.Count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            int percent = 20 + (int)(80.0 * sliceIndex / totalSlices);
                            progress?.Invoke($"Downloading {file.Name} ({i + 1}/{file.Slices.Count})...", percent);

                            byte[] sliceBytes = await DownloadSliceAsync(urls[i], cancellationToken).ConfigureAwait(false);
                            byte[] decompressed = DecompressSlice(manifest, sliceBytes, file.Slices[i].Size);
                            output.Write(decompressed, 0, decompressed.Length);
                            sliceIndex++;
                        }
                    }

                    written++;
                }

                progress?.Invoke($"Download complete: {written} files.", 100);
                return written;
            }
        }

        async Task<byte[]> DownloadManifestAsync(DownloadService download, string manifestId, CancellationToken cancellationToken)
        {
            var urls = await download.GetUrlsAsync(Settlers3HistoryEditionProductId,
                new List<string> { $"manifests/{manifestId}.manifest" }, cancellationToken).ConfigureAwait(false);
            if (urls.Count == 0 || urls[0].Count == 0)
                throw new UbisoftException("No manifest download URL.");

            return await DownloadFromUrlsAsync(urls[0], cancellationToken).ConfigureAwait(false);
        }

        async Task<byte[]> DownloadSliceAsync(List<string> urls, CancellationToken cancellationToken)
        {
            return await DownloadFromUrlsAsync(urls, cancellationToken).ConfigureAwait(false);
        }

        // Downloads from the first URL that works (the server provides
        // multiple mirror URLs per file).
        async Task<byte[]> DownloadFromUrlsAsync(List<string> urls, CancellationToken cancellationToken)
        {
            Exception? lastError = null;
            foreach (var url in urls)
            {
                try
                {
                    using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw new UbisoftException($"Download failed: {lastError?.Message ?? "Unknown error."}");
        }

        static byte[] DecompressSlice(Manifest manifest, byte[] sliceBytes, ulong outputSize)
        {
            if (!manifest.IsCompressed)
                return sliceBytes;

            string method = manifest.CompressionMethod.ToString().ToLowerInvariant();
            if (method.StartsWith("compressionmethod_"))
                method = method.Substring("compressionmethod_".Length);

            switch (method)
            {
                case "zstd":
                    using (var decompressor = new Decompressor())
                    {
                        return decompressor.Unwrap(sliceBytes).ToArray();
                    }
                case "deflate":
                    using (var input = new MemoryStream(sliceBytes))
                    using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
                    using (var output = new MemoryStream((int)outputSize))
                    {
                        zlib.CopyTo(output);
                        return output.ToArray();
                    }
                case "lzham":
                    throw new UbisoftException("LZHAM compression is not supported.");
                default:
                    throw new UbisoftException($"Unknown compression method: {method}");
            }
        }
    }
}
