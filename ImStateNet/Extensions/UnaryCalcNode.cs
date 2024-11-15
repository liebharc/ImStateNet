namespace ImStateNet.Extensions
{
    using System.Collections.Generic;
    using ImStateNet.Core;

    public abstract class UnaryCalcNode<TOUT, TI1> : DerivedNode<TOUT>
    {
        protected UnaryCalcNode(AbstractNode<TI1> dependency, string name = null)
            : base(new List<INode> { dependency }, name) { }

        public override TOUT Calculate(IReadOnlyList<object> inputs)
        {
            return _calculation((TI1)inputs[0]);
        }

        protected abstract TOUT _calculation(TI1 value);
    }

}
