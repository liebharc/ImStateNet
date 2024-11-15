namespace ImStateNet.Extensions
{
    using ImStateNet.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class LambdaCalcNode<T> : DerivedNode<T>
    {
        private readonly Func<List<T>, T> _calculation;

        public LambdaCalcNode(Func<List<T>, T> calculation, IReadOnlyList<INode> dependencies, string? name = null)
            : base(dependencies, name)
        {
            _calculation = calculation;
        }

        public override T? Calculate(IReadOnlyList<object?> inputs)
        {
            return _calculation(inputs.Cast<T>().ToList());
        }
    }
}
