# ImStateNet



Small example on how to create a persistent graph of state or calculation nodes in Python:

An earlier Python version can be found at https://github.com/liebharc/im_state_net/

```csharp
var builder = new StateBuilder();
var val1 = builder.AddInput(new InputNode<int>(), 1);
var val2 = builder.AddInput(new InputNode<int>(), 2);
var result = builder.AddCalculation(new LambdaCalcNode<int>(x => x[0] + x[1], new[] { val1, val2 }));
var state = builder.Build();

Assert.AreEqual(3, state.GetValue(result));

state = state.ChangeValue(val1, 2);

// Changes are detected
Assert.IsFalse(state.IsConsistent());

// The state detects if changes get reverted
state = state.ChangeValue(val1, 1);
Assert.IsTrue(state.IsConsistent());

// or calculates derived values on commit
(state, var changes) = state.ChangeValue(val1, 2).Commit();
Assert.IsTrue(state.IsConsistent());
Assert.AreEqual(4, state.GetValue(result));
CollectionAssert.AreEquivalent(new INode[] { val1, result }, changes);
```

Use Case: This solution is particularly beneficial when dealing with settings that are interdependent and time-consuming to apply. This could be due to the need to transmit them to hardware, which then adjusts electrical or mechanical parameters. In such scenarios, the overhead associated with this solution is justified by the reduction in the number of changes required.

Advantages:

- Detects changes and also allows to revert them of changes.
- Allows to combine multiple changes into a single commit.
- As the state is immutable, you can always work with the previous clean state while calculations are pending.
- Thread safe and lock free (with the exception of lazy calculations).
- Calculations run in parallel by default.
- Calculations can be lazy.

Disadvantages:

- Adds complexity and increases execution time.
