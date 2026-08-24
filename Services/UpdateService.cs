using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace XMRiGUI.Services
{
    public class UpdateService
    {
        public const string IntegratedVersion = "1.8.1";
        private const string RepoUrl = "https://api.github.com/repos/FreetimeMaker/XMRiGUI/releases/latest";

        public async Task<string> CheckForUpdateAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // GitHub API requires a User-Agent
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("XMRiGUI-WPF", IntegratedVersion));

                    var response = await client.GetStringAsync(RepoUrl);
                    var json = JObject.Parse(response);

                    string latestVersion = json["tag_name"]?.ToString().TrimStart('v');

                    if (!string.IsNullOrEmpty(latestVersion) && IsNewerVersion(latestVersion, IntegratedVersion))
                    {
                        return latestVersion;
                    }
                }
            }
            catch { /* Silent fail for update check */ }

            return null;
        }

        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                Version vLatest = new Version(latest);
                Version vCurrent = new Version(current);
                return vLatest > vCurrent;
            }
            catch
            {
                return latest != current;
            }
        }
    }
}
