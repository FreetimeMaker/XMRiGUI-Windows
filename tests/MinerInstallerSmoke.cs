using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using XMRiGUI.Services;

var temp = Directory.CreateTempSubdirectory("xmrigui-test-");
try
{
    var destination = Path.Combine(temp.FullName, "miner", "xmrig.exe");
    using var buffer = new MemoryStream();
    using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
    {
        using var entry = archive.CreateEntry("xmrig-1.2.3/xmrig.exe").Open();
        entry.WriteByte(42);
    }
    var zip = buffer.ToArray();
    using var bad = new HttpClient(new ReleaseHandler(zip, new string('0', 64)));
    try
    {
        await MinerInstaller.InstallLatestAsync(bad, destination, CancellationToken.None);
        throw new Exception("A wrong SHA-256 digest was accepted.");
    }
    catch (InvalidDataException) when (!File.Exists(destination)) { }

    using var good = new HttpClient(new ReleaseHandler(zip, Convert.ToHexString(SHA256.HashData(zip))));
    var version = await MinerInstaller.InstallLatestAsync(good, destination, CancellationToken.None);
    if (version != "v1.2.3" || !File.ReadAllBytes(destination).SequenceEqual(new byte[] { 42 }) ||
        Directory.GetFiles(Path.GetDirectoryName(destination)!).Length != 1)
        throw new Exception("The expected executable was not staged and installed correctly.");
    Console.WriteLine("Miner installer smoke test passed using mocked GitHub responses (no executable launched).");
}
finally { temp.Delete(recursive: true); }

sealed class ReleaseHandler(byte[] zip, string digest) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpContent content = request.RequestUri!.AbsoluteUri switch
        {
            "https://api.github.com/repos/xmrig/xmrig/releases/latest" => new StringContent(JsonSerializer.Serialize(new
            {
                tag_name = "v1.2.3",
                assets = new[] { new { name = "xmrig-1.2.3-windows-x64.zip", digest = "sha256:" + digest, size = zip.Length } }
            })),
            "https://github.com/xmrig/xmrig/releases/download/v1.2.3/xmrig-1.2.3-windows-x64.zip" => new ByteArrayContent(zip),
            _ => throw new Exception("Unexpected download URL: " + request.RequestUri)
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
