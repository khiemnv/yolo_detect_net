using Node;

namespace annotation
{
    public class AnntNode : Node.NodeBase
    {
        public AnntNode _parent;
        public List<AnntNode> _nodes;
        public AnntNode(DirectoryInfo di) : base(di)
        {
        }
        public AnntNode(FileInfo fi) : base(fi)
        {
        }
        override public string Name
        {
            get
            {
                var txt = PhysicName;
                if (objs != null) { txt += $" {objs.Count}"; }
                if (_nodes != null) { txt += $" {_nodes.Count}"; }
                return txt;
            }
        }

        public string lPath
        {
            get
            {
                if (_parent != null)
                {
                    return Path.Combine(_parent.di.FullName.Replace("images", "labels"), Path.GetFileNameWithoutExtension(_name) + ".txt");
                }
                else if (fi != null)
                {
                    var labelD = YolovExtension.ReplaceLastOccurrence(Path.GetDirectoryName(fi.FullName), "images", "labels");
                    return Path.Combine(labelD, Path.GetFileNameWithoutExtension(_name) + ".txt");
                }
                else
                {
                    return "";
                }
            }
        }

        override public INode Parent => _parent;

        override public IEnumerable<INode> Children => _nodes;
    }
}
