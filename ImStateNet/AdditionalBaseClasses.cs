namespace ImStateNet
{
    using System.Collections.Generic;

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

    public abstract class TernaryCalcNode<TOUT, TI1, TI2, TI3> : DerivedNode<TOUT>
    {
        protected TernaryCalcNode(AbstractNode<TI1> dependency1, AbstractNode<TI2> dependency2, AbstractNode<TI3> dependency3, string name = null)
            : base(new List<INode> { dependency1, dependency2, dependency3 }, name) { }

        public override TOUT Calculate(IReadOnlyList<object> inputs)
        {
            return _calculation((TI1)inputs[0], (TI2)inputs[1], (TI3)inputs[2]);
        }

        protected abstract TOUT _calculation(TI1 value1, TI2 value2, TI3 value3);
    }

    public abstract class QuaternaryCalcNode<TOUT, TI1, TI2, TI3, TI4> : DerivedNode<TOUT>
    {
        protected QuaternaryCalcNode(AbstractNode<TI1> dependency1, AbstractNode<TI2> dependency2, AbstractNode<TI3> dependency3, AbstractNode<TI4> dependency4, string name = null)
            : base(new List<INode> { dependency1, dependency2, dependency3, dependency4 }, name) { }

        public override TOUT Calculate(IReadOnlyList<object> inputs)
        {
            return _calculation((TI1)inputs[0], (TI2)inputs[1], (TI3)inputs[2], (TI4)inputs[3]);
        }

        protected abstract TOUT _calculation(TI1 value1, TI2 value2, TI3 value3, TI4 value4);
    }

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
