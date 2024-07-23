using Models;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Services
{
    public partial class DataClient
    {
        private static HttpClient client;
        private static BaseConfig _baseConfig;
        public static void InitClient(BaseConfig _cfg = null)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }
            };

            client?.Dispose();

            client = new HttpClient(handler);

            _baseConfig = _cfg;
        }
        public static void FinalClient()
        {
            client?.Dispose();
            client = null;

            _baseConfig = null;
        }

        public static BaseConfig GetBaseConfig()
        {
            if (_baseConfig != null) { return _baseConfig; }

            string root = Directory.GetCurrentDirectory();
            string configPath = Path.Combine(root, "appconfig.json");
            string jsonContent = System.IO.File.ReadAllText(configPath);
            BaseConfig baseConfig = jsonContent.FromJson<BaseConfig>();
            return baseConfig;
        }
        public static void WriteBaseConfig(BaseConfig baseConfig)
        {
            // convert baseConfig to json string
            string configJson = JsonConvert.SerializeObject(baseConfig, Formatting.Indented);
            string rootPath = Directory.GetCurrentDirectory();
            string configFilePath = Path.Combine(rootPath, "appconfig.json");

            // write data to file 
            File.WriteAllText(configFilePath, configJson);
        }
        public static void WriteBaseConfig(string key, object value)
        {
            try
            {
                var baseConfig = GetBaseConfig();
                baseConfig.GetType().GetProperty(key).SetValue(baseConfig, value, null);
                string configJson = JsonConvert.SerializeObject(baseConfig, Formatting.Indented);
                string rootPath = Directory.GetCurrentDirectory();
                string configFilePath = Path.Combine(rootPath, "appconfig.json");

                // write data to file 
                File.WriteAllText(configFilePath, configJson);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }
        public class LoginMessage
        {
            public string Token { get; set; }
        }
        public static bool CheckLogin(User userLogin)
        {
            var ret = false;
            try
            {
                string baseUrl = GetBaseConfig().BaseUrl;
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/users/login");
                string jsonContent = JsonConvert.SerializeObject(new
                {
                    userLogin.Account,
                    userLogin.Password
                });
                var content = new StringContent(jsonContent, null, "application/json");
                request.Content = content;
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    string reponseMess = response.Content.ReadAsStringAsync().Result;
                    var tokenValue = JsonConvert.DeserializeObject<LoginMessage>(reponseMess);
                    if (reponseMess != null)
                    {
                        //var a = tokenValue.Token;
                        BaseConfig baseConfig = GetBaseConfig();
                        baseConfig.AccessToken = "Bearer " + tokenValue.Token.Trim();
                        WriteBaseConfig(baseConfig);
                        ret = true;
                    }
                }

                request.Dispose();
                content.Dispose();
                response.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return ret;
        }

        public class BaseConfig
        {
            public string BaseUrl { get; set; } = "";
            public string AccessToken { get; set; } = "";
            public bool AsstModeless { get; set; } = false;
            public class CleanRule
            {
                public int FromNow { get; set; } = 30;
            };
            public CleanRule cleanRule = new CleanRule();
            public class DetectionSettingCls
            {
                public float Accuracy { get; set; } = 0.85f;
                public float Delta { get; set; } = 100;
            };
            public DetectionSettingCls DetectionSetting { get; set; } = new DetectionSettingCls();
            public bool TakeSampleMode { get; set; } = false;
            public int DelayCapture { get; set; } = 0;
            public bool DelayCaptureEnable { get; set; } = false;
            public bool PinNG { get; set; } = false;

            public PreprocessConfig PreprocessConfig { get; set; } = new PreprocessConfig();
            public string WorkingDir { get; set; } = "";
            public string UploadDir { get; set; } = "";
            public bool AutoLogin { get; set; } = false;
            public string ModelDetect { get; set; } = "";
            public string DeviceDetect { get; set; } = "";
            public bool SplitDetect { get; set; } = true;

            public bool ShowPercent { get; set; } = false;
            public bool FullScreen { get; set; } = true;

            public class UploadConfigCls
            {
                public bool Enable { get; set; } = false;
                public List<int> Retries { get; set; } = null;
                public bool UploadSync { get; set; } = false;
                public bool Subscribe { get; set; } = false;
                public int NgTimeout { get; set; } = 300; // 5*60 seconds
                public int UploadInterval { get; set; } = 2; // days
                public bool UploadRes { get; set; } = false; // upload result image
                public bool UseSqliteDb { get; set; } = true; // use sqlite or file
                public bool Upload1 { get; set; } = false;
            }
            public UploadConfigCls UploadConfig { get; set; } = new UploadConfigCls();

            public class UpdateConfigCls
            {
                public DateTimeOffset LastUpdatedDate { get; set; } = DateTimeOffset.Now;
                public int Timeout { get; set; } = 300; // 5*60 seconds
                public bool Enable { get; set; } = false;
            }
            public UpdateConfigCls UpdateConfig { get; set; } = new UpdateConfigCls();
            public bool EnableRC { get; set; } = false;
            public bool UseSignalR { get; set; } = false;
            public bool AutoRestart { get; set; } = false;
            public string RestartTime { get; set; }
            public bool AutoReconnectCamera { get; set; } = false;

            public BaseConfig() { }

        }
        public class PreprocessConfig
        {
            public string Mode { get; set; } = "none";
            public string DefaultModel { get; set; } = "model.onnx";
            public IEnumerable<int> CropLeft { get; set; }
            public IEnumerable<int> CropRight { get; set; }
        }
        public class QueryObject
        {
            public string Query { get; set; }
            public object Variables { get; set; }
        }


        // get detect model by barcode
        class QueryResult
        {
            public DataResult Data { get; set; }
        }
        class DataResult
        {
            public GetAllDetectModelsResult GetAllDetectModels { get; set; }
        }
        class GetAllDetectModelsResult
        {
            public List<DetectModel> Nodes { get; set; }
        }
        public static DetectModel GetDetectModelByBarCode(string bc)
        {
            try
            {
                BaseConfig baseConfig = GetBaseConfig();
                var queryObject = new QueryObject
                {
                    Query = "query GetAllDetectModels($bc:String!) { getAllDetectModels(where: { partNumber: { eq: $bc } }) { nodes { id partNumber modelFileId configFileId modifiedDate } } }",
                    Variables = new { bc }
                };
                string json = queryObject.ToJson();
                var content = new StringContent(json, null, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseConfig.BaseUrl}/graphql");
                request.Headers.Add("Authorization", baseConfig.AccessToken);
                request.Content = content;
                var response = client.SendAsync(request).Result;
                DetectModel ret = null;

                if (response.IsSuccessStatusCode)
                {
                    string raw = response.Content.ReadAsStringAsync().Result;
                    var obj = raw.FromJson<QueryResult>();

                    if (obj.Data != null
                        && obj.Data.GetAllDetectModels != null
                        && obj.Data.GetAllDetectModels.Nodes != null
                        && obj.Data.GetAllDetectModels.Nodes.Count > 0)
                    {
                        ret = obj.Data.GetAllDetectModels.Nodes[0];
                    }
                }

                request.Dispose();
                content.Dispose();
                return ret;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return null;
            }
        }

#if false
        public static bool PostCreateFiles(FileEntity files)
        {
            string query = $"mutation ($data:FileInput!) {{ file:createFile(input: $data) {{ id, path }} }}";
            var variables = new { data = new { id = files.Id, path = files.Path } };
            return GraphQL.GraphQueryZ(query, variables);
        }

        public class CreateNotificationData
        {
            public Notification Data { get; set; }
        }
        public static bool SendNotify(QuarterTrim qt)
        {
            string jsonDescription = JsonConvert.SerializeObject(qt);
            string query = $"mutation CreateNotification($data:NotificationInput!){{createNotification(input: $data) {{id,title,description}}}}";
            CreateNotificationData variables = new CreateNotificationData
            {
                Data = new Notification
                {
                    title = "OnQuarterTrimAdded",
                    description = jsonDescription
                }
            };
            return GraphQueryZ( query, variables);
        }
        public static bool SendNotify(BarcodeCounter bc)
        {
            string query = $"mutation CreateNotification($data:NotificationInput!){{createNotification(input: $data) {{id,title,description}}}}";
            CreateNotificationData variables = new CreateNotificationData
            {
                Data = new Notification
                {
                    title = "OnBarcodeCounterAdded",
                    description = bc.ToJson()
                }
            };
            return GraphQueryZ(query, variables);
        }
#endif
        /// data: {
        ///   output: {
        ///     T
        ///   }
        /// }
        public class MutationResult<T>
        {
            public MutationData<T> Data { get; set; } = null;
            public List<QtError> Errors { get; set; } = null;
        }
        public class MutationData<T>
        {
            public T Output { get; set; } = default;
        }


        /// data: {
        ///   output: {
        ///     nodes: [
        ///       {T},
        ///     ]
        ///   }
        /// }
        public class QueryResult<T>
        {
            public QueryData<T> Data { get; set; } = null;
            public List<QtError> Errors { get; set; } = null;
        }
        public class QtError
        {
            public string Message { get; set; }
        }
        public class QueryData<T>
        {
            public QueryOutput<T> Output { get; set; } = default;
        }
        public class QueryOutput<T>
        {
            public List<T> Nodes { get; set; } = null;
        }
        public static QuarterTrim PostCreateQuarterTrimBare(QuarterTrim qt)
        {
            try
            {
                string query = $"mutation ($input: QuarterTrimInput!){{output:createQuarterTrim(input:$input) {{id,barcode,detectModelId,createdDate,judge}} }}";
                var variables = new
                {
                    input = new QuarterTrim
                    {
                        Id = qt.Id,
                        Barcode = qt.Barcode,
                        CreatedDate = qt.CreatedDate,
                        DetectModelId = qt.DetectModelId,
                        Fixed = qt.Fixed,
                        Judge = qt.Judge,
                    }
                };
                var ret = GraphQL.GraphQueryZ<MutationResult<QuarterTrim>>(client, query, variables);
                return ret.Data.Output;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return null;
            }

        }

        public static QuarterTrim PostCreateQuarterTrim(QuarterTrim qt)
        {
            return GraphQL.PostCreateQuarterTrim(client, qt);
        }

        public static Models.Panel PostCreatePanel(Models.Panel panel)
        {
            return GraphQL.PostCreatePanel(client, panel);
        }
        private static bool IsQuerySuccess(HttpResponseMessage response)
        {
            var ret = false;
            if (response.IsSuccessStatusCode)
            {
                string raw = response.Content.ReadAsStringAsync().Result;
                dynamic obj = JsonConvert.DeserializeObject<dynamic>(raw);
                ret = (obj.data != null);
            }

            return ret;
        }

        class OutputClass<T> where T : class
        {
            public T Output { get; set; }
        }
        class DataClass<T> where T : class
        {
            public OutputClass<T> Data { get; set; }
        }

        public static Part PostCreatePart(Part pa)
        {
            string query = $"mutation ($input:PartInput!){{output:createPart(input: $input){{id,name,pos{{x,y,width,height}},judge,percent,panelId}}}}";
            var variables = new { input = new { id = pa.Id, name = pa.Name, pos = new { x = pa.PosX, y = pa.PosY, width = pa.PosW, height = pa.PosH }, judge = pa.Judge, percent = pa.Percent, panelId = pa.PanelId } };
            return GraphQL.GraphQueryZ<MutationResult<Part>>(client, query, variables).Data.Output;
        }

        public static DetectModel CreateDetectModelAsync(DetectModel model)
        {
            try
            {
                BaseConfig baseConfig = GetBaseConfig();
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseConfig.BaseUrl}/api/detectmodels");
                request.Headers.Add("Authorization", baseConfig.AccessToken);
                var text = model.ToJson();
                var content = new StringContent(text, null, "application/json");
                request.Content = content;
                var response = client.SendAsync(request).Result;

                DetectModel ret = null;
                if (response.IsSuccessStatusCode)
                {
                    var jsResult = response.Content.ReadAsStringAsync().Result;
                    var responseData = jsResult.FromJson<DetectModel>();
                    ret = responseData;
                }

                request.Dispose();
                content.Dispose();
                response.Dispose();
                return ret;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return null;
            }
        }

        // return error code
        public static FileEntity UploadFileAsync(FileEntity file, string prefix = "")
        {
            try
            {
                if (!File.Exists(file.Path))
                {
                    throw new Exception($"File not exist: {file.Path}");
                }

                BaseConfig baseConfig = GetBaseConfig();
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseConfig.BaseUrl}/api/files/upload");
                request.Headers.Add("Authorization", baseConfig.AccessToken);
                var content = new MultipartFormDataContent
                {
                    { new StreamContent(File.OpenRead(file.Path)), "file", prefix + Path.GetFileName(file.Path) },
                    { new StringContent(file.Id), "id" }
                };
                request.Content = content;
                var response = client.SendAsync(request).Result;

                FileEntity ret = null;
                if (response.IsSuccessStatusCode)
                {
                    // send ok
                    var jsResult = response.Content.ReadAsStringAsync().Result;
                    var responseData = jsResult.FromJson<FileEntity>();
                    ret = responseData;

                    // check parse result
                    Debug.Assert(ret != null);
                }
                else
                {
                    // send error
                    var jsResult = response.Content.ReadAsStringAsync().Result;
                    var apiErr = jsResult.FromJson<ApiError>();
                    if (apiErr?.ErrorCode == "E001")
                    {
                        ret = new FileEntity { Id = file.Id };
                    }
                    else
                    {
                        Logger.Error(apiErr.ErrorMessage);
                    }
                }

                request.Dispose();
                content.Dispose();
                response.Dispose();
                return ret;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return null;
            }
        }

        public static BarcodeCounter PostCreateBarcodeCounter(BarcodeCounter bc)
        {
            string query = "mutation ( $input:BarcodeCounterInput!) { output:createBarcodeCounter(input:$input) { id, barcode, createdDate}}";
            var variables = new { input = bc };
            var ret = GraphQL.GraphQueryZ<MutationResult<BarcodeCounter>>(client, query, variables);
            if (ret.Errors != null)
            {
                var msg = ret.Errors[0].Message;
                var apiErr = msg.FromJson<ApiError>();
                if (apiErr != null && apiErr.ErrorCode == "E201")
                {
                    // if bc was saved, return ok
                    return new BarcodeCounter { Id = bc.Id };
                }
                else
                {
                    Logger.Error(apiErr.ErrorMessage);
                }
            }
            return ret.Data.Output;
        }

        public static DetectModel PostCreateDetectModel(DetectModel data)
        {
            string query = "mutation ( $input:DetectModelInput!) { output:createDetectModel(input:$input) { id, partNumber, modelFileId}}";
            var variables = new { input = data };
            var ret = GraphQL.GraphQueryZ<MutationResult<DetectModel>>(client, query, variables);
            return ret.Data.Output;
        }
        public static DetectModel PostUpdateDetectModel(DetectModel data)
        {
            try
            {
                BaseConfig baseConfig = GetBaseConfig();
                var request = new HttpRequestMessage(HttpMethod.Put, $"{baseConfig.BaseUrl}/api/detectmodels/{data.Id}");
                request.Headers.Add("Authorization", baseConfig.AccessToken);
                var json = data.ToJson();
                var content = new StringContent(json, null, "application/json");
                request.Content = content;
                var response = client.SendAsync(request).Result;

                DetectModel ret = null;
                if (response.IsSuccessStatusCode)
                {
                    var jsResult = response.Content.ReadAsStringAsync().Result;
                    var responseData = jsResult.FromJson<DetectModel>();
                    ret = responseData;
                }

                request.Dispose();
                content.Dispose();
                response.Dispose();
                return ret;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return null;
            }
        }

        public static bool DownloadFile(string path, string fileId)
        {
            var baseConfig = GetBaseConfig();
            var request = new HttpRequestMessage(HttpMethod.Get, baseConfig.BaseUrl + $"/api/files/{fileId}/download");
            request.Headers.Add("Authorization", baseConfig.AccessToken);
            var response = client.SendAsync(request).Result;

            var ret = false;
            if (response.IsSuccessStatusCode)
            {
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    response.Content.CopyToAsync(fs).Wait();
                }
                ret = true;
            }

            request.Dispose();
            response.Dispose();
            return ret;
        }
        public static bool DownloadZ(DetectModel model, string outputDir)
        {
            try
            {
                string modelName = model.PartNumber;
                string fileId = model.ModelFileId;

                string extractPath = outputDir;
                string zip = $@"{extractPath}\model_{modelName}.zip";
                CreateOrCleanDir(extractPath);

                // down load & extract
                var ret = DownloadFile(zip, fileId);
                if (!ret)
                {
                    throw new Exception("download file error!");
                }
                ZipFile.ExtractToDirectory(zip, extractPath);

                // clean
                File.Delete(zip);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
        }

        private static void CreateOrCleanDir(string extractPath)
        {
            if (!Directory.Exists(extractPath))
            {
                Directory.CreateDirectory(extractPath);
            }
            else
            {
                // clean
                var di = new DirectoryInfo(extractPath);
                foreach (FileInfo file in di.EnumerateFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.EnumerateDirectories())
                {
                    dir.Delete(true);
                }
            }
        }

        public static IEnumerable<T> GetAll<T>()
        {
            var name = typeof(T).Name.ToLower() + "s";
            switch (typeof(T).Name)
            {
                case nameof(FileEntity):
                    name = "files";
                    break;
            }
            var baseConfig = GetBaseConfig();
            var request = new HttpRequestMessage(HttpMethod.Get, baseConfig.BaseUrl + $"/api/{name}");
            request.Headers.Add("Authorization", baseConfig.AccessToken);
            var response = client.SendAsync(request).Result;

            List<T> ret = null;
            if (response.IsSuccessStatusCode)
            {
                var js = response.Content.ReadAsStringAsync().Result;
                ret = js.FromJson<List<T>>();
            }

            request.Dispose();
            response.Dispose();
            return ret;
        }
        public static bool CheckConnection()
        {
            bool ret = false;
            var client = new TcpClient();
            try
            {
                var m = Regex.Match(GetBaseConfig().BaseUrl, @"^(https://|http://)?(.*):(\d+)$");
                client.Connect(m.Groups[2].Value, int.Parse(m.Groups[3].Value));
                //Logger.Debug("Connection open, host active");
                ret = true;
            }
            catch (SocketException ex)
            {
                Logger.Error("Connection could not be established due to: " + ex.Message);
            }
            finally
            {
                client.Close();
            }

            return ret;
        }

        internal static bool ApiPostCommand(string jsonContent)
        {
            return RestApi.ApiPostCommand(client, jsonContent);
        }

        internal static IEnumerable<DetectModel> SelectModifiedModelsByDate(DateTimeOffset lastUpdate)
        {
            string query = "query($input:DateTime){output:getAllDetectModels(where:{modifiedDate:{gte:$input}}){nodes{id partNumber configFileId modelFileId dictFileId modifiedDate}}}";
            var variables = new { input = lastUpdate };
            var ret = GraphQL.GraphQueryZ<QueryResult<DetectModel>>(client, query, variables);
            if (ret.Errors != null)
            {
                var msg = ret.Errors[0].Message;
                Logger.Error(msg);
                return null;
            }
            return ret.Data.Output.Nodes;
        }
    }
}
