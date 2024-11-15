namespace ImStateNet.Extensions
{
    using System.Collections.Generic;
    using ImStateNet.Core;

    public abstract class QuinaryCalcNode<TOUT, TI1, TI2, TI3, TI4, TI5> : DerivedNode<TOUT>
    {
        protected QuinaryCalcNode(AbstractNode<TI1> dependency1, AbstractNode<TI2> dependency2, AbstractNode<TI3> dependency3, AbstractNode<TI4> dependency4, AbstractNode<TI5> dependency5, string name = null)
            : base(new List<INode> { dependency1, dependency2, dependency3, dependency4, dependency5 }, name) { }

        public override TOUT Calculate(IReadOnlyList<object> inputs)
        {
            return _calculation((TI1)inputs[0], (TI2)inputs[1], (TI3)inputs[2], (TI4)inputs[3], (TI5)inputs[4]);
        }

        protected abstract TOUT _calculation(TI1 value1, TI2 value2, TI3 value3, TI4 value4, TI5 value5);
    }

}
