using GraphLibrary.Helpers;
using GraphLibrary.Model;
using System.Diagnostics;

namespace IHGEAlgorithm
{
    class Program
    {

        static void Main(string[] args)
        {
            // Check for test mode
            if (args.Length > 0 && args[0] == "--test")
            {
                var testRunner = new TestRunner(); // upewnij się, że masz klasę TestRunner
                string testDirectory = args.Length > 1 ? args[1] : "testy";
                testRunner.RunAllTests(testDirectory);
                return;
            }

            // Check for quiet mode
            bool quiet = args.Contains("--quiet");
            args = args.Where(a => a != "--quiet").ToArray();



            if (args.Length == 0)
            {
                if (!quiet)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  IHGEAlgorithm <input_file_path> [--quiet]    - Run single test");
                    Console.WriteLine("  IHGEAlgorithm --test [directory]            - Run all tests");
                    Console.WriteLine();
                    Console.WriteLine("Examples:");
                    Console.WriteLine("  IHGEAlgorithm input.txt");
                    Console.WriteLine("  IHGEAlgorithm --quiet input.txt");
                    Console.WriteLine("  IHGEAlgorithm --test");
                    Console.WriteLine("  IHGEAlgorithm --test testy");
                }
                return;
            }

            string inputPath = args[0];

            if (!File.Exists(inputPath))
            {
                if (!quiet)
                    Console.WriteLine($"Error: File '{inputPath}' not found.");
                return;
            }

            try
            {
                // Read graphs from file
                var (g1, g2) = GraphHelpers.FromFile(inputPath, out int copies);

                if (!quiet)
                {
                    Console.WriteLine("=== Input Data ===");
                    Console.WriteLine($"Pattern Graph G1 (vertices: {g1.VertexCount}, edges: {g1.EdgeCount})");
                    Console.WriteLine($"  Graph type: {(g1.IsDirected ? "Directed" : "Undirected")}");
                    Console.WriteLine($"Target Graph G2 (vertices: {g2.VertexCount}, edges: {g2.EdgeCount})");
                    Console.WriteLine($"  Graph type: {(g2.IsDirected ? "Directed" : "Undirected")}");
                    Console.WriteLine($"Required copies: {copies}");
                    Console.WriteLine();
                }

                // Create LIHGE solver
                var solver = new MultiIHGESolver(g1, g2);

                // Measure execution time
                var stopwatch = Stopwatch.StartNew();

                // Find k copies of G1 in G2 with minimal extensions
                var result = solver.FindKCopies(k: copies);

                stopwatch.Stop();

                // QUIET MODE → only print COST and exit
                if (quiet)
                {
                    Console.WriteLine($"COST={result.TotalCost}");
                    return;
                }

                // Normal verbose output
                Console.WriteLine("=== Results for multiple copies ===");
                Console.WriteLine($"Total cost of extension: {result.TotalCost}");
                Console.WriteLine($"Added vertices: {result.AddedVertices}");
                Console.WriteLine($"Added edges (cost): {result.AddedEdges}");
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
                            string direction = edge.IsDirected ? "->" : "<->";
                            int cost = edge.IsDirected ? 1 : 2;
                            Console.WriteLine($"    {edge.From} {direction} {edge.To} (cost: {cost})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!quiet)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }
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
            
            // Calculate cost:
            // - Each vertex costs 1
            // - Each directed edge (IsDirected=true) costs 1
            // - Each undirected edge (IsDirected=false) costs 2
            int edgeCost = 0;
            foreach (var edge in addedEdges)
            {
                edgeCost += edge.IsDirected ? 1 : 2;
            }
            
            Cost = addedVertices.Count + edgeCost;
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
            
            // Count total edge cost (considering directed vs undirected)
            var seenEdges = new HashSet<string>();
            int totalEdgeCost = 0;
            
            foreach (var mapping in mappings)
            {
                foreach (var edge in mapping.AddedEdges)
                {
                    string edgeKey = edge.IsDirected 
                        ? $"{edge.From}->{edge.To}"
                        : string.Join("-", new[] { edge.From, edge.To }.OrderBy(v => v));
                    
                    if (seenEdges.Add(edgeKey))
                    {
                        totalEdgeCost += edge.IsDirected ? 1 : 2;
                    }
                }
            }
            
            AddedEdges = totalEdgeCost;
        }
    }


}