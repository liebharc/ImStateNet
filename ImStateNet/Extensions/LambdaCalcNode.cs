namespace ImStateNet.Extensions
{
    using ImStateNet.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class LambdaCalcNode
    {
        public static LambdaUnaryCalcNode<TOUT, TI> Create<TOUT, TI>(
            AbstractNode<TI> dependency,
            Func<TI?, TOUT> calculation)
        {
            return new LambdaUnaryCalcNode<TOUT, TI>(dependency, calculation);
        }

        public static LambdaBinaryCalcNode<TOUT, TI1, TI2> Create<TOUT, TI1, TI2>(
            AbstractNode<TI1> dependency1,
            AbstractNode<TI2> dependency2,
            Func<TI1?, TI2?, TOUT> calculation)
        {
            return new LambdaBinaryCalcNode<TOUT, TI1, TI2>(dependency1, dependency2, calculation);
        }

        public static LambdaTernaryCalcNode<TOUT, TI1, TI2, TI3> Create<TOUT, TI1, TI2, TI3>(
            AbstractNode<TI1> dependency1,
            AbstractNode<TI2> dependency2,
            AbstractNode<TI3> dependency3,
            Func<TI1?, TI2?, TI3?, TOUT> calculation)
        {
            return new LambdaTernaryCalcNode<TOUT, TI1, TI2, TI3>(dependency1, dependency2, dependency3, calculation);
        }

        public static LambdaQuaternaryCalcNode<TOUT, TI1, TI2, TI3, TI4> Create<TOUT, TI1, TI2, TI3, TI4>(
            AbstractNode<TI1> dependency1,
            AbstractNode<TI2> dependency2,
            AbstractNode<TI3> dependency3,
            AbstractNode<TI4> dependency4,
            Func<TI1?, TI2?, TI3?, TI4?, TOUT> calculation)
        {
            return new LambdaQuaternaryCalcNode<TOUT, TI1, TI2, TI3, TI4>(dependency1, dependency2, dependency3, dependency4, calculation);
        }

        public static LambdaQuinaryCalcNode<TOUT, TI1, TI2, TI3, TI4, TI5> Create<TOUT, TI1, TI2, TI3, TI4, TI5>(
            AbstractNode<TI1> dependency1,
            AbstractNode<TI2> dependency2,
            AbstractNode<TI3> dependency3,
            AbstractNode<TI4> dependency4,
            AbstractNode<TI5> dependency5,
            Func<TI1?, TI2?, TI3?, TI4?, TI5?, TOUT> calculation)
        {
            return new LambdaQuinaryCalcNode<TOUT, TI1, TI2, TI3, TI4, TI5>(dependency1, dependency2, dependency3, dependency4, dependency5, calculation);
        }

        public static LambdaCalcNode<T> Create<T>(
            IReadOnlyList<AbstractNode<T>> dependencies,
            Func<IReadOnlyList<T>, T> calculation)
        {
            return new LambdaCalcNode<T>(calculation, dependencies);
        }
    }

    public class LambdaCalcNode<T> : DerivedNode<T>
    {
        private readonly Func<IReadOnlyList<T>, T> _calculation;

        public LambdaCalcNode(Func<IReadOnlyList<T>, T> calculation, IReadOnlyList<AbstractNode<T>> dependencies, string? name = null)
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
