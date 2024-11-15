namespace ImStateNet.Core
{
    using System;
    using System.Collections.Generic;

    public interface INode
    {
        void OnBuild();
        string Name { get; }
        bool AreValuesEqual(object? value1, object? value2);
    }

    public abstract class AbstractNode<T> : INode
    {
        protected string _name;

        protected AbstractNode(string? name = null)
        {
            _name = name ?? GetType().Name + " " + Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Called when the state is built, a node can be part of multiple states.
        /// </summary>
        public virtual void OnBuild()
        {
        }

        public string Name => _name;

        /// <summary>
        /// Compares two values and returns true if they are equal.
        /// Can be overridden by subclasses to provide a custom comparison method, e.g., by using a tolerance for floats.
        /// </summary>
        public virtual bool AreValuesEqual(T? value1, T? value2)
        {
            return EqualityComparer<T>.Default.Equals(value1, value2);
        }

        public override string ToString() => _name;

        public sealed override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public sealed override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        bool INode.AreValuesEqual(object? value1, object ?value2)
        {
            return AreValuesEqual((T?)value1, (T?)value2);
        }
    }

    public interface IInputNode : INode
    {
        object? Validate(object? value);
    }

    public class InputNode<T> : AbstractNode<T>, IInputNode
    {
        public InputNode(string? name = null) : base(name) { }

        /// <summary>
        /// Validates the value before setting it. It can coerce the value to a valid one or throw an exception if the value is invalid.
        /// </summary>
        public virtual T? Validate(T? value)
        {
            return value;
        }

        object? IInputNode.Validate(object? value)
        {
            return Validate((T?)value);
        }
    }

    public interface IDerivedNode : INode
    {
        IReadOnlyList<INode> Dependencies { get; }

        bool IsLazy { get; }

        object? Calculate(IReadOnlyList<object?> inputs);
    }

    public abstract class DerivedNode<T> : AbstractNode<T>, IDerivedNode
    {
        private static bool AnyLazyDependencies(IReadOnlyList<INode> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                if (dependency is IDerivedNode derivedNode && derivedNode.IsLazy)
                {
                    return true;
                }
            }

            return false;
        }

        protected IReadOnlyList<INode> _dependencies;

        protected DerivedNode(IReadOnlyList<INode> dependencies, string? name = null) : base(name)
        {
            _dependencies = dependencies;
            IsLazy = AnyLazyDependencies(dependencies);
        }

        public IReadOnlyList<INode> Dependencies => _dependencies;

        public bool IsLazy { get; init; }

        /// <summary>
        /// Calculates the value of the node based on the inputs.
        /// The caller guarantees that the inputs are in the same order
        /// as the dependencies.
        /// </summary>
        public abstract T? Calculate(IReadOnlyList<object?> inputs);

        object? IDerivedNode.Calculate(IReadOnlyList<object?> inputs)
        {
            return Calculate(inputs);
        }
    }
}
