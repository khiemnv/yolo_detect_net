using Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using WP_GUI;

namespace Services
{

    public class RCService
    {
        private Thread _t;
        public AsstModeless.MyHandler hideNG = null;
        public AsstModeless.MyHandler restart = null;
        public AsstModeless.MyHandler refresh = null;
        public AsstModeless.MyHandlerWithParam delayCapture = null;
        private Socket socket;
        private bool stop;

        public virtual void Init()
        {

        }
        public virtual void Start()
        {
            _t = CreateThread();
            stop = false;
            _t.Start();
        }

        public virtual void Stop()
        {
            Logger.Debug($"Stop {stop}");
            stop = true;
            socket?.Close();
            //socket?.Dispose();
            if (_t != null && _t.IsAlive)
            {
                _t.Join();
            }
            socket = null;
        }

        enum error_code
        {
            ec_invalid_cmd = -1,
            invalid_model = -2,
            download_config_error = -3,
            chg_runtime_error = -4,
            unknown = -9
        }

        private Thread CreateThread()
        {
            var t = new Thread((p) =>
            {
                try
                {
                    IPHostEntry ipHostInfo = Dns.GetHostEntryAsync(Environment.MachineName).Result;
                    IPAddress ipAddress = ipHostInfo.AddressList[0];
                    var endPoint = new IPEndPoint(ipAddress, 13);
                    socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    socket.Bind(endPoint);
                    socket.Listen(1000);
                    while (!stop)
                    {
                        try
                        {
                            var acceptedSocket = socket.Accept();
                            byte[] bytes = new byte[256];
                            var n = acceptedSocket.Receive(bytes);
                            var cmd = Encoding.UTF8.GetString(bytes, 0, n);
                            Logger.Debug($"cmd: [{cmd}]");

                            // execute cmd
                            int ret = ExecuteCmd(cmd);

                            // return result
                            byte[] msg = Encoding.UTF8.GetBytes($"{ret}");
                            acceptedSocket.Send(msg);
                            Logger.Debug($"result: {ret}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                }

                Logger.Debug("Thread stop");
            });
            return t;
        }

        protected int ExecuteCmd(string cmd)
        {
            var ret = (int)error_code.ec_invalid_cmd;
            if (Regex.IsMatch(cmd, @"^refresh", RegexOptions.IgnoreCase))
            {
                refresh?.Invoke();
                ret = 0;
            }
            else if (Regex.IsMatch(cmd, @"^restart", RegexOptions.IgnoreCase))
            {
                restart?.Invoke();
                ret = 0;
            }
            else if (Regex.IsMatch(cmd, @"^hideNG", RegexOptions.IgnoreCase))
            {
                hideNG?.Invoke();
                ret = 0;
            }
            else if (Regex.IsMatch(cmd, @"^runtime (\w+)", RegexOptions.IgnoreCase))
            {
                var rt = cmd.Split(' ')[1];
                var bOK = ProgramHelpers.SwRT(rt);
                ret = bOK ? 0 : (int)error_code.chg_runtime_error;
            }
            else if (Regex.IsMatch(cmd, @"^delayCapture [0-9]", RegexOptions.IgnoreCase))
            {
                var timeout = cmd.Split(' ')[1];
                delayCapture?.Invoke(int.Parse(timeout));
                ret = 0;
            }
            else if (Regex.IsMatch(cmd, @"^updateConfig (\w+)", RegexOptions.IgnoreCase))
            {
                var model = cmd.Split(' ')[1];
                ret = UpdateModel(model);
            }
            else if (Regex.IsMatch(cmd, @"^updateModelFromUsb", RegexOptions.IgnoreCase))
            {
                var model = cmd.Split(' ')[1];
                var path = @"E:\models";
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Removable)
                    {
                        path = System.IO.Path.Combine(drive.Name, "models");
                        break;
                    }
                }

                ret = 0;
                if (Directory.Exists(path))
                {
                    foreach (var sub in new DirectoryInfo(path).GetDirectories())
                    {
                        if (Regex.IsMatch(sub.Name, $@"model_{model}"))
                        {
                            // E:\models\model_7230B086XA
                            UpdateModelUsb(sub.FullName);
                            ret++;
                        }
                    }
                }

            }
            else if (Regex.IsMatch(cmd, @"^updateConfigFromUsb", RegexOptions.IgnoreCase))
            {
                var model = cmd.Split(' ')[1];
                var path = @"E:\models";
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Removable)
                    {
                        path = System.IO.Path.Combine(drive.Name, "models");
                        break;
                    }
                }

                ret = 0;
                if (Directory.Exists(path))
                {
                    foreach (var sub in new DirectoryInfo(path).GetDirectories())
                    {
                        if (Regex.IsMatch(sub.Name, $@"model_{model}"))
                        {
                            // E:\models\model_7230B086XA
                            UpdateConfigUsb(sub.FullName);
                            ret++;
                        }
                    }
                }

            }

            return ret;
        }

