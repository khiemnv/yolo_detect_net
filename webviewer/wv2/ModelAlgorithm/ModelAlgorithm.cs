using Google.OrTools.Sat;
using System.Xml;

namespace WP_GUI.ModelAlgorithm
{
    public class ModelAlgorithm
    {
        private List<ImgObject> _LReferenceObj;
        List<List<Double>> _LRefObjDist;
        private Dictionary<string, int> _RefObjNum;
        private ImageSize _ImgSize;

        public ModelAlgorithm(string XmlConfigPath)
        {
            XmlDocument xmlFile = new XmlDocument();
            xmlFile.Load(XmlConfigPath);

            var ImgSize = xmlFile.DocumentElement.SelectSingleNode("size");
            _ImgSize.width = Int32.Parse(ImgSize.ChildNodes[0].InnerText);
            _ImgSize.height = Int32.Parse(ImgSize.ChildNodes[1].InnerText);
            _ImgSize.depth = Int32.Parse(ImgSize.ChildNodes[2].InnerText);


            var ObjNodes = xmlFile.DocumentElement.SelectNodes("object");
            if (ObjNodes == null) return;
            // throw a exception
            // TODO

            _LReferenceObj = getImgObject(ObjNodes);
            _RefObjNum = getObjectNumber(_LReferenceObj);
            _LRefObjDist = new List<List<double>>();
            for (int i = 0; i < _LReferenceObj.Count; i++)
            {
                List<Double> rowDist = new List<double>();
                for (int j = 0; j < _LReferenceObj.Count; j++)
                {
                    Double dist = Vector2D.norm(_LReferenceObj[j].CenterPos - _LReferenceObj[i].CenterPos);
                    rowDist.Add(dist);
                }
                _LRefObjDist.Add(rowDist);
            }
        }

        public ModelAlgorithm(ImageSize imageSize, List<ImgObject> imgObjects)
        {
            _ImgSize = imageSize;
            _LReferenceObj = imgObjects;
            _RefObjNum = getObjectNumber(_LReferenceObj);
            _LRefObjDist = new List<List<double>>();
            for (int i = 0; i < _LReferenceObj.Count; i++)
            {
                List<Double> rowDist = new List<double>();
                for (int j = 0; j < _LReferenceObj.Count; j++)
                {
                    Double dist = Vector2D.norm(_LReferenceObj[j].CenterPos - _LReferenceObj[i].CenterPos);
                    rowDist.Add(dist);
                }
                _LRefObjDist.Add(rowDist);
            }
        }

        public static List<ImgObject> getImgObject(XmlNodeList Nodes)
        {
            List<ImgObject> LObj = new List<ImgObject>();
            foreach (XmlNode node in Nodes)
            {
                string name = node.FirstChild.InnerText;
                var xmin = float.Parse(node.LastChild.ChildNodes[0].InnerText);
                var ymin = float.Parse(node.LastChild.ChildNodes[1].InnerText);
                var xmax = float.Parse(node.LastChild.ChildNodes[2].InnerText);
                var ymax = float.Parse(node.LastChild.ChildNodes[3].InnerText);
                LObj.Add(new ImgObject(name, xmin, ymin, xmax, ymax));
            }
            return LObj;
        }

        private Dictionary<string, int> getObjectNumber(List<ImgObject> LObj)
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            LObj.ForEach(o =>
            {
                if (result.ContainsKey(o.LabelName)) result[o.LabelName]++;
                else result[o.LabelName] = 1;
            });

