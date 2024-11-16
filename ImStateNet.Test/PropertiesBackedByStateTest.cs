namespace ImStateNet.Test
{
    using System.Collections.Generic;
    using ImStateNet.Core;
    using ImStateNet.Examples;
    using ImStateNet.Extensions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class PropertiesBackedByState
    {
        private State _state;
        private readonly InputNode<int> _val1;
        private readonly InputNode<int> _val2;
        private readonly DerivedNode<int> _result;

        public PropertiesBackedByState()
        {
            var builder = new StateBuilder();
            _val1 = builder.AddInput(new InputNode<int>("val1"), 1);
            _val2 = builder.AddInput(new NumericMinMaxNode<int>(1, 5, "val2"), 2);
            _result = builder.AddCalculation(new SumNode<int>(new List<AbstractNode<int>> { _val1, _val2 }, "result"));
            _state = builder.Build();
        }

        private T? GetValue<T>(AbstractNode<T> node) => _state.GetValue(node);

        private void SetValue<T>(InputNode<T> node, T value)
        {
            (_state, _) = _state.ChangeValue(node, value).Commit();
        }

        public int Result => GetValue(_result);

        public int Val1
        {
            get => GetValue(_val1);
            set => SetValue(_val1, value);
        }

        public int Val2 => GetValue(_val2);

        public void SetVal1AndVal2(int val1, int val2)
        {
            (_state, _) = _state.ChangeValue(_val1, val1).ChangeValue(_val2, val2).Commit();
        }
    }

    [TestClass]
    public class StateMutTests
    {
        [TestMethod]
        public void PropertiesBackedByStateTest()
        {
            var state = new PropertiesBackedByState();
            Assert.AreEqual(3, state.Result);
            Assert.AreEqual(1, state.Val1);
            Assert.AreEqual(2, state.Val2);

            state.Val1 = 3;
            Assert.AreEqual(5, state.Result);

            state.SetVal1AndVal2(4, 5);
            Assert.AreEqual(9, state.Result);
        }
    }
}