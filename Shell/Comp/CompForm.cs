using annotation;
using ClosedXML.Excel;
using Extensions;
using LabelMe;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static annotation.BgWrk;
using static comp.CompExtensions;
using static comp.CompModels;

namespace comp
{
    public partial class CompForm : Form
    {
        CompShellEnv env = new CompShellEnv();
        CompModels cmp => env._cmp;
        private WorkingDir wkDir => env._wkDir;
        private string RootDir => wkDir.path;
        private CompNode rootNode;
        private TreeView tree;

        private StatusStrip progressStatusStrip = new StatusStrip();
        private ToolStripProgressBar toolStripProgressBar = new ToolStripProgressBar();
        private ToolStripStatusLabel toolStripStatusLabel = new ToolStripStatusLabel();
        private ProPictureBox pb;

        public CompForm(WorkingDir workingDir = null)
        {
            InitializeComponent();

            // wkdir
            env._wkDir = workingDir ?? new WorkingDir();

            Load += OnLoad;
        }

        private const string FORM_NAME = "CompareTools";

        private void OnLoad(object _sender, EventArgs _e)
        {
            InitGUI();

            // subscribe
            tree.KeyUp += OnKeyUp;

            if (Directory.Exists(wkDir.test))
            {
                SetRootDir(wkDir.path);
            }
        }
        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyData)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Back:
                    OnChangeSelectedNode((CompNode)tree.SelectedNode.Tag);
                    break;
            }
        }
        private void InitGUI()
        {
            InitTree();

            // split
            var spt = new SplitContainer();
            spt.Dock = DockStyle.Fill;
            spt.Panel1.Controls.Add(tree);

            // pb
            pb = new ProPictureBox();
            pb.Dock = DockStyle.Fill;
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            spt.Panel2.Controls.Add(pb);

            MenuStrip ms = InitMenu();
            InitProgressBar(spt, ms);

            //refresh key
            KeyDown += OnKeyDown;

            // cms
            var cms = new ContextMenuStrip();
            cms.Opening += new System.ComponentModel.CancelEventHandler(cms_Opening);
            ContextMenuStrip = cms;


            // init form icon
            this.Icon = Icon.FromHandle(new Bitmap("Assets\\icon-comp.png").GetHicon());
            this.Text = FORM_NAME;
        }

        private void InitProgressBar(SplitContainer spt, MenuStrip ms)
        {
            // progress bar
            Application.EnableVisualStyles();
            toolStripStatusLabel.Overflow = ToolStripItemOverflow.Always;
            progressStatusStrip.Items.Add(toolStripProgressBar);
            progressStatusStrip.Items.Add(toolStripStatusLabel);

            Controls.Add(spt);
            Controls.Add(ms);
            Controls.Add(progressStatusStrip);
        }

        private MenuStrip InitMenu()
        {
            // menu
            // Convert
            var convMenuStrip = new ContextMenuStrip();
            convMenuStrip.Items.Add("Labelme2yolo").Click += (s, e) =>
            {
                var ff = new CommonOpenFileDialog();
                ff.IsFolderPicker = true;
                ff.Title = "Please Select Folder";
                if (ff.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    var outDir = Labelme2yolo(ff.FileName);
                    SetRootDir(outDir);
                }
            };
            convMenuStrip.Items.Add("Yolov8").Click += (s, e) =>
            {
                var ff = new OpenFileDialog();
                if (ff.ShowDialog() == DialogResult.OK)
                {
                    BgDetect(ff.FileName);
                }
            };

            // File
            var fileContextMenuStrip = new ContextMenuStrip();
            fileContextMenuStrip.Items.Add("Open").Click += OnOpen;
            fileContextMenuStrip.Items.Add("ImgSave").Click += OnImgSave;
            fileContextMenuStrip.Items.Add("Reload").Click += MenuReload;
            fileContextMenuStrip.Items.Add("Stop trace").Click += (s, e) => env._traverser.Stop();
            fileContextMenuStrip.Items.Add("-");
            fileContextMenuStrip.Items.Add("Exit").Click += OnExit;

            // Options
            var optContextMenuStrip = new ContextMenuStrip();
            var tsmi = new ToolStripMenuItem
            {
                Text = "AnnotationMode",
            };
            tsmi.CheckedChanged += (s, e) =>
            {
            };
            optContextMenuStrip.Items.Add(tsmi);

            // models
            var modelsCmb = new ToolStripComboBox();
            modelsCmb.FlatStyle = FlatStyle.System;
            modelsCmb.DropDownStyle = ComboBoxStyle.DropDownList;
            UpdateModelCmb(modelsCmb, BaseConfig.GetBaseConfig().CompareWith);
            void onChangeModel(object _s, EventArgs _e)
            {
                var text = ((ToolStripMenuItem)modelsCmb.SelectedItem).Text;

                // update config
                BaseConfig.WriteBaseConfig(nameof(BaseConfig.CompareWith), text);

                // notify
                SetProgressBar($"Selected compare target: {text}", 0);

                BgExplore(wkDir.path);
            }
            modelsCmb.SelectedIndexChanged += onChangeModel;

            // + search
            var srchTxt = new ToolStripSpringTextBox()
            {
                Alignment = ToolStripItemAlignment.Right,
                BorderStyle = BorderStyle.FixedSingle,
            };
            var srchLst = new List<CompNode>();
            srchTxt.KeyPress += (s, e) =>
            {
                if (e.KeyChar == (char)Keys.Return)
                {
                    var key = ((ToolStripTextBox)s).Text;
                    srchLst.Clear();
                    if (string.IsNullOrEmpty(key))
                    {
                        SetProgressBar("Clear search", 0);
                    }
                    else
                    {
                        env._traverser.Start(rootNode, n =>
                        {
                            //n.treeNode.BackColor = Color.Yellow;
                            srchLst.Add(n);
                            return n;
                        }, n => n.IsLeaf && n.Name.Contains(key));
                        foreach (var n in srchLst)
                        {
                            n.treeNode.EnsureVisible();
                            tree.SelectedNode = n.treeNode;
                            tree.Focus();
                            break;
                        }

                        SetProgressBar($"Found {srchLst.Count} items", 0);
                    }
                }
            };

            var nextBtn = new ToolStripButton() { Alignment = ToolStripItemAlignment.Right };
            nextBtn.Image = new Bitmap(@"Assets\icon-next.png");
            nextBtn.Click += (s, e) =>
            {
                if (srchLst.Count == 0 && tree.SelectedNode != null)
                {
                    var nextNode = tree.SelectedNode.NextNode;
                    if (nextNode != null)
                    {
                        tree.SelectedNode = nextNode;
                        nextNode.EnsureVisible();
                        OnChangeSelectedNode((CompNode)nextNode.Tag);
                    }
                    return;
                }

                var i = srchLst.FindIndex(n => ((CompNode)n).treeNode.IsSelected);
                if (i != -1 && (i + 1) < srchLst.Count)
                {
                    srchLst[i + 1].treeNode.EnsureVisible();
                    tree.SelectedNode = srchLst[i + 1].treeNode;
                }
            };

            var prevBtn = new ToolStripButton() { Alignment = ToolStripItemAlignment.Right };
            prevBtn.Image = new Bitmap(@"Assets\icon-next.png");
            prevBtn.Image.RotateFlip(RotateFlipType.Rotate180FlipY);
            prevBtn.Click += (s, e) =>
            {
                if (srchLst.Count == 0 && tree.SelectedNode != null)
                {
                    var prevNode = tree.SelectedNode.PrevNode;
                    if (prevNode != null)
                    {
                        tree.SelectedNode = prevNode;
                        prevNode.EnsureVisible();
                        OnChangeSelectedNode((CompNode)prevNode.Tag);
                    }
                    return;
                }

                var i = srchLst.FindIndex(n => n.treeNode.IsSelected);
                if (i != -1 && i > 0)
                {
                    srchLst[i - 1].treeNode.EnsureVisible();
                    tree.SelectedNode = srchLst[i - 1].treeNode;
                }
            };

            var ms = new MenuStrip();
            ms.Items.AddRange(new[] {
                new ToolStripMenuItem("File"){DropDown = fileContextMenuStrip},
                //new ToolStripMenuItem("Options"){DropDown = optContextMenuStrip},
                new ToolStripMenuItem("Tools"){DropDown = convMenuStrip},
                (ToolStripItem)modelsCmb,
                nextBtn,
                prevBtn,
                srchTxt,
            });
            return ms;
        }

        private void MenuReload(object sender, EventArgs e)
        {
            // clear all controls when reload GUI
            this.Controls.Clear();

            // init GUI
            InitGUI();

            // render tree
            if (rootNode != null)
            {
                RenderTree(rootNode);
            }
        }

        private void InitTree()
        {

            // tree
            tree = new TreeView();
            tree.ShowNodeToolTips = true;
            tree.CheckBoxes = true;
            tree.AllowDrop = true;
            tree.ShowNodeToolTips = true;
            tree.Dock = DockStyle.Fill;
            tree.NodeMouseClick += Tree_NodeMouseClick;

            tree.ItemDrag += new ItemDragEventHandler(tree_ItemDrag);
            tree.DragEnter += new DragEventHandler(tree_DragEnter);
            tree.DragOver += new DragEventHandler(tree_DragOver);
            tree.DragDrop += new DragEventHandler(tree_DragDrop);

            tree.DrawNode += (sender, e) =>
            {
                // first, let .NET draw the Node with its defaults
                e.DrawDefault = true;
                // Now update the highlighting or not
                if (e.State == TreeNodeStates.Selected)
                {
                    e.Node.BackColor = SystemColors.Highlight;
                    e.Node.ForeColor = SystemColors.HighlightText;
                }
                else
                {
                    e.Node.BackColor = ((TreeView)sender).BackColor;
                    e.Node.ForeColor = ((TreeView)sender).ForeColor;
                }
            };
            //tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            tree.HideSelection = false;
        }

        private void OnImgSave(object sender, EventArgs e)
        {
            var saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "img files (*.jpeg)|*.jpeg";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                pb.Image.Save(saveFileDialog1.FileName);
            }
        }

        #region render
        void UpdateModelCmb(object s, string curModel)
        {
            var modelsContextMenuStrip = (ToolStripComboBox)s;
            modelsContextMenuStrip.Items.Clear();

            var lst = new List<string>() { "test", "YOLODataset\\images" };
            lst.ForEach(sub =>
            {
                if (Directory.Exists(Path.Combine(wkDir.path, sub)))
                {
                    var tsmi = new ToolStripMenuItem
                    {
                        Text = sub,
                    };
                    modelsContextMenuStrip.Items.Add(tsmi);
                    if (curModel == sub)
                    {
                        modelsContextMenuStrip.SelectedItem = tsmi;
                    }
                }
            });
        }
        #endregion

        // backgroud work callback

        private bool Refresh(CompNode rootNode, ReportCallback cb = null)
        {

            env._traverser.Start(rootNode, (n) =>
            {
                RefreshImgNode(n, cb);
                return true;
            }, n => n.isFile);

            return true;
        }

        private void RefreshImgNode(CompNode imgNode, ReportCallback cb = null)
        {
            try
            {

                // re-compare
                var modelsD = new DirectoryInfo(wkDir.models);
                var r = new Regex(@"\.jpeg$");
                //if (imgNode.testImg == null)
                {
                    imgNode.testImg = cmp.CreateTestImg(Path.Combine(imgNode._parent.di.FullName, imgNode._name));
                }

                if (imgNode.testImg.boxes == null) { throw new Exception("not exit notion"); }

                imgNode.testImg.results = new List<ActualResult>();

                foreach (var model in modelsD.GetDirectories())
                {
                    var labelFile = Path.Combine(model.FullName, "output", imgNode._parent.di.Name, r.Replace(imgNode._name, ".txt"));
                    var ts = imgNode._parent.di;
                    if (File.Exists(labelFile))
                    {
                        var boxes2 = cmp.ParseJson(File.ReadAllText(labelFile));
                        var (d, l) = Compbox(ts.Name, model.Name, imgNode.testImg, boxes2);
                    }
                }

                cb?.Invoke(imgNode);
            }
            catch
            {

            }
        }


        // backgroud work
        BgWrk bg = new BgWrk();
        private void BgExplore(string modelPath)
        {
            wkDir.path = modelPath;
            tree.Nodes.Clear();

            //read dict
            if (!File.Exists(wkDir.names_txt)) return;

            labelConfig._dict = cmp.ParseDict(wkDir.names_txt);

            SetProgressBar("Start", 1);
            void cb(object arg)
            {
                var node = (CompNode)arg;
                if (node.isFile)
                {
                    // Refresh(node);
                }
                bg.Notify(arg);
            }
            object w(object arg) { return CompShell.Explore(env, cb); }
            void c(object result)
            {
                rootNode = (CompNode)result;
                RenderTree(rootNode);

                // stop progress
                SetProgressBar($"Explore done! {rootNode._path}", 0);


                //BgRefresh(rootNode);
            }
            void p(object obj)
            {
                var n = (CompNode)obj;
                SetProgressBar(n._path, 1);
                return;

                if (!n.isFile)
                {
                    //Logger.Logger.Debug(Environment.CurrentManagedThreadId + "r:" + n._path);
                }

                // render node
                var newTreeNode = new TreeNode(n.Name)
                {
                    Tag = n,
                    ImageIndex = n.state
                };
                n.treeNode = newTreeNode;
                if (n._parent == null)
                {
                    tree.Nodes.Add(newTreeNode);
                }
                else
                {
                    n._parent.treeNode.Nodes.Add(newTreeNode);
                }
            }
            bg.BgExecute(w, c, p);
        }
        private void BgDetect(string modelPath, CompNode node = null)
        {
            if (node == null) { node = rootNode; }

            var modelDir = Path.GetDirectoryName(modelPath);
            var modelName = Path.GetFileName(modelDir);
            var outdir = Path.Combine(modelDir, "output");

            SetProgressBar("Start", 1);
            void cb(object arg)
            {
                var n = (CompNode)arg;
                var ts = n._parent;
                var imgDir = ts.di.FullName;
                var imgPath = Path.Combine(imgDir, n._path);
                var labelDir = Path.Combine(outdir, ts.di.Name);
                var jsonFile = Path.Combine(labelDir, Path.GetFileNameWithoutExtension(imgPath) + ".txt");
                CompShell.CompareNode(env, n, imgPath, jsonFile, modelName, ts._path);

                bg.Notify(arg);
            }
            object w(object arg) { return CompShell.Detect(env, node, modelPath, cb); }
            void c(object result)
            {
                // stop progress
                SetProgressBar($"Detecting done! {node._path}", 0);
            }
            void p(object obj)
            {
                var cur = (CompNode)obj;
                UpdateTreeNode(cur);
                if (tree.SelectedNode != null && cur == tree.SelectedNode.Tag)
                {
                    DrawImgWithsBoxes(cur);
                }
                SetProgressBar(cur._path, 1);
            }
            bg.BgExecute(w, c, p);
        }
        private void BgRefresh(CompNode node)
        {
            SetProgressBar("Start", 1);
            void cb(object arg)
            {
                bg.Notify(arg);
            }
            object w(object arg) { return Refresh(node, cb); }
            void c(object result)
            {
                // stop progress
                SetProgressBar($"Refresh done! {node._path}", 0);
            }
            void p(object obj)
            {
                var n = (CompNode)obj;
                UpdateTreeNode(n);
                if (n.treeNode.IsSelected)
                {
                    DrawImgWithsBoxes(n);
                }

                SetProgressBar(n._path, 1);
            }
            bg.BgExecute(w, c, p);
        }

        private static void UpdateTreeNode(CompNode cur)
        {
            if (cur.isFile)
            {
                cur.treeNode.Text = cur.Name;
                cur.treeNode.ImageIndex = 3;
                cur.treeNode.SelectedImageIndex = 3;

                if (cur.testImg == null) return;
                if (cur.testImg.results == null) return;

                cur.treeNode.ImageIndex = cur.IsOk() ? 1 : 2;
                cur.treeNode.SelectedImageIndex = cur.IsOk() ? 1 : 2;

                foreach (var item in cur.testImg.results)
                {
                    cur.treeNode.ToolTipText = item.diff > 0 ? item.diffJson : "";
                    break;
                }
            }
        }

        private string Labelme2yolo(string inputDir)
        {
            // inputDir:
            //   train
            //   val
            //   dict.txt
            // ouputDir: data
            //   images
            //     train
            //     val
            //   labels
            //     train
            //     val

            var di = new DirectoryInfo(inputDir);
            var dict = new Dictionary<string, int>();
            var outDir = Path.Combine(inputDir, "YOLODataset");

            // read label dict
            var dictF = di.GetFiles("dict.txt").FirstOrDefault();
            if (dictF != null)
            {
                var labelDict = ReadLabelDict(dictF.FullName);
                labelDict.ToList().ForEach(p => dict.Add(p.Value, p.Key));
            }

            // create output dir
            di.GetDirectories().Where(s => s.Name != "YOLODataset").ToList().ForEach(sub =>
            {
                var imgSD = Directory.CreateDirectory(Path.Combine(outDir, "images", sub.Name));
                var labelSD = Directory.CreateDirectory(Path.Combine(outDir, "labels", sub.Name));
                foreach (var img in sub.GetFiles("*.jpeg"))
                {
                    var label = Path.Combine(sub.FullName, Path.GetFileNameWithoutExtension(img.Name) + ".json");
                    var txt = File.ReadAllText(label);
                    Labelme data = File.ReadAllText(label).FromJson<Labelme>();
                    var lines = data.shapes.ConvertAll(shape =>
                    {
                        if (!dict.ContainsKey(shape.label)) { dict.Add(shape.label, dict.Count); }
                        var idx = dict[shape.label];
                        var line = $"{idx} " + string.Join(" ", shape.points.ConvertAll(p => $"{p[0] / data.imageWidth} {p[1] / data.imageHeight}"));
                        return line;
                    });

                    File.WriteAllLines(Path.Combine(labelSD.FullName, Path.GetFileNameWithoutExtension(img.Name) + ".txt"), lines);
                    File.Copy(img.FullName, Path.Combine(imgSD.FullName, img.Name), true);
                }
            });

            // create dataset.yaml
            var path = outDir.Replace("\\", "/");
            var tmpl = $"path: {path}\r\ntrain: images/train\r\nval: images/val\r\ntest: images/test\r\nnames:\r\n";
            var cfgFile = Path.Combine(outDir, "dataset.yaml");
            File.WriteAllText(cfgFile, tmpl + string.Join("\r\n", dict.ToList().ConvertAll(p => $"  {p.Value}: {p.Key}")));

            return outDir;
        }

        private void tree_ItemDrag(object sender, ItemDragEventArgs e)
        {
            // Move the dragged node when the left mouse button is used.
            if (e.Button == MouseButtons.Left)
            {
                DoDragDrop(e.Item, DragDropEffects.Move);
            }

            // Copy the dragged node when the right mouse button is used.
            else if (e.Button == MouseButtons.Right)
            {
                DoDragDrop(e.Item, DragDropEffects.Copy);
            }
        }

        // Set the target drop effect to the effect 
        // specified in the ItemDrag event handler.
        private void tree_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;
        }

        // Select the node under the mouse pointer to indicate the 
        // expected drop location.
        private void tree_DragOver(object sender, DragEventArgs e)
        {
            // Retrieve the client coordinates of the mouse position.
            Point targetPoint = tree.PointToClient(new Point(e.X, e.Y));

            // Select the node at the mouse position.
            var node = tree.GetNodeAt(targetPoint);
            CompNode data = (CompNode)node.Tag;
            if (data != null && !data.isFile)
            {
                tree.SelectedNode = tree.GetNodeAt(targetPoint);
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void tree_DragDrop(object sender, DragEventArgs e)
        {
            // Retrieve the client coordinates of the drop location.
            Point targetPoint = tree.PointToClient(new Point(e.X, e.Y));

            // Retrieve the node at the drop location.
            TreeNode targetNode = tree.GetNodeAt(targetPoint);

            // Retrieve the node that was dragged.
            TreeNode draggedNode = (TreeNode)e.Data.GetData(typeof(TreeNode));

            // Confirm that the node at the drop location is not 
            // the dragged node or a descendant of the dragged node.
            if (!draggedNode.Equals(targetNode) && !ContainsNode(draggedNode, targetNode))
            {
                // If it is a move operation, remove the node from its current 
                // location and add it to the node at the drop location.
                if (e.Effect == DragDropEffects.Move)
                {
                    //draggedNode.Remove();
                    //targetNode.Nodes.Add(draggedNode);

                    // move image
                    MoveImage((CompNode)draggedNode.Tag, (CompNode)targetNode.Tag);
                    RenderTree(rootNode);
                }

                // If it is a copy operation, clone the dragged node 
                // and add it to the node at the drop location.
                else if (e.Effect == DragDropEffects.Copy)
                {
                    targetNode.Nodes.Add((TreeNode)draggedNode.Clone());
                }

                // Expand the node at the location 
                // to show the dropped node.
                targetNode.Expand();
            }
        }

        private void MoveImage(CompNode imgN, CompNode dirN)
        {
            if (!imgN.isFile || dirN.isFile) { return; }

            var parent = imgN._parent;
            var src = Path.Combine(parent.di.FullName, imgN._path);
            var des = Path.Combine(dirN.di.FullName, imgN._path);
            File.Move(src, des);

            if (imgN.state == 1)
            {
                // move .txt
                var src2 = GetLabelFile(src);
                var des2 = GetLabelFile(des);
                File.Move(src2, des2);
            }

            parent._nodes.Remove(imgN);
            dirN._nodes.Add(imgN);
            imgN._parent = dirN;
        }

        private string GetLabelFile(string imgPath)
        {
            var labelD = ReplaceLastOccurrence(Path.GetDirectoryName(imgPath), "images", "labels");
            return Path.Combine(labelD, Path.GetFileNameWithoutExtension(imgPath) + ".txt");
        }
        public static string ReplaceLastOccurrence(string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);

            if (place == -1)
                return source;

            return source.Remove(place, find.Length).Insert(place, replace);
        }

        private bool ContainsNode(TreeNode node1, TreeNode node2)
        {
            // Check the parent node of the second node.
            if (node2.Parent == null) return false;
            if (node2.Parent.Equals(node1)) return true;

            // If the parent node is not null or equal to the first node, 
            // call the ContainsNode method recursively using the parent of 
            // the second node.
            return ContainsNode(node1, node2.Parent);
        }

        private void swViewMode(bool @checked)
        {
            tree.Nodes.Clear();
        }

        private void Tree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeViewHitTestInfo info = tree.HitTest(e.X, e.Y);
            if (info == null) { return; }
            if (info.Location == TreeViewHitTestLocations.StateImage) { return; }

            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip.Show();
                ContextMenuStrip.Items.Clear();
                var n = (CompNode)e.Node.Tag;
                //ContextMenuStrip.Items.Add("Clean").Click += (_s, _e) =>
                //{
                //    if (n.isFile)
                //    {
                //        var f = Path.Combine(n.parent.di.FullName, n.name);
                //        switch (Path.GetExtension(n.name).ToLower())
                //        {
                //            case ".sln":
                //                CleanSln(f);
                //                Refresh(n.parent);
                //                return;
                //            case ".csproj":
                //                CleanPrj(f);
                //                Refresh(n.parent);
                //                return;
                //        }
                //    }
                //};
                ContextMenuStrip.Items.Add("Explore").Click += (_s, _e) =>
                {
                    var f = (n.isFile) ? n._parent.di.FullName : n.di.FullName;
                    Process.Start(f);
                };
                base.ContextMenuStrip.Items.Add("Refresh").Click += (_s, _e) =>
                {
                    if (n.isFile)
                    {
                        Refresh(n, null);
                        UpdateTreeNode(n);
                        DrawImgWithsBoxes(n);
                    }
                    else
                    {
                        BgRefresh(n);
                    }
                };
                base.ContextMenuStrip.Items.Add("CopyName").Click += (_s, _e) =>
                {
                    Clipboard.SetText($"{n.PhysicName}");
                };
                if (n.isFile)
                {
                    base.ContextMenuStrip.Items.Add("Use Right Boxes").Click += (_s, _e) =>
                    {
                        n.testImg.boxes.Clear();
                        foreach (var box in n.testImg.results[0].boxes)
                        {
                            n.testImg.boxes.Add(new Box
                            {
                                X = box.X,
                                H = box.H,
                                Y = box.Y,
                                W = box.W,
                                LabelName = box.LabelName,
                                LabelId = box.LabelId
                            });
                        }
                        n.testImg.results[0].diff = 0;
                        n.testImg.results[0].diffJson = "";
                        CompModels.SaveAsYolo(n.testImg);
                        DrawImgWithsBoxes(n);
                        UpdateTreeNode(n);
                    };
                }

                var cms_moveTo = new ContextMenuStrip();
                var modelsD = wkDir.models;
                var lst = YolovExtension.GetModles(modelsD);
                foreach (var (model, path, type) in lst)
                {
                    cms_moveTo.Items.Add(model).Click += (_s, _e) =>
                    {
                        DetectWithModel(n, path);
                    };
                }
                ToolStripMenuItem tsmi_compTo = new ToolStripMenuItem("Compare");
                tsmi_compTo.DropDown = cms_moveTo;
                ContextMenuStrip.Items.Add(tsmi_compTo);
                if (!n.isFile)
                {
                    base.ContextMenuStrip.Items.Add(e.Node.Checked ? "Export checked" : "Export").Click += (_s, _e) =>
                    {
                        var nodeslst = new List<CompNode>();
                        if (e.Node.Checked)
                        {
                            env._traverser.Start(rootNode, x =>
                            {
                                nodeslst.Add(x);
                                return true;
                            }, x => !x.isFile && x.treeNode != null && x.treeNode.Checked);
                        }
                        else
                        {
                            env._traverser.Start(n, x =>
                            {
                                nodeslst.Add(x);
                                return true;
                            }, x => !x.isFile);
                        }
                        string xlsx = CompShell.ExportCompResult(env, nodeslst);

                        SetProgressBar($"Compare result was saved! {xlsx}", 0);
                    };
                }
                tree.SelectedNode = e.Node;
            }
            else if (e.Button == MouseButtons.Left)
            {
                var n = (CompNode)e.Node.Tag;
                OnChangeSelectedNode(n);
            }
        }

        private void OnChangeSelectedNode(CompNode n)
        {
            if (!n.isFile) return;

            if (n.testImg == null)
            {
                n.testImg = cmp.CreateTestImg(Path.Combine(n._parent.di.FullName, n._path));
            }

            DrawImgWithsBoxes(n);

            SetProgressBar(n._path, 0);
        }

        private void DrawImgWithsBoxes(CompNode n)
        {
            var path = Path.Combine(n._parent.di.FullName, n._path);

            var bmp = FromFile(path);
            var bmp4 = new Bitmap(bmp.Width * 2, bmp.Height * 2);
            var scale = BaseConfig.GetBaseConfig().fontScale;

            using (Graphics G = Graphics.FromImage(bmp4))
            {
                var plt = new PlotBox();
                var font = new Font("Arial", bmp.Height / scale);
                var points = new Point[4] {
                        new Point(0,0),
                        new Point(bmp.Width,0),
                        new Point(0,bmp.Height),
                        new Point(bmp.Width, bmp.Height)
                    };

                // draw base
                var clone0 = (Bitmap)bmp.Clone();
                plt.CreateResultImg(clone0, font, n.testImg.boxes);
                List<(string, Bitmap)> clones = new List<(string, Bitmap)> {
                    ("0",clone0)
                };

                // draw compare
                foreach (var res in n.testImg.results)
                {
                    var clone = (Bitmap)bmp.Clone();
                    plt.CreateResultImg(clone, font, res.boxes);

                    // draw diff
                    var diffJson = res.diffJson;
                    var diff = diffJson.FromJson<List<CompExtensions.CompCell<Box>>>();

                    using (Graphics G2 = Graphics.FromImage(clone))
                    {
                        var fw = bmp.Height / scale / 5;
                        foreach (var c in diff)
                        {
                            switch (c.type)
                            {
                                case EditType.insert:
                                    hlBox(c.a, fw, G2, Color.Green);
                                    break;
                                case EditType.delete:
                                    hlBox(c.a, fw, G2, Color.Red);
                                    break;
                            }
                        }
                    }

                    clones.Add((res.modelName, clone));
                }

                // plot in grid
                int i = 0;
                foreach (var (modelName, clone) in clones)
                {
                    G.DrawImage(clone, points[i]);
                    G.DrawString(modelName, font, Brushes.White, points[i]);
                    i++;
                }
            }
            pb.Image = bmp4;

            void hlBox(Box box, int fw, Graphics G2, Color color)
            {
                var cx = box.X + box.W / 2;
                var cy = box.Y + box.H / 2;
                var r = (float)Math.Sqrt(box.W * box.W + box.H * box.H) / 2;
                r = Math.Max(r, 200);
                G2.DrawEllipse(new Pen(color, fw), cx - r, cy - r, r * 2, r * 2);
            }
        }

        private void DetectWithModel(CompNode n, string modelPath)
        {
            BgDetect(modelPath, n);
        }

        public static Bitmap FromFile(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
            return (Bitmap)img;
        }

        private class ImgObject
        {
            public string Name { get; set; }
            public Double xmin { get; set; }
            public Double ymin { get; set; }
            public Double xmax { get; set; }
            public Double ymax { get; set; }
            //public Vector2D CenterPos;
            public int width { get; set; }
            public int height { get; set; }
            public int percent { get; set; }
            public ImgObject(string name, Double xmin, Double ymin, Double xmax, Double ymax, int percent)
            {
                this.Name = name;
                this.xmin = xmin;
                this.ymin = ymin;
                this.xmax = xmax;
                this.ymax = ymax;
                this.width = (int)(xmax - xmin);
                this.height = (int)(ymax - ymin);
                this.percent = percent;
                //CenterPos = new Vector2D((xmin + xmax) / 2, (ymin + ymax) / 2);
            }
            public ImgObject(string name, Double xmin, Double ymin, Double xmax, Double ymax)
            {
                this.Name = name;
                this.xmin = xmin;
                this.ymin = ymin;
                this.xmax = xmax;
                this.ymax = ymax;
                this.width = (int)(xmax - xmin);
                this.height = (int)(ymax - ymin);
                //CenterPos = new Vector2D((xmin + xmax) / 2, (ymin + ymax) / 2);
            }
        }


        LabelConfig labelConfig = new LabelConfig();
        ColorPalette colorPalette = new ColorPalette();
        private int rootImageIndex;
        private int selectedCustomerImageIndex;
        private int unselectedCustomerImageIndex;
        private int selectedOrderImageIndex;
        private int unselectedOrderImageIndex;

        private Brush getCorlor(string name)
        {
            if (colorPalette.d == null)
            {
                var solidColorBrushList = new List<Brush>()
                {
                     new SolidBrush(Color.FromArgb(255,27,161,226)),
                     new SolidBrush(Color.FromArgb(255,160,80,0)),
                     new SolidBrush(Color.FromArgb(255, 51, 153, 51)),
                     new SolidBrush(Color.FromArgb(255, 162, 193, 57)),
                     new SolidBrush(Color.FromArgb(255, 216, 0, 115)),
                     new SolidBrush(Color.FromArgb(255, 240, 150, 9)),
                     new SolidBrush(Color.FromArgb(255, 230, 113, 184)),
                     new SolidBrush(Color.FromArgb(255, 162, 0, 255)),
                     new SolidBrush(Color.FromArgb(255, 229, 20, 0)),
                     new SolidBrush(Color.FromArgb(255, 0, 171, 169))
                };
                colorPalette.d = new Dictionary<string, Brush>();
                labelConfig._dict.ToList().ForEach(p => colorPalette.d[p.Value] = solidColorBrushList[(int)p.Key % solidColorBrushList.Count]);
                labelConfig._dict2.ToList().ForEach(p => colorPalette.d[p.Value] = colorPalette.d[p.Key]);
            }

            if (colorPalette.d.ContainsKey(name))
                return colorPalette.d[name];

            return Brushes.Red;
        }
        private Brush getCorlor(ImgObject imgObject)
        {
            return colorPalette.GetColor(imgObject.Name);
        }



        private void CleanSln(string f)
        {
            var cmd = new string[]
            {
                $@"call ""C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat""",
                $@"msbuild {f} -t:""Clean"" /p:Configuration=""Debug""",
                $@"msbuild {f} -t:""Clean"" /p:Configuration=""Release"""
            };
            var bat = Path.GetTempPath() + @"\cleanGui.bat";
            File.WriteAllLines(bat, cmd);
            var process = Process.Start(bat);
            process.WaitForExit(-1);
            File.Delete(bat);
        }
        private void CleanPrj(string f)
        {
            var cmd = new string[]
            {
                $@"call ""C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat""",
                $@"msbuild {f} -t:""Clean""",
            };
            var bat = Path.GetTempPath() + @"\cleanGui.bat";
            File.WriteAllLines(bat, cmd);
            var process = Process.Start(bat);
            process.WaitForExit(-1);
            File.Delete(bat);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                if (Directory.Exists(RootDir))
                {
                    tree.Nodes.Clear();
                    Compare(this, new DoWorkEventArgs(RootDir));
                }
            }
        }

        private void cms_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var ispb = sender as PictureBox;
            var cms = sender as ContextMenuStrip;
            if (ispb != null)
            {

            }

            // Set Cancel to false. 
            // It is optimized to true based on empty entry.
            e.Cancel = false;
        }

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
        private void OnDone(object sender, RunWorkerCompletedEventArgs e)
        {
            var node = (CompNode)e.Result;
            RenderTree(node);

            // stop progress
            SetProgressBar(node.Name, 0);
        }

        private void RenderTree(CompNode node)
        {
            // Load the images in an ImageList.
            var ass = new Assets.Assets();

            // Assign the ImageList to the TreeView.
            tree.ImageList = ass.Theme2();

            // Set the TreeView control's default image and selected image indexes.
            //tree.ImageIndex = 0;
            //tree.SelectedImageIndex = 1;

            /* Set the index of image from the 
            ImageList for selected and unselected tree nodes.*/
            this.rootImageIndex = 2;
            this.selectedCustomerImageIndex = 3;
            this.unselectedCustomerImageIndex = 4;
            this.selectedOrderImageIndex = 5;
            this.unselectedOrderImageIndex = 6;

            // clear
            tree.Nodes.Clear();
            // create root node
            var st = new List<(CompNode, TreeNode)> { (node, new TreeNode(node.Name) { Tag = node }) };
            // add root treeNode
            tree.Nodes.Add(st[0].Item2);

            while (st.Count > 0)
            {
                var cur = st[0].Item1;
                var curTreeNode = st[0].Item2;
                st.RemoveAt(0);
                if (cur._nodes != null)
                {
                    // create treeNode
                    st.InsertRange(0, cur._nodes.ConvertAll(n =>
                    {
                        var newTreeNode = new TreeNode(n.Name) { Tag = n };
                        n.treeNode = newTreeNode;
                        UpdateTreeNode(n);
                        //if (n.isFile)
                        //{
                        //    var idx = n.testImg ==  null || n.testImg.results.Count == 0 ? Assets.IconIdx.neu
                        //    : n.IsOk() ? Assets.IconIdx.ok : Assets.IconIdx.ng;
                        //    newTreeNode.StateImageIndex = (int)idx;
                        //    newTreeNode.SelectedImageIndex = (int)idx;
                        //}
                        curTreeNode.Nodes.Add(newTreeNode);
                        return (n, newTreeNode);
                    }));
                }
            }

            // expand parent
            tree.Nodes[0].Expand();
        }

        private void OnProgress(object sender, ProgressChangedEventArgs e)
        {
            var cur = (CompNode)e.UserState;
            SetProgressBar(cur.Name, 1);
            return;
            if (cur.treeNode == null)
            {
                if (cur._parent == null)
                {
                    // root node
                    cur.treeNode = tree.Nodes.Add(cur.Name);
                }
                else
                {
                    cur.treeNode = cur._parent.treeNode.Nodes.Add(cur.Name);
                }
                cur.treeNode.Tag = cur;
            }
        }

        private class MyImg
        {
            public string path;

            public bool hasLabel;
        }

        private class MyData
        {
            public string path; // train|val
            public List<MyImg> images;
        }
        private List<MyData> ReadData(string dir)
        {
            // C:\work\investigate\yolov5\datasets\dd\data607
            // images
            //   train
            //   val
            // labels
            //   train
            //   val
            var imagesD = new DirectoryInfo(Path.Combine(dir, "images"));
            var lst = imagesD.GetDirectories().ToList().ConvertAll(
            di =>
            {
                var newData = new MyData { path = di.FullName };
                var files = di.GetFiles("*.*").Where(f => Regex.IsMatch(f.Extension, "^\\.(jpeg|png)$", RegexOptions.IgnoreCase)).ToList();
                var labelD = di.FullName.Replace("images", "labels");
                var h = Directory.Exists(labelD) ? new DirectoryInfo(labelD).GetFiles("*.txt").Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToHashSet() : new HashSet<string>();
                newData.images = files.ToList().ConvertAll((file) => new MyImg
                {
                    path = file.FullName,
                    hasLabel = h.Contains(Path.GetFileNameWithoutExtension(file.Name))
                });
                return newData;
            });

            // read dict
            labelConfig = ReadLabelConfig(dir);

            return lst;
        }

        private LabelConfig ReadLabelConfig(string dir)
        {
            var labelConfig = new LabelConfig();
            var cfg = new DirectoryInfo(dir).GetFiles("*.yaml").FirstOrDefault();
            if (cfg != null)
            {
                labelConfig._dict = ReadLabelDict(cfg.FullName);
            }
            return labelConfig;
        }

        private Dictionary<int, string> ReadLabelDict(string path)
        {
            var dict = new Dictionary<int, string>();
            var lines = File.ReadAllLines(path);
            var s = 0;
            lines.ToList().ForEach(line =>
            {
                switch (s)
                {
                    case 0:
                        if (Regex.IsMatch(line, "names:"))
                        {
                            s = 1;
                        }
                        break;
                    case 1:
                        var arr = line.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                        dict.Add(int.Parse(arr[0]), arr[1]);
                        break;
                }
            });
            return dict;
        }

        private void Compare(object sender, DoWorkEventArgs e)
        {
            string dir = (string)e.Argument;
            var td = cmp.ParseData(dir);

            // compare data
            CompModels.CompAndExport(dir, td);

            var root = Transform(td);
            e.Result = root;
        }


        private void OnWork2(object sender, DoWorkEventArgs e)
        {
            var node = (CompNode)e.Argument;
            var st = new List<CompNode>();
            st.Add(node);
            while (st.Count > 0)
            {
                var cur = st[0];
                st.RemoveAt(0);
                cur._nodes = cur.di.GetDirectories().ToList().ConvertAll((tdi) => new CompNode(tdi) { _parent = cur });
                st.InsertRange(0, cur._nodes);
                foreach (var f in cur.di.GetFiles())
                {
                    var fNode = new CompNode(f) { _parent = cur };
                    fNode._path = f.Name;
                    fNode.Size = f.Length;
                    cur._nodes.Add(fNode);
                }
                ((BackgroundWorker)sender).ReportProgress(1, cur);
            }
            e.Result = node;
        }

        private void OnExit(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnOpen(object sender, EventArgs e)
        {
            var ff = new CommonOpenFileDialog();
            ff.IsFolderPicker = true;
            ff.Title = "Please Select Folder";
            if (ff.ShowDialog() == CommonFileDialogResult.Ok)
            {
                wkDir.path = ff.FileName;
                BgExplore(wkDir.path);
            }
        }

        private void SetRootDir(string dir)
        {
            BgExplore(wkDir.path);
            return;

            var td = cmp.ParseData(dir);
            if (td == null) return;

            // compare data
            CompModels.CompAndExport(dir, td);

            // transform
            rootNode = Transform(td);

            // render tree
            RenderTree(rootNode);
        }

        private static CompNode Transform(CompModels.TestDir td)
        {
            string rootDir = td.path;
            CompNode root = new CompNode(new DirectoryInfo(rootDir));
            root._nodes = td.testSets.ConvertAll(ts =>
            {
                var n = new CompNode(new DirectoryInfo(Path.Combine(rootDir, "test", ts.Name)))
                {
                    _parent = root
                };
                n._nodes = ts.imgs.ConvertAll(img =>
                {
                    var f = new CompNode(new FileInfo(img.path))
                    {
                        _parent = n,
                        testImg = img,
                    };
                    var key = Path.GetFileNameWithoutExtension(img.path);
                    f.dict = td.models.ConvertAll(model =>
                    {
                        if (model.tsDict.ContainsKey(ts.Name))
                        {
                            var imgDict = model.tsDict[ts.Name].imgDict;
                            if (imgDict.ContainsKey(key))
                            {
                                return (model.Name, imgDict[key]);
                            }
                        }
                        return (model.Name, null);
                    });
                    return f;
                });
                return n;
            });
            return root;
        }

        static private string SizeToString(long size)
        {
            return size.ToString("N0");
            return size > GB ? $"{size / GB}GB" :
                size > MB ? $"{size / MB}MB" :
                size > KB ? $"{size / KB}KB" : $"{size}B";
        }
        const long KB = 1024;
        const long MB = 1024 * KB;
        const long GB = 1024 * MB;
    }
}