            return result;
        }
        public List<bool> Reconstruction(in List<ImgObject> ObjDectected, out List<ImgObject> ObjReconstruct, double delta)
        {
            List<bool> rs = Enumerable.Repeat(false, _LReferenceObj.Count).ToList();
            bool res = true;
            List<ImgObject> tmpObjDet = new List<ImgObject>(ObjDectected);

            if (_LReferenceObj == null || _LReferenceObj.Count == 0)
            {
                ObjReconstruct = null;
                //outRs = null;
                return rs;
            }

            // Init All object
            ObjReconstruct = new List<ImgObject>();
            //outRs = Enumerable.Repeat(false, _LReferenceObj.Count).ToList();//new List<bool>();

            // Step 1: Kiem tra xem Detect da du so luong obj chua
            var ObjDetNum = getObjectNumber(tmpObjDet);
            if (ObjDetNum.Count == _RefObjNum.Count)
            {
                foreach (var obj in _RefObjNum)
                {
                    if (!ObjDetNum.ContainsKey(obj.Key) || ObjDetNum[obj.Key] != obj.Value)
                    {
                        res = false;
                        break;
                    }
                }
            }
            else res = false;

            if (!res)
            {
                // Step 2: Neu detect bi false, Tim index cua cac Obj detect dung
                var ObjIdx = getIndex(tmpObjDet);
                for (int i = 0; i < ObjIdx.Count(); i++)
                {
                    rs[ObjIdx[i]] = true;
                }

                // Step 3: Khoi phuc cac vi tri bi sai theo cac vi tri dung
                for (int i = 0; i < _LReferenceObj.Count; i++)
                {
                    bool isExisted = false;
                    for (int j = 0; j < ObjIdx.Length; j++)
                    {
                        if (i == ObjIdx[j])
                        {
                            ObjReconstruct.Add(tmpObjDet[j]);
                            isExisted = true;
                            break;
                        }
                    }
                    if (!isExisted)
                    {
                        ImgObject refObj = _LReferenceObj[i];
                        Vector2D cenPos = findxy(i, tmpObjDet, ObjIdx);
                        Double dx = (refObj.W) / 2;
                        Double dy = (refObj.H) / 2;
                        Double xmin = cenPos.x - dx;
                        Double xmax = cenPos.x + dx;
                        Double ymin = cenPos.y - dy;
                        Double ymax = cenPos.y + dy;
                        ObjReconstruct.Add(new ImgObject(refObj.LabelName, xmin, ymin, xmax, ymax));
                    }
                }
            }
            else
            {
                ObjReconstruct = ObjDectected;
                return Enumerable.Repeat(true, _LReferenceObj.Count).ToList();
            }

            for (int i = 0; i < rs.Count; i++)
            {
                if (!rs[i])
                {
                    for (int j = 0; j < ObjDectected.Count; j++)
                    {
                        var di = Vector2D.norm(ObjDectected[j].CenterPos - ObjReconstruct[i].CenterPos);
                        if (ObjReconstruct[i].LabelName == ObjDectected[j].LabelName && di < delta)
                        {
                            rs[i] = true;
                            ObjReconstruct[i].X = ObjDectected[j].X;
                            ObjReconstruct[i].Y = ObjDectected[j].Y;
                            ObjReconstruct[i].W = ObjDectected[j].W;
                            ObjReconstruct[i].H = ObjDectected[j].H;
                        }
                    }
                }
            }

            return rs;
        }



