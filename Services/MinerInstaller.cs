using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XMRiGUI.Services
{
    internal static class MinerInstaller
    {
        private const int MaxDownloadSize = 32 * 1024 * 1024;
        private const string LatestRelease = "https://api.github.com/repos/xmrig/xmrig/releases/latest";
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60), MaxResponseContentBufferSize = MaxDownloadSize };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XMRiGUI");
            return client;
        }

        public static Task<string> InstallLatestAsync(string destination, CancellationToken cancellation) =>
            InstallLatestAsync(Client, destination, cancellation);

        internal static async Task<string> InstallLatestAsync(HttpClient client, string destination, CancellationToken cancellation)
        {
            using var release = JsonDocument.Parse(await client.GetByteArrayAsync(LatestRelease, cancellation).ConfigureAwait(false));
            var tag = release.RootElement.GetProperty("tag_name").GetString();
            if (!Regex.IsMatch(tag ?? "", @"\Av[0-9]+\.[0-9]+\.[0-9]+\z"))
                throw new InvalidDataException("GitHub returned an unsupported XMRig release tag.");

            var assetName = $"xmrig-{tag.Substring(1)}-windows-x64.zip";
            var asset = release.RootElement.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(entry => entry.GetProperty("name").GetString() == assetName);
            if (asset.ValueKind == JsonValueKind.Undefined)
                throw new InvalidDataException("The latest XMRig release has no Windows x64 ZIP.");
            var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
            if (!Regex.IsMatch(digest ?? "", @"\Asha256:[0-9a-fA-F]{64}\z"))
                throw new InvalidDataException("The XMRig ZIP has no verifiable SHA-256 digest.");
            var size = asset.GetProperty("size").GetInt32();
            if (size <= 0 || size > MaxDownloadSize) throw new InvalidDataException("Unexpected XMRig ZIP size.");
            var zipUrl = $"https://github.com/xmrig/xmrig/releases/download/{tag}/{assetName}";
            using var response = await client.GetAsync(zipUrl, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var zip = await response.Content.ReadAsByteArrayAsync(cancellation).ConfigureAwait(false);
            if (zip.Length != size || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(zip), Convert.FromHexString(digest.Substring(7))))
                throw new InvalidDataException("The XMRig ZIP failed its SHA-256 or size check.");

            using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            var executables = archive.Entries.Where(entry => entry.Name.Equals("xmrig.exe", StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (executables.Length != 1 || executables[0].Length <= 0 || executables[0].Length > MaxDownloadSize)
                throw new InvalidDataException("The ZIP does not contain exactly one valid-sized xmrig.exe.");
            var directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            var staging = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                using (var input = executables[0].Open())
                    await input.CopyToAsync(output, cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                try { File.Move(staging, destination); }
                catch (IOException) when (File.Exists(destination)) { }
                return tag;
            }
            finally
            {
                File.Delete(staging);
            }
        }
    }
}
