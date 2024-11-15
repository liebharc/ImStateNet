namespace ImStateNet.Extensions
{
    using System.Collections.Generic;
    using ImStateNet.Core;

    public abstract class BinaryCalcNode<TOUT, TI1, TI2> : DerivedNode<TOUT>
    {
        protected BinaryCalcNode(AbstractNode<TI1> dependency1, AbstractNode<TI2> dependency2, string name = null)
            : base(new List<INode> { dependency1, dependency2 }, name) { }

        public override TOUT Calculate(IReadOnlyList<object> inputs)
        {
            return _calculation((TI1)inputs[0], (TI2)inputs[1]);
        }

        protected abstract TOUT _calculation(TI1 value1, TI2 value2);
    }

}
