using static Services.DataClient;

namespace Services
{
    internal static class RestApi
    {
        public static bool ApiPostCommand(HttpClient client, string jsonContent)
        {
            try
            {
                BaseConfig baseConfig = GetBaseConfig();
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseConfig.BaseUrl}/api/Commands");
                request.Headers.Add("Authorization", baseConfig.AccessToken);

                var content = new StringContent(jsonContent, null, "application/json");
                request.Content = content;

                var response = client.SendAsync(request).Result;

                var ret = response.IsSuccessStatusCode;
                if (!response.IsSuccessStatusCode)
                {
                    var txt = response.Content.ReadAsStringAsync().Result;
                    Logger.Error(txt);
                }

                request.Dispose();
                content.Dispose();
                response.Dispose();
                return ret;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
        }
    }
}