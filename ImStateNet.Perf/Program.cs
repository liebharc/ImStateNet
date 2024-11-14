using ImStateNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;


public class Program
{
    public static async Task Main()
    {
        var startTime = Stopwatch.StartNew();

        var builder = new StateBuilder();
        var nodes = new List<AbstractNode<long>>();
        var random = new Random(12312421);

        for (int i = 0; i < 1000; i++)
        {
            if (i % 5 == 0)
            {
                nodes.Add(builder.AddInput(new InputNode<long>($"input-{i}"), (long)(i % 10)));
            }
            else if (i % 5 == 1)
            {
                nodes.Add(builder.AddInput(new InputNode<long>($"input-{i}"), (long)(i + 1) % 10));
            }
            else if (i % 5 == 2)
            {
                var randomInput = (AbstractNode<long>)nodes[random.Next(nodes.Count)];
                var incrementNode = new LambdaCalcNode<long>(x => x[0] + random.Next(1, 11), new[] { randomInput }, $"lambda-{i}");
                nodes.Add(builder.AddCalculation(incrementNode));
            }
            else if (i % 5 == 3)
            {
                var randomInputs = Enumerable.Range(0, random.Next(1, 6)).Select(_ => nodes[random.Next(nodes.Count)]).ToArray();
                nodes.Add(builder.AddCalculation(new SumNode<long>(randomInputs, $"sum-{i}")));
            }
            else
            {
                var randomInputs = Enumerable.Range(0, random.Next(1, 6)).Select(_ => nodes[random.Next(nodes.Count)]).ToArray();
                nodes.Add(builder.AddCalculation(new ProductNode<long>(randomInputs, $"product-{i}")));
            }
        }

        var inputs = nodes.OfType<InputNode<long>>().ToDictionary(node => node, _ => random.Next(0, 101));
        var state = builder.Build();

        startTime.Stop();
        Console.WriteLine($"Setup time: {startTime.Elapsed.TotalSeconds} seconds");

        startTime.Restart();

        int batchSize = 20;
        int numberOfCommits = 0;
        int change = 0;

        foreach (var (node, value) in inputs)
        {
            state = state.ChangeValue(node, value);
            change++;
            if (change >= batchSize)
            {
                (state, _) = await state.Commit();
                numberOfCommits++;
                change = 0;
            }
        }

        startTime.Stop();
        Console.WriteLine($"Update time: {startTime.Elapsed.TotalSeconds} seconds for {numberOfCommits} commits");
    }
}
