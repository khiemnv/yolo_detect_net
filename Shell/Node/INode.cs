using System.Collections.Generic;

namespace Node
{
    public interface INode
    {
        INode Parent { get; }
        IEnumerable<INode> Children { get; }

        bool IsLeaf { get; }
        string Name { get; }
        string PhysicName { get; }
    }
}
