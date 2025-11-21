using GraphLibrary.Model;
using GraphLibrary.Helpers;
using System.Diagnostics;
using ExactAlgorythm;

// Read input graphs from file
if (args.Length == 0)
{
    Console.WriteLine("Usage: ExactAlgorythm <input_file_path>");
    Console.WriteLine("Example: ExactAlgorythm input.txt");
    return;
}

string inputPath = args[0];

if (!File.Exists(inputPath))
{
    Console.WriteLine($"Error: File '{inputPath}' not found.");
    return;
}

try
{
    // Read graphs from file
    var (g1, g2) = GraphHelpers.FromFile(inputPath, out int copies);

    Console.WriteLine("=== Input Data ===");
    Console.WriteLine($"Pattern Graph G1 (vertices: {g1.VertexCount}, edges: {g1.EdgeCount})");
    Console.WriteLine($"Target Graph G2 (vertices: {g2.VertexCount}, edges: {g2.EdgeCount})");
    Console.WriteLine($"Required copies: {copies}");
    Console.WriteLine();

    // Create VF2 solver
    var solver = new VF2Solver(g1, g2);

    // Measure execution time
    var stopwatch = Stopwatch.StartNew();

    // Find k copies of G1 in G2 with minimal extensions
    var result = solver.FindKCopies(copies);

    stopwatch.Stop();

    // Output results
    Console.WriteLine("=== Results ===");
    Console.WriteLine($"Total cost of extension: {result.TotalCost}");
    Console.WriteLine($"Added vertices: {result.AddedVertices}");
    Console.WriteLine($"Added edges: {result.AddedEdges}");
    Console.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds} ms");
    Console.WriteLine();

    Console.WriteLine($"Found {result.Mappings.Count} copies:");
    for (int i = 0; i < result.Mappings.Count; i++)
    {
        Console.WriteLine($"\nCopy {i + 1} (cost: {result.Mappings[i].Cost}):");
        Console.WriteLine("  Vertex mapping:");
        foreach (var (v1, v2) in result.Mappings[i].VertexMap)
        {
            string marker = result.Mappings[i].AddedVertices.Contains(v2) ? " (new)" : "";
            Console.WriteLine($"    {v1} -> {v2}{marker}");
        }

        if (result.Mappings[i].AddedEdges.Count > 0)
        {
            Console.WriteLine("  Added edges:");
            foreach (var edge in result.Mappings[i].AddedEdges)
            {
                Console.WriteLine($"    {edge}");
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// Represents a single mapping from G1 to G2
public class Mapping
{
    public Dictionary<int, int> VertexMap { get; }
    public HashSet<int> AddedVertices { get; }
    public List<Edge<int>> AddedEdges { get; }
    public int Cost { get; }

    public Mapping(Dictionary<int, int> vertexMap, HashSet<int> addedVertices, List<Edge<int>> addedEdges)
    {
        VertexMap = vertexMap;
        AddedVertices = addedVertices;
        AddedEdges = addedEdges;
        Cost = addedVertices.Count + addedEdges.Count;
    }
}

// Final result of the search
public class SearchResult
{
    public List<Mapping> Mappings { get; }
    public int TotalCost { get; }
    public int AddedVertices { get; }
    public int AddedEdges { get; }

    public SearchResult(List<Mapping> mappings, int totalCost)
    {
        Mappings = mappings;
        TotalCost = totalCost;
        AddedVertices = mappings.SelectMany(m => m.AddedVertices).Distinct().Count();
        AddedEdges = mappings.SelectMany(m => m.AddedEdges).Distinct().Count();
    }
}