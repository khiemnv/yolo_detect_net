using Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace WP_GUI
{
    public partial class AsstModeless : Form
    {
        private readonly StatusStrip progressStatusStrip = new StatusStrip();
        private readonly ToolStripProgressBar toolStripProgressBar = new ToolStripProgressBar();
        private readonly ToolStripStatusLabel toolStripStatusLabel = new ToolStripStatusLabel();
        readonly Macro commands = new Macro();
        class PbAndNode
        {
            public PictureBox pb;
            internal string img;
        }
        readonly Dictionary<int, PbAndNode> _dictPAN = new Dictionary<int, PbAndNode> {
            {1, new PbAndNode() },
            {2, new PbAndNode() },
            {3, new PbAndNode() },
            {4, new PbAndNode() },
        };

        class Macro : IEnumerable<MacroCmd>
        {
            private readonly List<MacroCmd> commands = new List<MacroCmd>();
            private Stopwatch sw;

            public void Clear()
            {
                commands.Clear();
                sw = Stopwatch.StartNew();
            }
            public void Add(string cmd)
            {
                commands.Add(new MacroCmd(cmd, (int)sw.ElapsedMilliseconds));
                sw.Restart();
            }
            public void RemoveAt(int idx)
            {
                commands.RemoveAt(idx);
            }

            public IEnumerator<MacroCmd> GetEnumerator()
            {
                return commands.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return commands.GetEnumerator();
            }
        }
        class MacroCmd
        {
            public string cmd;
            public int ms; // ms
            public MacroCmd(string cmd, int ms)
            {
                this.cmd = cmd;
                this.ms = ms;
            }
        };
        public AsstModeless()
        {
            //InitializeComponent();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(207, 485);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AsstModeless";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "AsstModeless";
            this.TopMost = true;


            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            var signinfl = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight };
            var lbt = new Button { Text = "login" };
            var us = new TextBox { Text = "admin" };
            var pw = new TextBox { Text = "123456" };
            lbt.Click += (e, s) =>
            {
                string cmd = $"sendDataToNet(\"CheckLogin\", \"{us.Text}@{pw.Text}@false\");";
                sentCmdHandler?.Invoke(cmd);
            };
            signinfl.Controls.AddRange(new Control[]
            {
                us,
                pw,
                lbt
            });
            signinfl.Dock = DockStyle.Fill;

            var fl = new FlowLayoutPanel() { FlowDirection = FlowDirection.LeftToRight };
            var bc = new TextBox()
            {
                Text = "7230B086XA"
            };
            var sbc = new Button() { Text = "Sent" };
            void bcClickHandler(object s, EventArgs e)
            {
                string cmd = "handleBarcode(\'" + bc.Text + "\');";
                if (recFlag) { commands.Add(cmd); }
                sentCmdHandler(cmd);
            }
            //macro.Add(bcClickHandler);
            sbc.Click += bcClickHandler;
            fl.Controls.Add(bc);
            fl.Controls.Add(sbc);
            fl.Dock = DockStyle.Fill;

            // img
            var imfl = new FlowLayoutPanel();
            var img1 = new PictureBox() { SizeMode = PictureBoxSizeMode.StretchImage, Width = 80, Height = 60, BorderStyle = BorderStyle.FixedSingle };
            img1.Click += SelectImg(img1);
            img1.DragEnter += Form1_DragEnter;
            img1.DragDrop += Form1_DragDrop;
            _dictPAN[1].pb = img1;
            var img2 = new PictureBox() { SizeMode = PictureBoxSizeMode.StretchImage, Width = 80, Height = 60, BorderStyle = BorderStyle.FixedSingle };
            img2.Click += SelectImg(img2);
            img2.DragEnter += Form1_DragEnter;
            img2.DragDrop += Form1_DragDrop;
            _dictPAN[2].pb = img2;
            var img3 = new PictureBox() { SizeMode = PictureBoxSizeMode.StretchImage, Width = 80, Height = 60, BorderStyle = BorderStyle.FixedSingle };
            img3.Click += SelectImg(img3);
            img3.DragEnter += Form1_DragEnter;
            img3.DragDrop += Form1_DragDrop;
            _dictPAN[3].pb = img3;
            var img4 = new PictureBox() { SizeMode = PictureBoxSizeMode.StretchImage, Width = 80, Height = 60, BorderStyle = BorderStyle.FixedSingle };
            img4.Click += SelectImg(img4);
            img4.DragEnter += Form1_DragEnter;
            img4.DragDrop += Form1_DragDrop;
            _dictPAN[4].pb = img4;
            var setbtn = new Button() { Text = "Set" };
            var sendbtn = new Button() { Text = "Send" };
            setbtn.Click += OnSetImg(img1, img2);
            //macro.Add(OnSetImg(img1, img2));
            sendbtn.Click += SentCmd("sendImage(0);");
            //macro.Add(SentCmd("sendImg();"));
            imfl.Controls.AddRange(new Control[] { img1, img2, setbtn, sendbtn });
            imfl.Dock = DockStyle.Fill;

            var imfl2 = new FlowLayoutPanel();
            var setbtn2 = new Button() { Text = "Set" };
            var sendbtn2 = new Button() { Text = "Send" };
            setbtn2.Click += OnSetImg(img3, img4);
            sendbtn2.Click += SentCmd("sendImage(0);");
            //macro.Add(SentCmd("sendImg();"));
            imfl2.Controls.AddRange(new Control[] { img3, img4, setbtn2, sendbtn2 });
            imfl2.Dock = DockStyle.Fill;

            // macro
            var mcfl = new FlowLayoutPanel();
            var mcR = new Button() { Text = "Rec" };
            var mcS = new Button() { Text = "Start" };
            mcR.Click += (s, e) =>
            {
                switch (mcR.Text)
                {
                    case "Rec":
                        commands.Clear();
                        //commands.Add("");
                        mcR.Text = "Save";
                        mcS.Enabled = false;
                        recFlag = true;
                        break;
                    case "Save":
                        commands.Add("");
                        //commands.RemoveAt(0);
                        mcR.Text = "Rec";
                        mcS.Enabled = true;
                        recFlag = false;
                        break;
                }
            };
            var bg = new BackgroundWorker
            {
                WorkerReportsProgress = true,

            };
            var stopFlg = true;
            bg.DoWork += (s, e) =>
            {
                while (!stopFlg)
                {
                    var t = 0;
                    var total = commands.Sum(c => c.ms);
                    foreach (var command in commands)
                    {
                        if (stopFlg) { break; }
                        Thread.Sleep(command.ms);
                        if (!string.IsNullOrEmpty(command.cmd))
                        {
                            bg.ReportProgress(t / total, command.cmd);
                        }
                        t += command.ms;
                    }
                };
            };
            bg.ProgressChanged += (s, e) =>
            {
                sentCmdHandler?.Invoke(e.UserState as string);
            };
            mcS.Click += (s, e) =>
            {

                switch (mcS.Text)
                {
                    case "Start":
                        stopFlg = false;
                        bg.RunWorkerAsync();
                        mcS.Text = "Stop";
                        break;
                    case "Stop":
                        stopFlg = true;
                        mcS.Text = "Stopping...";
                        mcS.Enabled = false;
                        while (bg.IsBusy)
                        {
                            Thread.Sleep(100);
                            Application.DoEvents();
                        }
                        mcS.Text = "Start";
                        mcS.Enabled = true;
                        break;
                }
            };
            mcfl.Controls.AddRange(new Control[] { mcR, mcS });

            var tbl = new TableLayoutPanel() { RowCount = 6, Dock = DockStyle.Fill };
            tbl.RowStyles.Add(new RowStyle() { Height = 60, SizeType = SizeType.Absolute });
            tbl.RowStyles.Add(new RowStyle() { Height = 40, SizeType = SizeType.Absolute });
            tbl.RowStyles.Add(new RowStyle() { Height = 100, SizeType = SizeType.Absolute });
            tbl.RowStyles.Add(new RowStyle() { Height = 100, SizeType = SizeType.Absolute });
            tbl.RowStyles.Add(new RowStyle() { Height = 100, SizeType = SizeType.Absolute });
            tbl.Controls.Add(signinfl, 0, 0);
            tbl.Controls.Add(fl, 0, 1);
            tbl.Controls.Add(imfl, 0, 2);
            tbl.Controls.Add(imfl2, 0, 3);
            tbl.Controls.Add(mcfl, 0, 6);

            var cmdtxt = new TextBox()
            {
                Text = "handleCapture();",
                Multiline = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                WordWrap = true,
                Dock = DockStyle.Fill
            };
            var ebt = new Button { Text = "Enter" };
            void enterClickHandler(object s, EventArgs e)
            {
                if (recFlag) { commands.Add(cmdtxt.Text); }
                sentCmdHandler(cmdtxt.Text);
            }

            ebt.Click += enterClickHandler;
            //macro.Add(enterClickHandler);
            tbl.Controls.Add(cmdtxt, 0, 4);
            tbl.Controls.Add(ebt, 0, 5);

            // progress bar
            Application.EnableVisualStyles();
            toolStripStatusLabel.Overflow = ToolStripItemOverflow.Always;
            progressStatusStrip.Items.Add(toolStripProgressBar);
            progressStatusStrip.Items.Add(toolStripStatusLabel);

            // menu
            var fileContextMenuStrip = new ContextMenuStrip();
            fileContextMenuStrip.Items.Add("Refresh").Click += OnRefresh();
            //fileContextMenuStrip.Items.Add("Macro").Click += OnMacro();
            fileContextMenuStrip.Items.Add("CropRefresh").Click += OnCropRefresh();
            fileContextMenuStrip.Items.Add("SwapCamera").Click += OnSwapCamera();
            var tsmiTakeSampleMode = new ToolStripMenuItem
            {
                Text = "TakeSampleMode",
                Checked = DataClient.GetBaseConfig().TakeSampleMode,
                CheckOnClick = true
            };
            tsmiTakeSampleMode.CheckedChanged += (s, e) =>
            {
                var b = tsmiTakeSampleMode.Checked;
                var baseConfig = DataClient.GetBaseConfig();
                baseConfig.TakeSampleMode = b;
                DataClient.WriteBaseConfig(baseConfig);
                RestartApp();
            };
            fileContextMenuStrip.Items.Add(tsmiTakeSampleMode);

            // delayCapture
            var delayCapture = new ToolStripMenuItem
            {
                Text = "DelayCapture",
                CheckOnClick = true,
                Checked = DataClient.GetBaseConfig().DelayCaptureEnable,
            };
            delayCapture.CheckedChanged += (s, e) =>
            {
                var b = delayCapture.Checked;
                if (b)
                {
                    var n = DataClient.GetBaseConfig().DelayCapture;
                    var cmd = $"execCmd(\"delay_capture\", {n});"; //execCmd(\"delay_capture\", 5);
                    sentCmdHandler?.Invoke(cmd);
                }
                else
                {
                    var cmd = $"execCmd(\"delay_capture\", 0);";
                    sentCmdHandler?.Invoke(cmd);
                }
                DataClient.WriteBaseConfig(nameof(DataClient.BaseConfig.DelayCaptureEnable), b);
            };
            fileContextMenuStrip.Items.Add(delayCapture);

            // collapse
            var collapse = new ToolStripMenuItem("Collapse")
            {
                CheckOnClick = true
            };
            fileContextMenuStrip.Items.Add(collapse);
            collapse.CheckedChanged += (s, e) =>
            {
                var show = !collapse.Checked;
                imfl.Visible = show;
                imfl2.Visible = show;
                if (!show)
                {
                    tbl.RowStyles[2].Height = 0;
                    tbl.RowStyles[3].Height = 0;
                    this.Height -= 200;
                }
                else
                {
                    tbl.RowStyles[2].Height = 100;
                    tbl.RowStyles[3].Height = 100;
                    this.Height += 200;
                }
            };

            // export xml
            var exportXmlMi = new ToolStripMenuItem("ExportXml");
            exportXmlMi.Click += (s, e) =>
            {
                exportXml?.Invoke();
            };
            fileContextMenuStrip.Items.Add(exportXmlMi);

            // upload model
            fileContextMenuStrip.Items.Add("UploadModel").Click += (s, e) => uploadModel?.Invoke();
            fileContextMenuStrip.Items.Add("UploadConfig").Click += (s, e) => uploadConfig?.Invoke();

            fileContextMenuStrip.Items.Add("-");
            fileContextMenuStrip.Items.Add("Exit").Click += OnExit();
            var file = new ToolStripMenuItem("File")
            {
                DropDown = fileContextMenuStrip
            };
            var ms = new MenuStrip();
            ms.Items.Add(file);

            Controls.Add(tbl);
            Controls.Add(ms);
            Controls.Add(progressStatusStrip);

            //var adminTool = new Form { Text = "Modeless Windows Forms", ClientSize = new Size(200, 400) };
            //adminTool.SuspendLayout();
            //adminTool.Controls.Add(tbl);


            //adminTool.Controls.Add(ms);
            //adminTool.ResumeLayout();
            //adminTool.Show();
            //Application.Run(adminTool);
            //return;
            this.KeyPreview = true;
            this.PreviewKeyDown += (s, e) => { Debug.WriteLine($"{e.KeyData}"); };


            this.Load += (s, e) =>
            {
                if (DataClient.GetBaseConfig().TakeSampleMode)
                {
                    collapse.PerformClick();
                }
            };
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Move;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var reg = new Regex(@"(^|_)([1-4])(_|$)");
            foreach (string file in files)
            {
                var fname = Path.GetFileNameWithoutExtension(file);
                var m = reg.Match(fname);
                if (m.Success)
                {
                    var i = int.Parse(m.Groups[2].Value);
                    _dictPAN[i].img = file;
                    _dictPAN[i].pb.Image = ImageExtensions.CloneFromFile(file);
                }
            }
        }

        private EventHandler OnSetImg(PictureBox img1, PictureBox img2)
        {
            return (s, e) =>
            {
                try
                {
                    if (img1.Image == null) { return; }
                    string cmd1 = $"setImg(\"data:image/jpeg;base64,{GetBase64(img1)}\",\"data:image/jpeg;base64,{GetBase64(img2)}\");";
                    if (recFlag) { commands.Add(cmd1); }
                    sentCmdHandler?.Invoke(cmd1);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                }
            };
        }

        private EventHandler SelectImg(PictureBox img1)
        {
            return (e, s) =>
            {
                OpenFileDialog ofd = new OpenFileDialog();
                var res = ofd.ShowDialog();
                if (res == DialogResult.OK)
                {
                    img1.Image = Image.FromFile(ofd.FileName);
                }
            };
        }
        private string GetBase64(PictureBox pictureBox1)
        {
            MemoryStream ms = new System.IO.MemoryStream();
            pictureBox1.Image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            var base64 = Convert.ToBase64String(ms.ToArray());
            ms.Dispose();
            return base64;
        }

        public delegate void SentCmdHandler(string cmd);
        public SentCmdHandler sentCmdHandler = null;
        private EventHandler SentCmd(string cmd)
        {
            return (e, s) =>
            {
                if (recFlag) { commands.Add(cmd); }
                sentCmdHandler?.Invoke(cmd);
            };
        }

        private EventHandler OnRefresh()
        {
            return (e, s) =>
            {
                refreshHandler?.Invoke();
            };
        }
        private EventHandler OnCropRefresh()
        {
            return (e, s) =>
            {
                cropRefreshHandler?.Invoke();
            };
        }
        private EventHandler OnSwapCamera()
        {
            return (e, s) =>
            {
                swapCamerahHandler?.Invoke();
            };
        }
        private EventHandler OnExit()
        {
            return (e, s) =>
            {
                exitHandler?.Invoke();
            };
        }
        public delegate void MyHandler();
        public delegate object MyHandlerWithParam(object obj);
        public MyHandler refreshHandler = null;
        public MyHandler cropRefreshHandler = null;
        public MyHandler exitHandler = null;
        public MyHandler swapCamerahHandler = null;
        public MyHandler restartApp = null;
        public MyHandler exportXml = null;
        public MyHandler uploadModel = null;
        public MyHandler uploadConfig = null;
        private bool recFlag;

        public void SetProgressBar(string text, int value)
        {
            //Logger.Debug($"{text} {value}");
            if (value != 0)
            {
                toolStripProgressBar.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                toolStripProgressBar.Style = ProgressBarStyle.Blocks;
            }
            //toolStripProgressBar.Value = value;
            toolStripStatusLabel.Text = text;
        }


        private bool GetFilename(out string filename, DragEventArgs e)
        {
            bool ret = false;
            filename = String.Empty;
            if ((e.AllowedEffect & DragDropEffects.Copy) == DragDropEffects.Copy)
            {
                if (((IDataObject)e.Data).GetData("FileDrop") is Array data)
                {
                    if ((data.Length == 1) && (data.GetValue(0) is String))
                    {
                        filename = ((string[])data)[0];
                        string ext = Path.GetExtension(filename).ToLower();
                        if ((ext == ".jpg") || (ext == ".png") || (ext == ".bmp"))
                        {
                            ret = true;
                        }
                    }
                }
            }
            return ret;
        }
        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            var validData = GetFilename(out _, e);
            if (validData)
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
                e.Effect = DragDropEffects.None;
        }
        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            var pictureBox1 = (PictureBox)sender;
            if (e.Effect != DragDropEffects.Copy) return;

            var validData = GetFilename(out string filename, e);
            if (validData)
            {
                pictureBox1.Image = ImageExtensions.CloneFromFile(filename);
            }
        }

        private void RestartApp()
        {
            restartApp?.Invoke();

        }
    }
}
