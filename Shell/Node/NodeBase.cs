namespace Node
{
    public class NodeBase : INode
    {
        public NodeBase(DirectoryInfo di)
        {
            this.di = di;
            _name = di.Name;
        }
        public NodeBase(FileInfo fi)
        {
            this.fi = fi;
            _name = fi.Name;
            isFile = true;
        }
        public class TreeNode
        {
            internal bool Checked;
        }


        public DirectoryInfo di;
        public bool isFile = false;
        public FileInfo fi;
        public string _name;
        public int state;
        public TreeNode treeNode;
        public (int Width, int Height) bmp;
        public List<annotation.MyBox> objs;
        private long _size = 0;

        public long Size
        {
            get => _size;
            set
            {
                _size = value;
            }
        }

        virtual public INode Parent => throw new System.NotImplementedException();

        virtual public IEnumerable<INode> Children => throw new System.NotImplementedException();

        public bool IsLeaf => isFile;

        virtual public string Name => _name;
        public string PhysicName => _name;
    }
}