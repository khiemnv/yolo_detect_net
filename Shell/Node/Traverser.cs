using Node;
using System.Collections.Generic;
using System.Linq;

namespace annotation
{
    public class Traverser
    {
        public delegate object ExecuteCb<T>(T arg);
        public delegate bool FilterCb<T>(T arg);

        private bool interuptTraceReq;
        public void Stop() { interuptTraceReq = true; }
        public void Start(INode rootNode, ExecuteCb<INode> executeCb, FilterCb<INode> filterCb)
        {
            interuptTraceReq = false;
            var lst = new List<INode> { rootNode };
            var i = 0;
            for (; i < lst.Count; i++)
            {
                if (interuptTraceReq) { break; }

                var cur = lst[i];
                if (filterCb(cur))
                {
                    executeCb(cur);
                }

                if (!cur.IsLeaf)
                {
                    lst.AddRange(cur.Children);
                }
            }
        }
        public void Start<T>(T rootNode, ExecuteCb<T> executeCb, FilterCb<T> filterCb)
            where T : INode
        {
            interuptTraceReq = false;
            var lst = new List<T> { rootNode };
            var i = 0;
            for (; i < lst.Count; i++)
            {
                if (interuptTraceReq) { break; }

                var cur = lst[i];
                if (filterCb(cur))
                {
                    executeCb(cur);
                }

                if (!cur.IsLeaf)
                {
                    lst.AddRange((IEnumerable<T>)cur.Children);
                }
            }
        }

        public static void BFS<T>(T rootNode, ExecuteCb<T> executeCb, FilterCb<T> filterCb)
            where T : INode
        {
            var stack = new Stack<List<T>>();
            stack.Push(new List<T> { rootNode });
            while (stack.Count > 0)
            {
                var lvl = stack.Pop();
                var lvl2 = new List<T>();
                lvl.ForEach(node =>
                {
                    if (filterCb(node))
                    {
                        executeCb(node);
                    }

                    if (!node.IsLeaf)
                    {
                        lvl2.AddRange((IEnumerable<T>)node.Children);
                    }
                });

                if (lvl2.Count > 0) { stack.Push(lvl2); }
            }
        }

        public static int CountLeafs(INode rootNode)
        {
            if (rootNode.IsLeaf) { return 1; }

            var lst = new MyStack(rootNode);
            var total = 0;
            while (lst.Count > 0)
            {
                var top = lst.Pop();

                var firtChild = top.Children?.FirstOrDefault();
                if (firtChild != null && firtChild.IsLeaf)
                {
                    total += top.Children.Count();
                }
                else
                {
                    lst.Push(top.Children);
                }
            }
            return total;
        }
    }

    public class MyStack
    {
        public MyStack(INode root) { stack.Add(root); }
        List<INode> stack = new List<INode>();
        public void Push(IEnumerable<INode> nodes)
        {
            stack.InsertRange(0, nodes);
        }
        public INode Pop()
        {
            var top = stack[0];
            stack.RemoveAt(0);
            return top;
        }
        public int Count => stack.Count;
    }
}
