using Models;
using System.Text;
using static Services.DataClient;

namespace Services
{
    internal static class GraphQL
    {

        public static T GraphQueryZ<T>(HttpClient client, string query, object variables)
        {
            try
            {
                BaseConfig baseConfig = GetBaseConfig();
                string contentString = (new { query, variables }).ToJson();
                var content = new StringContent(contentString, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseConfig.BaseUrl}/graphql");
                request.Headers.Add("Authorization", baseConfig.AccessToken);
                request.Content = content;

                var response = client.SendAsync(request).Result;

                var raw = "";
                if (response.IsSuccessStatusCode)
                {
                    raw = response.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    throw new Exception(response.StatusCode.ToString());
                }

                request.Dispose();
                content.Dispose();
                response.Dispose();

                return raw.FromJson<T>();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                throw;
            }
        }

        public static Models.Panel PostCreatePanel(HttpClient client, Models.Panel pn)
        {
            try
            {

                string type = "";
                switch (pn.Type)
                {
                    case PanelType.FIRST_BACK:
                        type = "FIRST_BACK";
                        break;
                    case PanelType.SECOND_BACK:
                        type = "SECOND_BACK";
                        break;
                    case PanelType.FIRST_FRONT:
                        type = "FIRST_FRONT";
                        break;
                    case PanelType.SECOND_FRONT:
                        type = "SECOND_FRONT";
                        break;
                }
                string query = $"mutation ($input:PanelInput!){{output:createPanel(input: $input){{id, type, quarterTrimId, beforeImgId, resultImgId}}}}";
                var variables = new { input = new { id = pn.Id, type, quarterTrimId = pn.QuarterTrimId, beforeImgId = pn.BeforeImgId, resultImgId = pn.ResultImgId } };

                var ret = GraphQueryZ<MutationResult<Models.Panel>>(client, query, variables);
                if (ret == null) { return null; }
                if (ret.Errors != null && ret.Errors.Count > 0)
                {
                    Logger.Error(ret.Errors[0].Message);
                }
                return ret.Data.Output;

            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return null;
            }
        }

        public static QuarterTrim PostCreateQuarterTrim(HttpClient client, QuarterTrim qt)
        {
            try
            {
                // barcode,detectModelId,createdDate,judge,
                string query = $"mutation ($input: QuarterTrimInput!){{output:createQuarterTrim(input:$input) {{id, no}} }}";
                var variables = new
                {
                    input = qt
                };
                var ret = GraphQueryZ<MutationResult<QuarterTrim>>(client, query, variables);
                if (ret.Errors != null)
                {
                    var msg = ret.Errors[0].Message;
                    var apiErr = msg.FromJson<ApiError>();
                    if (apiErr != null && apiErr.ErrorCode == "E101")
                    {
                        // if qrt was saved, return ok
                        return new QuarterTrim { Id = qt.Id };
                    }
                    else
                    {
                        Logger.Error($"{apiErr.ErrorMessage}");
                    }
                }
                return ret.Data.Output;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return null;
            }
        }
    }
}