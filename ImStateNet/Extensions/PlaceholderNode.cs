namespace ImStateNet.Extensions
{
    using ImStateNet.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class PlaceholderNode<U> : DerivedNode<U> where U : struct
    {
        private DerivedNode<U>? _node;

        public PlaceholderNode() : base(new List<INode>()) { }

        public void Assign(DerivedNode<U> node)
        {
            if (_node != null)
                throw new InvalidOperationException("Placeholder node has already been assigned a value");

            _dependencies = node.Dependencies.ToList();
            _name = node.Name;
            _node = node;
        }

        public override U Calculate(IReadOnlyList<object?> inputs)
        {
            if (_node == null)
                throw new InvalidOperationException("Placeholder node has not been assigned a value yet");

            return _node.Calculate(inputs);
        }

        public override void OnBuild()
        {
            if (_node == null)
                throw new InvalidOperationException("Placeholder node has not been assigned a value yet");

            _node.OnBuild();
        }
    }

}
