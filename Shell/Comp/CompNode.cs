using annotation;
using Node;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace comp
{
    public class CompNode : Node.NodeBase
    {
        public string _path;
        public List<CompNode> _nodes;
        public CompNode _parent;

        public CompNode(DirectoryInfo di) : base(di) { }
        public CompNode(FileInfo fi) : base(fi)
        {
            _path = fi.FullName;
        }
        override public string Name => $"{_name} {GetStatus()}";

        public string GetStatus()
        {
            if (testImg != null && testImg.results != null && testImg.boxes != null)
            {
                var f = testImg.results.ConvertAll(r => r.boxes.Count);
                f.Insert(0, testImg.boxes.Count);
                return $"{string.Join(",", f)}";
            }
            if (_nodes != null)
            {
                return _nodes.Count.ToString();
            }
            return "";
        }

        public bool IsOk()
        {
            var f = testImg.results.ConvertAll(r => r.diff);
            return f.Sum() == 0;
        }

        public CompModels.TestImg testImg;
        public List<(string, List<Box>)> dict; // (modelName, boxes)
        public List<(string, List<Box>)> PredictResults
        {
            get
            {
                var lst = testImg.results.ConvertAll(r => (r.modelName, r.boxes));
                lst.Insert(0, ("0", testImg.boxes));
                return lst;
            }
        }

        override public INode Parent => _parent;

        override public IEnumerable<INode> Children => _nodes;
    }
}
