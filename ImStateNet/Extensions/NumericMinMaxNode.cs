namespace ImStateNet.Extensions
{
    using ImStateNet.Core;
    using System;

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
