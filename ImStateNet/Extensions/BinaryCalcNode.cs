namespace ImStateNet.Extensions
{
    using System.Collections.Generic;
    using ImStateNet.Core;

    public abstract class BinaryCalcNode<TOUT, TI1, TI2> : DerivedNode<TOUT>
    {
        protected BinaryCalcNode(AbstractNode<TI1> dependency1, AbstractNode<TI2> dependency2, string? name = null)
            : base(new List<INode> { dependency1, dependency2 }, name) { }

        public override TOUT Calculate(IReadOnlyList<object?> inputs)
        {
            return Calculation((TI1?)inputs[0], (TI2?)inputs[1]);
        }

        protected abstract TOUT Calculation(TI1? value1, TI2? value2);
    }

    public class LambdaBinaryCalcNode<TOUT, TI1, TI2> : BinaryCalcNode<TOUT, TI1, TI2>
    {
        private readonly Func<TI1?, TI2?, TOUT> _calculation;

        public LambdaBinaryCalcNode(AbstractNode<TI1> dependency1, AbstractNode<TI2> dependency2, Func<TI1?, TI2?, TOUT> calculation, string? name = null)
            : base(dependency1, dependency2, name)
        {
            _calculation = calculation;
        }

        protected override TOUT Calculation(TI1? value1, TI2? value2)
        {
            return _calculation(value1, value2);
        }
    }
}