        private int[] getIndex(List<ImgObject> ObjDectected)
        {
            // Loai bo cac obj bi sai
            var ObjDetNum = getObjectNumber(ObjDectected);
            for (int i = ObjDectected.Count - 1; i >= 0; i--)
            {
                if (!_RefObjNum.ContainsKey(ObjDectected[i].LabelName) ||
                    ObjDetNum[ObjDectected[i].LabelName] != _RefObjNum[ObjDectected[i].LabelName])
                {
                    ObjDectected.RemoveAt(i);
                }
            }

            // sap xep lai obj theo group obj
            for (int i = 0; i < ObjDectected.Count - 2; i++)
            {
                for (int j = ObjDectected.Count - 1; j > i; j--)
                {
                    if (ObjDectected[j].LabelName == ObjDectected[i].LabelName)
                    {
                        ObjDectected.Insert(i, ObjDectected[j]);
                        ObjDectected.RemoveAt(j + 1);
                        j++;
                        i++;
                    }
                }
            }

            // Tim index cho cac Object duy nhat
            int[] objIdx = new int[ObjDectected.Count];
            for (int i = 0; i < ObjDectected.Count; i++)
            {
                if (_RefObjNum[ObjDectected[i].LabelName] == 1)
                {
                    for (int j = 0; j < _LReferenceObj.Count; j++)
                    {
                        if (_LReferenceObj[j].LabelName == ObjDectected[i].LabelName)
                        {
                            objIdx[i] = j;
                            break;
                        }
                    }

                }
            }

            // Tim index cho cac object con lai
            for (int i = 0; i < ObjDectected.Count; i++)
            {
                if (objIdx[i] == 0)
                {
                    objIdx[i] = getIndex(i, ObjDectected, objIdx);
                }
            }

            return objIdx;
        }
        private int getIndex(int idx, List<ImgObject> ObjDectected, int[] objIdx)
        {
            Dictionary<int, Double> dict = new Dictionary<int, Double>();
            for (int i = 0; i < _LReferenceObj.Count; i++)
            {
                Double DiffDist = 0;
                if (_LReferenceObj[i].LabelName == ObjDectected[idx].LabelName)
                {
                    for (int j = 0; j < ObjDectected.Count; j++)
                    {
                        if (j != idx && objIdx[j] != 0)
                        {
                            Double RefDist = Vector2D.norm(_LReferenceObj[objIdx[j]].CenterPos - _LReferenceObj[i].CenterPos);
                            Double DetDist = Vector2D.norm(ObjDectected[j].CenterPos - ObjDectected[idx].CenterPos);
                            DiffDist += Math.Abs(RefDist - DetDist);
                        }
                    }
                }
                dict[i] = DiffDist;
            }
            int res = 0;
            Double minimum = Double.MaxValue;
            for (int i = 0; i < _LReferenceObj.Count; i++)
            {
                if (dict[i] > 0 && dict[i] < minimum)
                {
                    minimum = dict[i];
                    res = i;
                }
            }

            return res;
        }

        private Vector2D findxy(int tgtIdx, List<ImgObject> ObjDet, int[] ObjDetIdx)
        {
            CpModel model = new CpModel();

            IntVar x = model.NewIntVar(0, _ImgSize.width, "x");
            IntVar y = model.NewIntVar(0, _ImgSize.height, "y");

            IntVar[] dist = new IntVar[ObjDet.Count];
            for (int i = 0; i < ObjDet.Count; i++)
            {
                //dist.Add(model.NewIntVar(0, Int32.MaxValue, $"dist{i}"));
                dist[i] = model.NewIntVar(0, Int32.MaxValue, $"dist{i}");
            }

            for (int i = 0; i < ObjDet.Count; i++)
            {
                IntVar dx = model.NewIntVar(Int32.MinValue, Int32.MaxValue, $"dx{i}");
                IntVar dy = model.NewIntVar(Int32.MinValue, Int32.MaxValue, $"dy{i}");
                model.Add(dx == x - (int)(ObjDet[i].CenterPos.x));
                model.Add(dy == y - (int)(ObjDet[i].CenterPos.y));

                IntVar d2x = model.NewIntVar(0, Int32.MaxValue, $"d2x{i}");
                IntVar d2y = model.NewIntVar(0, Int32.MaxValue, $"d2y{i}");
                model.AddMultiplicationEquality(d2x, dx, dx);
                model.AddMultiplicationEquality(d2y, dy, dy);
                model.AddAbsEquality(dist[i], d2x + d2y - (int)(_LRefObjDist[tgtIdx][ObjDetIdx[i]] * _LRefObjDist[tgtIdx][ObjDetIdx[i]]));
            }

            model.Minimize(LinearExpr.Sum(dist));

            CpSolver solver = new CpSolver();
            CpSolverStatus status = solver.Solve(model);
            if (status == CpSolverStatus.Optimal)
            {
                return new Vector2D(solver.Value(x), solver.Value(y));
            }
            else
            {
                Console.WriteLine("No solution found.");
            }

            return null;
        }

    }
}

