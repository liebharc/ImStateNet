namespace ImStateNet.Extensions
{
    using System.Collections.Generic;
    using ImStateNet.Core;

    public abstract class TernaryCalcNode<TOUT, TI1, TI2, TI3> : DerivedNode<TOUT>
    {
        protected TernaryCalcNode(AbstractNode<TI1> dependency1, AbstractNode<TI2> dependency2, AbstractNode<TI3> dependency3, string? name = null)
            : base(new List<INode> { dependency1, dependency2, dependency3 }, name) { }

        public override TOUT Calculate(IReadOnlyList<object?> inputs)
        {
            return Calculation((TI1?)inputs[0], (TI2?)inputs[1], (TI3?)inputs[2]);
        }

        protected abstract TOUT Calculation(TI1? value1, TI2? value2, TI3? value3);
    }
}
