namespace ImStateNet.Extensions
{
    using ImStateNet.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class SumNode<U> : DerivedNode<U> where U : struct
    {
        public SumNode(IReadOnlyList<AbstractNode<U>> dependencies, string? name = null)
            : base(dependencies.Cast<INode>().ToList(), name) { }

        public override U Calculate(IReadOnlyList<object?> inputs)
        {
            return (U)Convert.ChangeType(inputs.Cast<U>().Sum(x => Convert.ToInt64(x)), typeof(U));
        }
    }
}