        int UpdateConfigUsb(string modelPath)
        {
            try
            {
                var m = Regex.Match(modelPath, @"model_(\w+)$");
                var model = m.Groups[1].Value;
                var des = $@"C:\deploy\doordetect\workingDir\model_{model}\sample_{model}\output";
                var src = $@"{modelPath}\sample_{model}\output";
                var files = new[]
                {
                    "back_01.xml",
                    "back_02.xml",
                    "dict.json",
                    "front_01.xml",
                    "front_02.xml",
                };
                foreach (var file in files)
                {
                    File.Copy(Path.Combine(src, file), Path.Combine(des, file), true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
            return 0;
        }
        public int UpdateModelUsb(string modelPath)
        {
            try
            {
                var m = Regex.Match(modelPath, @"model_(\w+)$");
                var model = m.Groups[1].Value;
                var des = $@"C:\deploy\doordetect\workingDir\model_{model}\sample_{model}\output";
                var src = $@"{modelPath}\sample_{model}\output";
                CleanDirR(des);
                CopyDirR(src, des);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
            return 0;
        }

        private void CopyDirR(string src, string des)
        {
            var rsrcd = new DirectoryInfo(src);
            var rdesd = new DirectoryInfo(des);
            CopyDirR(rsrcd, rdesd);

            void CopyDirR(DirectoryInfo srcd, DirectoryInfo desd)
            {
                foreach (var file in srcd.GetFiles())
                {
                    file.CopyTo(Path.Combine(des, file.Name), true);
                }
                foreach (var sub in srcd.GetDirectories())
                {
                    CopyDirR(sub, desd.CreateSubdirectory(sub.Name));
                }
            }
        }

        private void CleanDirR(string path)
        {
            var rd = new DirectoryInfo(path);
            CleanDirR(rd);

            void CleanDirR(DirectoryInfo d)
            {
                foreach (var file in d.GetFiles())
                {
                    file.Delete();
                }
                foreach (var sub in d.GetDirectories())
                {
                    CleanDirR(sub);
                    sub.Delete();
                }
            }
        }



        // __out: error code
        //   model is null
        //   download model config error
        //   not specified error
        public int UpdateModel(string model)
        {
            var cfg = DataClient.GetBaseConfig();
            var root = cfg.WorkingDir;
            var m = DataClient.GetDetectModelByBarCode(model);
            if (m != null && m.ConfigFileId != null)
            {
                return UpdateConfig(m, root);
            }
            return (int)error_code.invalid_model;
        }
        private int UpdateConfig(DetectModel m, string root)
        {
            try
            {
                var model = m.PartNumber;
                var sample_output = $"{root}\\model_{model}\\sample_{model}\\output";
                var cfgPath = $"{sample_output}\\{model}.config";
                var b = DataClient.DownloadFile(cfgPath, m.ConfigFileId);
                if (!b)
                {
                    Logger.Error($"DownloadFile {m.ConfigFileId} error!");
                    return (int)error_code.download_config_error;
                }

                var qt = File.ReadAllText(cfgPath).FromJson<QuarterTrim>();
                XmlExporter.CreateXmls(qt, sample_output);
                Logger.Debug($"{model} was updated!");
                return 0;
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                return (int)error_code.unknown;
            }
        }
    }
}
