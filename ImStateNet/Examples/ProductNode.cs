namespace ImStateNet.Examples
{
    using ImStateNet.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class ProductNode<U> : DerivedNode<U> where U : struct
    {
        public ProductNode(IReadOnlyList<AbstractNode<U>> dependencies, string? name = null)
            : base(dependencies.Cast<INode>().ToList(), name) { }

        public override U Calculate(IReadOnlyList<object?> inputs)
        {
            U result = (U)Convert.ChangeType(1, typeof(U));
            foreach (var value in inputs.Cast<U>())
            {
                unchecked
                {
                    result = (U)Convert.ChangeType(Convert.ToInt64(result) * Convert.ToInt64(value), typeof(U));
                }
            }
            return result;
        }
    }
}
