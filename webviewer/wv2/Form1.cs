using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Models;
using Repositories;
using System.Text.Json;
using Services;
using System.ComponentModel;
using WP_GUI;

using static Services.DataClient;
using System.Diagnostics;

namespace wv2
{
    public partial class Form1 : Form
    {
        private WebView2 webView;
        static readonly List<string> strImgList = new List<string>();// paths of image input
        private static AsstModeless asst;

        // backgroud
        private static BackgroundWorker bg;
        private delegate object WorkCallback(object arg);
        private delegate void CompleteCallback(object result);
        public Form1()
        {
            InitializeComponent();
            webView = new WebView2();
            //webView.Source = new Uri("https://www.microsoft.com");
            //webView.Source = new Uri(Path.GetFullPath("app.html"));
            //webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            webView.Dock = DockStyle.Fill;

            var btn = new Button();
            btn.Text = "sent";
            btn.Click += Btn_Click;

            var tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Fill;
            //tbl.Controls.Add(btn, 0, 0);
            tbl.Controls.Add(webView, 0, 1);
            Controls.Add(tbl);

            InitializeAsync();

            var baseCfg = GetBaseConfig();

            // tool modeless
            asst = new AsstModeless
            {
                sentCmdHandler = EvalCmd,
                //refreshHandler = RefreshGUI,
                //cropRefreshHandler = CropRefresh(),
                //swapCamerahHandler = SwapCamera(),
                //exportXml = ExportXml,
                //uploadModel = UploadModel,
                //uploadConfig = UploadConfig,
            };

            asst.SetProgressBar("Idle", 0);
            if (baseCfg.AsstModeless)
            {
                asst.Show();
            }
            asst.FormClosing += (s, e) => { asst.Hide(); e.Cancel = true; };

            // load config
            ProgramHelpers.LoadConfig();

            // load crop model
            CreatePreprocess(baseCfg);

            /*----start pushing data thread----*/
            if (!baseCfg.UploadConfig.Enable)
            {
                ProgramHelpers.srv = new UploadServiceStub(ProgramHelpers.cfg.root);
            }
            else if (baseCfg.UploadConfig.UseSqliteDb)
            {
                ProgramHelpers.srv = new UploadService3(ProgramHelpers.cfg.root);
            }
            else
            {
                ProgramHelpers.srv = new UploadService2(ProgramHelpers.cfg.root);
            }
            ProgramHelpers.srv.OnDisconnected = (s, e) => Invoke((MethodInvoker)delegate
            {
                EvalCmd($"execCmd(\"disconnected\", {e.IsConnected});");
            });
            ProgramHelpers.srv.OnRestart = (s, e) => Invoke((MethodInvoker)delegate
            {
                RestartCb(this);
            });
            ProgramHelpers.srv.Start();


            Size = new System.Drawing.Size(1250, 680);
            StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += new FormClosingEventHandler(OnFormClosing);

            if (GetBaseConfig().FullScreen)
            {
                // enter fullscreen
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
            }
        }
        void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (ProgramHelpers.srv.IsAlive())
            {
                object w(object arg)
                {
                    try
                    {
                        Logger.Debug("StopAndWait");
                        ProgramHelpers.srv.StopAndWait();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.Message);
                        return false;
                    }
                }
                void c(object result)
                {
                    ((Form)sender).Close();
                }
                BgExecute(w, c);
                e.Cancel = true;
            }
            Logger.Debug($"FormClosing {e.Cancel}");
            return;
        }

        void RestartCb(Form f)
        {
            if (bg != null) { WaitForBgComplete(); }

            // run script to install new version
            var path = Path.GetFullPath("restart.bat");
            ProcessStartInfo startInfo = new ProcessStartInfo(path)
            {
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = Path.GetDirectoryName(path)
            };
            Process.Start(startInfo);

            //Environment.Exit(0);
            f.Close();
        }
        void CreatePreprocess(BaseConfig baseCfg)
        {
            if (baseCfg.PreprocessConfig.Mode == "auto")
            {
                ProgramHelpers.preprocess = new PreprocessAuto(Path.Combine(baseCfg.WorkingDir, baseCfg.PreprocessConfig.DefaultModel));
            }
            else
            {
                ProgramHelpers.preprocess = new PreprocessManual(baseCfg.PreprocessConfig.CropLeft, baseCfg.PreprocessConfig.CropRight);
            }
        }

        private void Btn_Click(object? sender, EventArgs e)
        {
            var uri = new Uri(Path.GetFullPath("assets/ConnectCamera.html"));
            webView.CoreWebView2.Navigate(uri.AbsoluteUri);
            //webView.CoreWebView2.Navigate("https://www.microsoft.com");
            //webView.CoreWebView2.ExecuteScriptAsync($"notify('{uri} is not safe, try an https link')");
        }

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            //webView.CoreWebView2.Navigate("https://www.microsoft.com");
        }

        async void InitializeAsync()
        {


            var environment = await CoreWebView2Environment.CreateAsync();
            await webView.EnsureCoreWebView2Async(environment);

            // Map the local folder to a virtual host name
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets.example", Path.GetFullPath("assets"),
                CoreWebView2HostResourceAccessKind.Allow);

            await webView.EnsureCoreWebView2Async(null);
            webView.CoreWebView2.WebMessageReceived += MsgHandler;
            // CoreWebView2PermissionKind Camera
            var settings = webView.CoreWebView2.Settings;
            settings.AreHostObjectsAllowed = false;
            settings.IsWebMessageEnabled = true;
            settings.IsScriptEnabled = true; // Enable only if necessary
            webView.CoreWebView2.PermissionRequested += HandlePermissionRequested;

            // Load content using the virtual URL
            webView.Source = new Uri("https://appassets.example/ConnectCamera.html");



            //await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.postMessage(window.document.URL);");
            //await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.addEventListener(\'message\', event => alert(event.data));");

        }

        private void HandlePermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            if (e.PermissionKind == CoreWebView2PermissionKind.Camera)
            {
                e.State = CoreWebView2PermissionState.Allow;
            }
        }

        private void MsgHandler(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            String json = args.TryGetWebMessageAsString();
            //webView.CoreWebView2.ExecuteScriptAsync($"notify('{uri} is not safe, try an https link')");

            var msg = JsonSerializer.Deserialize<Msg>(json);
            switch (msg?.funcName)
            {
                case "OnState":
                    if (msg.data == "script_loaded")
                    {

                        EvalCmd("receiveLoginFromNet(\"OK\");");
                        //EvalCmd("handleBarcode(\"7230B086XA\");");
                        //EvalCmd("startCamera_in();");
                    }
                    break;
                case "devices":
                    break;
                case "BarCode":
                    if (msg.data == null) { throw new Exception("invalid data"); }
                    GetBarCode(msg.data);
                    break;
                case "Capture_Image":
                    if (msg.data == null) { throw new Exception("invalid data"); }
                    OnCapture_Image(msg.data);
                    break;
            }
        }
        private void EvalCmd(string cmd)
        {
            try
            {
                webView.CoreWebView2.ExecuteScriptAsync(cmd);
            }
            catch (Exception ex)
            {
                Logger.Error($"exec [{cmd}] error [{ex.Message}]");
            }
        }

        enum WorkState
        {
            init,
            scanbarcode,
            loadmodel,
            captureImg12,
            saveImg12,
            captureImg34,
        }
        private WorkState _workState = WorkState.init;
        private void GetBarCode(string barcode)
        {
            if (_workState != WorkState.init)
            {
                Logger.Error($"Invalid state {_workState}");
                return;
            }
            _workState = WorkState.scanbarcode;

            var sectionId = RepositoryHelper.NewId();
            object w(object arg)
            {
                Logger.Debug($"GetBarCode start {barcode}");
                // reciever barcode
                // send barcode for reciever quatertrim -> detectmodel info
                // change path model detect and path save images input
                ProgramHelpers.InitSession(sectionId);

                // barcode = "7230B313XA";
                barcode = barcode.ToUpper();

                // send barcode to web app
                var bc = new BarcodeCounter
                {
                    Id = sectionId,
                    Barcode = barcode
                };
                bool takeSampleMode = DataClient.GetBaseConfig().TakeSampleMode;
                if (!takeSampleMode)
                {
                    bool b0 = ProgramHelpers.srv.Add(bc);
                    bool b1 = ProgramHelpers.srv.InfoScaned(bc);
                }
                else
                {
                    strImgList.Clear();
                }

                // skip load model
                if (takeSampleMode)
                {
                    ProgramHelpers.Qt1.Barcode = barcode;
                    return true;
                }

                // fetch model
                // NOTE: update config.model & detectModel
                var isOk = ProgramHelpers.LoadModel(barcode);
                return isOk;
            }
            void c(object isOk)
            {
                // take sample mode
                if (GetBaseConfig().TakeSampleMode)
                {
                    string result = "OK";
                    var imgDir = Path.GetFullPath(@"lib\Html_Render\img");
                    var f1 = $@"{imgDir}\{ProgramHelpers.Qt1.Barcode}\front.png";
                    var f2 = $@"{imgDir}\{ProgramHelpers.Qt1.Barcode}\back.png";
                    if (!File.Exists(f1))
                    {
                        // default img
                        f1 = $@"{imgDir}\DoorFront.png";
                    }
                    if (!File.Exists(f2))
                    {
                        // default img
                        f2 = $@"{imgDir}\DoorBack.png";
                    }

                    string frontBase64 = Convert.ToBase64String(File.ReadAllBytes(f1));
                    string backBase64 = Convert.ToBase64String(File.ReadAllBytes(f2));
                    string cmd = $"receiveResultBarCode('{result}','data:image/png;base64,{frontBase64}','data:image/png;base64,{backBase64}');";
                    EvalCmd(cmd);
                    _workState = WorkState.loadmodel;
                    return;
                }

                // normal mode
                if (!(bool)isOk)
                {
                    // send command to javascript for return to begin screen
                    string result = "NG";
                    string cmd = $"receiveResultBarCode('{result}');";
                    EvalCmd(cmd);
                    _workState = WorkState.init;
                }
                else
                {
                    string result = "OK";
                    string frontBase64 = Convert.ToBase64String(File.ReadAllBytes($@"{ProgramHelpers.cfg.SampleOutDir}\front.png"));
                    string backBase64 = Convert.ToBase64String(File.ReadAllBytes($@"{ProgramHelpers.cfg.SampleOutDir}\back.png"));
                    string cmd = $"receiveResultBarCode('{result}','data:image/png;base64,{frontBase64}','data:image/png;base64,{backBase64}');";
                    EvalCmd(cmd);
                    _workState = WorkState.loadmodel;
                }

                Logger.Debug($"GetBarCode end {sectionId} {isOk}");
            }
            BgExecute(w, c);
        }

        void OnCapture_Image(string s)
        {
            if (_workState == WorkState.loadmodel)
            {
                if (strImgList.Count != 0)
                {
                    Logger.Error($"Invalid state {_workState}");
                    return;
                }
                _workState = WorkState.captureImg12;
            }
            else if (_workState == WorkState.saveImg12)
            {
                if (strImgList.Count != 2)
                {
                    Logger.Error($"Invalid state {_workState}");
                    return;
                }
                _workState = WorkState.captureImg34;
            }
            else
            {
                Logger.Error($"Invalid state {_workState}");
                return;
            }

            try
            {
                object w(object arg)
                {
                    Logger.Debug($"OnCapture_Image start");

                    //receive image from js and decode to an image 
                    List<string> pathCaptureImage = SaveDataZ(s);
                    var baseCfg = GetBaseConfig();
                    bool splitDetect = baseCfg.SplitDetect;
                    bool takeSampleMode = baseCfg.TakeSampleMode;
                    if (takeSampleMode)
                    {
                        if (strImgList.Count == 4) { return true; }
                        return null;
                    }

                    // normal mode
                    if (strImgList.Count == 4)
                    {
                        var ci3 = new FileEntity { Path = strImgList[2] };
                        var ci4 = new FileEntity { Path = strImgList[3] };
                        ProgramHelpers.Qt1.Panels.ElementAt(2).BeforeImg = ci3;
                        ProgramHelpers.Qt1.Panels.ElementAt(3).BeforeImg = ci4;
                        ProgramHelpers.srv.UploadFile(ci3);
                        ProgramHelpers.srv.UploadFile(ci4);
                        if (splitDetect)
                        {
                            var lst = new List<(string, PanelHelpers.ImgListIndex)> {
                                (strImgList[2], PanelHelpers.ImgListIndex.secondLeft ),
                                (strImgList[3], PanelHelpers.ImgListIndex.secondRight ),
                            };
                            ProgramHelpers.StartDetectZ(lst);
                        }
                        else
                        {
                            var lst = new List<(string, PanelHelpers.ImgListIndex)> {
                                (strImgList[0], PanelHelpers.ImgListIndex.firstLeft ),
                                (strImgList[1], PanelHelpers.ImgListIndex.firstRight ),
                                (strImgList[2], PanelHelpers.ImgListIndex.secondLeft ),
                                (strImgList[3], PanelHelpers.ImgListIndex.secondRight ),
                            };
                            ProgramHelpers.StartDetectZ(lst);
                        }

                        // upload result
                        var sw = Stopwatch.StartNew();
                        ProgramHelpers.UploadResult();
                        sw.Stop();
                        //Logger.Debug($"UploadResult completed! {sw.ElapsedMilliseconds} ms");

                        return ProgramHelpers.Qt1.Judge;
                    }
                    else
                    {
                        var ci1 = new FileEntity { Path = strImgList[0] };
                        var ci2 = new FileEntity { Path = strImgList[1] };
                        ProgramHelpers.Qt1.Panels.ElementAt(0).BeforeImg = ci1;
                        ProgramHelpers.Qt1.Panels.ElementAt(1).BeforeImg = ci2;
                        ProgramHelpers.srv.UploadFile(ci1);
                        ProgramHelpers.srv.UploadFile(ci2);

                        if (splitDetect)
                        {
                            var lst = new List<(string, PanelHelpers.ImgListIndex)> {
                                (strImgList[0], PanelHelpers.ImgListIndex.firstLeft ),
                                (strImgList[1], PanelHelpers.ImgListIndex.firstRight ),
                            };
                            ProgramHelpers.StartDetectZ(lst);
                        }
                    }
                    return null;
                }
                void c(object result)
                {
                    // info img recived
                    EvalCmd($"execCmd(\"img_received\", {strImgList.Count});");

                    // info detect result
                    if (result != null)
                    {
                        // normal mode
                        var sumRs = (bool)result;
                        EvalCmd("receiveResultFromNet(\'" + (sumRs ? "OK" : "NG") + "\');");
                        strImgList.Clear();
                        //imgQueue.Clear();
                        _workState = WorkState.init;
                    }
                    else
                    {
                        // capture mode
                        asst.SetProgressBar($"{strImgList.Count} imgs", 1);
                        _workState = WorkState.saveImg12;
                    }

                    Logger.Debug($"OnCapture_Image end");
                }
                BgExecute(w, c);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }

        private List<string> SaveDataZ(string s)
        {
            try
            {
                // [image/jpeg],[data]@[image/jpeg],[data]
                string[] arr = s.Split(new char[] { ',', '@' });

                byte[] bytesLeft = Convert.FromBase64String(arr[1]);
                byte[] bytesRight = Convert.FromBase64String(arr[3]);

                strImgList.Add(DecodeBase64toImage(bytesLeft, $"{strImgList.Count + 1}"));
                strImgList.Add(DecodeBase64toImage(bytesRight, $"{strImgList.Count + 1}"));
                return strImgList;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }

            //return pathPicturePre;
            return null;
        }

        private static string DecodeBase64toImage(byte[] bytes, string n)
        {
            string path;
            if (GetBaseConfig().TakeSampleMode)
            {
                path = $@"{ProgramHelpers.cfg.CaptureImage}_{ProgramHelpers.Qt1.Barcode}\{ProgramHelpers.Qt1.Id}_{n}.jpeg";
            }
            else
            {
                path = $@"{ProgramHelpers.cfg.CaptureDir}\input\{ProgramHelpers.Qt1.Id}\ci_{n}.jpeg";
            }

            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.IO.File.WriteAllBytes(path, bytes);
            return path;
        }

        // show spin => do word => hide spin
        class BgParam
        {
            public WorkCallback w;
            public CompleteCallback c;
            public object r;
        }
        private void BgExecute(WorkCallback w, CompleteCallback c)
        {
            // init bg
            if (bg == null)
            {
                var myTimer = new System.Windows.Forms.Timer() { Interval = 1000 };
                int alarmCounter = 0;
                myTimer.Tick += (s, e) =>
                {
                    alarmCounter++;
                    asst.SetProgressBar($"Elapsed {alarmCounter}s", 1);
                };
                bg = new BackgroundWorker()
                {
                    WorkerReportsProgress = true,
                    WorkerSupportsCancellation = true,
                };
                bg.DoWork += (object sender, DoWorkEventArgs e) =>
                {
                    bg.ReportProgress(0);

                    // start work
                    var param = (BgParam)e.Argument;
                    param.r = param.w(e.Argument);
                    e.Result = param;
                };
                bg.ProgressChanged += (s, e) =>
                {
                    // start timer
                    myTimer.Start();
                    alarmCounter = 0;
                    asst.SetProgressBar($"Elapsed {alarmCounter}s", 1);

                    // show spin
                    EvalCmd("execCmd(\"spin_show\", {showHide:1});");
                };
                bg.RunWorkerCompleted += (object sender, RunWorkerCompletedEventArgs e) =>
                {
                    // stop timer & pogress
                    myTimer.Stop();
                    asst.SetProgressBar($"Done {alarmCounter}s", 0);

                    // hide spin
                    EvalCmd("execCmd(\"spin_show\", {showHide:0})");

                    // show result
                    var param = (BgParam)e.Result;
                    param.c(param.r);
                };
            }

            // start detect in bg
            WaitForBgComplete();
            bg.RunWorkerAsync(new BgParam { w = w, c = c });
        }
        private static void WaitForBgComplete()
        {
            int i = 0;
            while (bg.IsBusy)
            {
                asst.SetProgressBar($"Waiting ${i}s", 1);
                Thread.Sleep(1000);
                Application.DoEvents();
                i++;
                Logger.Debug($"WaitForBgComplete {i}s");
            }
        }
    }

    class Msg
    {
        public string? funcName { get; set; }
        public string? data { get; set; }
    }
}