﻿namespace ImStateNet.Extensions
{
    using System.Collections.Generic;
    using ImStateNet.Core;

    public abstract class UnaryCalcNode<TOUT, TI1> : DerivedNode<TOUT>
    {
        protected UnaryCalcNode(AbstractNode<TI1> dependency, string? name = null)
            : base(new List<INode> { dependency }, name) { }

        public override Task<TOUT?> Calculate(IReadOnlyList<object?> inputs)
        {
            return Calculation((TI1?)inputs[0]);
        }

        protected abstract Task<TOUT?> Calculation(TI1? value);
    }

    public class LambdaUnaryCalcNode<TOUT, TI> : UnaryCalcNode<TOUT, TI>
    {
        private readonly Func<TI?, Task<TOUT?>> _calculation;

        public LambdaUnaryCalcNode(
            AbstractNode<TI> dependency,
            Func<TI?, Task<TOUT?>> calculation,
            string? name = null)
            : base(dependency, name)
        {
            _calculation = calculation;
        }

        protected override Task<TOUT?> Calculation(TI? value)
        {
            return _calculation(value);
        }
    }
}
