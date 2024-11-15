namespace ImStateNet.Extensions
{
    using ImStateNet.Core;
    using System.Collections.Generic;
    using System.Linq;

    public class LazyNode<U> : DerivedNode<U> where U : struct
    {
        public LazyNode(DerivedNode<U> innerNode)
            : base(innerNode.Dependencies.Cast<INode>().ToList(), innerNode.Name)
        {
            InnerNode = innerNode;
            IsLazy = true;
        }

        public DerivedNode<U> InnerNode { get; }

        public override U Calculate(IReadOnlyList<object?> inputs)
        {
            return InnerNode.Calculate(inputs);
        }
    }

}
