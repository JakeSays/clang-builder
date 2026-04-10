namespace Std.BuildTools.Clang;

public class FileDownloader : IDisposable
{
    private readonly HttpClient _httpClient = new();

    public async Task DownloadFile(string url, string destinationPath, string label)
    {
        if (File.Exists(destinationPath))
        {
            Log.Info($"{label}: Already downloaded, skipping.");
            return;
        }

        Log.Info(LogColor.Green, $"{label}: Downloading from {url}...");
        await using var s = await _httpClient.GetStreamAsync(url);
        await using var fs = new FileStream(destinationPath, FileMode.CreateNew);
        await s.CopyToAsync(fs);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
