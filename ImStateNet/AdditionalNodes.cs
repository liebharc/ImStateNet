namespace ImStateNet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class LambdaCalcNode<T> : DerivedNode<T>
    {
        private readonly Func<List<T>, T> _calculation;

        public LambdaCalcNode(Func<List<T>, T> calculation, IReadOnlyList<INode> dependencies, string name = null)
            : base(dependencies, name)
        {
            _calculation = calculation;
        }

        public override T Calculate(IReadOnlyList<object> inputs)
        {
            return _calculation(inputs.Cast<T>().ToList());
        }
    }

    public class SumNode<U> : DerivedNode<U> where U : struct
    {
        public SumNode(IReadOnlyList<AbstractNode<U>> dependencies, string name = null)
            : base(dependencies.Cast<INode>().ToList(), name) { }

        public override U Calculate(IReadOnlyList<object> inputs)
        {
            return (U)Convert.ChangeType(inputs.Cast<U>().Sum(x => Convert.ToInt64(x)), typeof(U));
        }
    }

    public class ProductNode<U> : DerivedNode<U> where U : struct
    {
        public ProductNode(IReadOnlyList<AbstractNode<U>> dependencies, string name = null)
            : base(dependencies.Cast<INode>().ToList(), name) { }

        public override U Calculate(IReadOnlyList<object> inputs)
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

    public class PlaceholderNode<U> : DerivedNode<U> where U : struct
    {
        private DerivedNode<U> _node;

        public PlaceholderNode() : base(new List<INode>()) { }

        public void Assign(DerivedNode<U> node)
        {
            if (_node != null)
                throw new InvalidOperationException("Placeholder node has already been assigned a value");

            _dependencies = node.Dependencies.ToList();
            _name = node.Name;
            _node = node;
        }

        public override U Calculate(IReadOnlyList<object> inputs)
        {
            if (_node == null)
                throw new InvalidOperationException("Placeholder node has not been assigned a value yet");

            return _node.Calculate(inputs);
        }

        public override void OnBuild()
        {
            if (_node == null)
                throw new InvalidOperationException("Placeholder node has not been assigned a value yet");

            _node.OnBuild();
        }
    }

    public class NumericMinMaxNode<U> : InputNode<U> where U : struct, IComparable<U>
    {
        private readonly U _minValue;
        private readonly U _maxValue;

        public NumericMinMaxNode(U minValue, U maxValue, string? name = null) : base(name)
        {
            _minValue = minValue;
            _maxValue = maxValue;
        }

        public U MinValue => _minValue;
        public U MaxValue => _maxValue;

        public override U Validate(U value)
        {
            if (value.CompareTo(_minValue) < 0)
                return _minValue;
            else if (value.CompareTo(_maxValue) > 0)
                return _maxValue;
            return value;
        }
    }

}
