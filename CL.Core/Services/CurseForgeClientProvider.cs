using CurseForge.APIClient;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public static class CurseForgeClientProvider
    {
        private static ApiClient _cfApiClientInstance;

        public static async Task<ApiClient> GetClientAsync()
        {
            if (_cfApiClientInstance != null) return _cfApiClientInstance;

            try
            {
                if (!WebHelper.Client.DefaultRequestHeaders.Contains("x-launcher-secret"))
                {
                    WebHelper.Client.DefaultRequestHeaders.Add("x-launcher-secret", "CL-Super-Secret-2026");
                }

                var response = await WebHelper.Client.GetAsync($"{Secrets.CurseForgeKey}");
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    string key = data["key"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(key))
                    {
                        _cfApiClientInstance = new ApiClient(key);
                        return _cfApiClientInstance;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"CurseForge Backend returned: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error init CF Client: {ex.Message}");
            }

            return null;
        }
    }
}
