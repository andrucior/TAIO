using GraphLibrary.Model;
using GraphLibrary.Helpers;


Graph<int> exampleGraph = GraphHelpers.Graph(10);
Console.WriteLine($"Graph 1: {exampleGraph}");

Graph<int> exampleGraph2 = new(exampleGraph);
Console.WriteLine($"Graph 2 (a copy):");
Console.WriteLine(exampleGraph2.DisplayDetailedInfo());

Console.WriteLine("Graph 1 after adding edges:");
exampleGraph.AddEdge(1, 2);
exampleGraph.AddEdge(2, 4);
Console.WriteLine(exampleGraph.DisplayDetailedInfo());
