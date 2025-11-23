// Main LIHGE solver (Layered Iterative Hungarian Graph Extension)
using GraphLibrary.Model;

public class LIHGESolver
{
    private readonly Graph<int> g1; // Pattern graph
    private readonly Graph<int> g2; // Target graph
    private readonly bool isDirected;
    private int nextPlaceholderId;
    private readonly int maxIterations = 300;
    private double gamma = 10.0; // Diversity penalty parameter

    public LIHGESolver(Graph<int> g1, Graph<int> g2)
    {
        this.g1 = g1;
        this.g2 = g2;
        this.isDirected = g1.IsDirected;

        // Start placeholder IDs after the maximum vertex ID in G2
        this.nextPlaceholderId = g2.Vertices.Any() ? g2.Vertices.Max() + 1 : 1000;
    }

    public SearchResult FindKCopies(int k)
    {
        var v1List = g1.Vertices.ToList();
        int n1 = v1List.Count;

        // Build list of candidate vertices (V2 + placeholders for all layers)
        var candidates = new List<int>();

        // Add vertices from G2
        foreach (var v in g2.Vertices)
        {
            candidates.Add(v);
        }

        // Add placeholders for all k layers
        for (int layer = 0; layer < k; layer++)
        {
            for (int i = 0; i < n1; i++)
            {
                candidates.Add(nextPlaceholderId + layer * n1 + i);
            }
        }

        // Initialize k layers of mappings
        var layers = new List<Dictionary<int, int>>();
        for (int r = 0; r < k; r++)
        {
            layers.Add(new Dictionary<int, int>());
        }

        // Iterative refinement
        bool changed = true;
        int iteration = 0;

        while (changed && iteration < maxIterations)
        {
            iteration++;
            changed = false;

            // Process each layer
            for (int r = 0; r < k; r++)
            {
                var oldPhi = new Dictionary<int, int>(layers[r]);

                // Build cost matrix for this layer
                var costMatrix = BuildLayerCostMatrix(v1List, candidates, layers, r);

                // Run Hungarian algorithm
                var hungarian = new HungarianAlgorithm(costMatrix);
                var assignment = hungarian.Solve();

                // Update mapping for this layer
                var newPhi = new Dictionary<int, int>();
                for (int i = 0; i < n1; i++)
                {
                    newPhi[v1List[i]] = candidates[assignment[i]];
                }

                // Check if this layer is identical to any previous layer
                bool needsForcedDifference = false;
                for (int s = 0; s < r; s++)
                {
                    if (AreLayersIdentical(newPhi, layers[s]))
                    {
                        needsForcedDifference = true;
                        break;
                    }
                }

                // Apply forced-difference if needed
                if (needsForcedDifference)
                {
                    newPhi = ApplyForcedDifference(v1List, candidates, layers, r, costMatrix);
                }

                // Check if mapping changed
                if (!MappingsEqual(oldPhi, newPhi))
                {
                    changed = true;
                }

                layers[r] = newPhi;
            }
        }

        // Merge placeholders with identical topological function
        var mergedLayers = MergePlaceholders(layers, v1List);

        // Convert layers to Mapping objects
        var mappings = new List<Mapping>();
        var globalAddedVertices = new HashSet<int>();
        var globalAddedEdges = new HashSet<string>();

        foreach (var phi in mergedLayers)
        {
            var addedVertices = new HashSet<int>();
            var addedEdges = new List<Edge<int>>();

            // Identify added vertices
            foreach (var v in phi.Values)
            {
                if (!g2.ContainsVertex(v))
                {
                    addedVertices.Add(v);
                    globalAddedVertices.Add(v);
                }
            }

            // Identify added edges
            foreach (var edge in g1.GetAllEdges())
            {
                int u = phi[edge.From];
                int v = phi[edge.To];

                if (!g2.HasEdge(u, v))
                {
                    addedEdges.Add(new Edge<int>(u, v, 1.0, isDirected));

                    string edgeKey = isDirected
                        ? $"{u}->{v}"
                        : string.Join("-", new[] { u, v }.OrderBy(x => x));
                    globalAddedEdges.Add(edgeKey);
                }
            }

            mappings.Add(new Mapping(phi, addedVertices, addedEdges));
        }

        // Calculate total cost
        int totalCost = CalculateTotalCost(mappings);

        return new SearchResult(mappings, totalCost);
    }

    private double[,] BuildLayerCostMatrix(List<int> v1List, List<int> candidates,
        List<Dictionary<int, int>> layers, int currentLayer)
    {
        int n1 = v1List.Count;
        int nCandidates = candidates.Count;
        var costMatrix = new double[n1, nCandidates];
        var phi = layers[currentLayer];

        for (int i = 0; i < n1; i++)
        {
            int u = v1List[i];

            for (int j = 0; j < nCandidates; j++)
            {
                int c = candidates[j];
                double cost = 0;

                // Cost for using a placeholder
                if (!g2.ContainsVertex(c))
                {
                    cost += 1.0;
                }

                // Cost for missing outgoing edges: (u, t) in E1
                foreach (var (t, _) in g1.GetNeighbors(u))
                {
                    if (phi.ContainsKey(t))
                    {
                        int phiT = phi[t];
                        if (!g2.HasEdge(c, phiT))
                        {
                            cost += 1.0;
                        }
                    }
                    else
                    {
                        // Pessimistic assumption: edge doesn't exist
                        cost += 1.0;
                    }
                }

                // Cost for missing incoming edges: (t, u) in E1
                foreach (var t in GetIncomingNeighbors(g1, u))
                {
                    if (phi.ContainsKey(t))
                    {
                        int phiT = phi[t];
                        if (!g2.HasEdge(phiT, c))
                        {
                            cost += 1.0;
                        }
                    }
                    else
                    {
                        // Pessimistic assumption: edge doesn't exist
                        cost += 1.0;
                    }
                }

                // Diversity penalty removed - handled by forced-difference instead

                costMatrix[i, j] = cost;
            }
        }

        return costMatrix;
    }

    private Dictionary<int, int> ApplyForcedDifference(List<int> v1List, List<int> candidates,
        List<Dictionary<int, int>> layers, int currentLayer, double[,] baseCostMatrix)
    {
        int n1 = v1List.Count;
        int nCandidates = candidates.Count;

        // Collect all vertex SETS used in previous layers
        var usedSets = new List<HashSet<int>>();
        for (int s = 0; s < currentLayer; s++)
        {
            var vertexSet = layers[s].Values.Where(v => g2.ContainsVertex(v)).ToHashSet();
            usedSets.Add(vertexSet);
        }

        // Try all possible combinations until we find a different set
        Dictionary<int, int>? bestAlternative = null;
        double bestCost = double.MaxValue;

        // Generate all possible mappings and evaluate them
        var allMappings = GenerateAllMappings(v1List, candidates, n1, g2.VertexCount);

        foreach (var mapping in allMappings)
        {
            // Check if this mapping creates a different vertex set
            var vertexSet = mapping.Values.Where(v => g2.ContainsVertex(v)).ToHashSet();
            bool isDifferent = true;

            foreach (var usedSet in usedSets)
            {
                if (vertexSet.SetEquals(usedSet))
                {
                    isDifferent = false;
                    break;
                }
            }

            if (!isDifferent) continue;

            // Calculate cost of this mapping
            double cost = 0;
            foreach (var (v1, v2) in mapping)
            {
                if (!g2.ContainsVertex(v2))
                {
                    cost += 1.0; // Placeholder cost
                }
            }

            // Add cost for missing edges
            foreach (var edge in g1.GetAllEdges())
            {
                int u = mapping[edge.From];
                int v = mapping[edge.To];

                if (!g2.HasEdge(u, v))
                {
                    cost += isDirected ? 1.0 : 2.0;
                }
            }

            if (cost < bestCost)
            {
                bestCost = cost;
                bestAlternative = mapping;
            }
        }

        if (bestAlternative != null)
        {
            Console.WriteLine($"  Forced difference found alternative with cost {bestCost}");
            return bestAlternative;
        }

        // Last resort: use placeholders
        Console.WriteLine($"  Forced difference failed, using placeholders");
        var placeholderPhi = new Dictionary<int, int>();
        int placeholderStart = nextPlaceholderId + currentLayer * n1;
        for (int i = 0; i < n1; i++)
        {
            placeholderPhi[v1List[i]] = placeholderStart + i;
        }

        return placeholderPhi;
    }

    private List<Dictionary<int, int>> GenerateAllMappings(List<int> v1List, List<int> candidates, int n1, int v2Count)
    {
        var result = new List<Dictionary<int, int>>();

        // Only consider vertices from G2 (not placeholders) for efficiency
        var v2Candidates = candidates.Take(v2Count).ToList();

        // If there are too many combinations, sample them
        if (v2Candidates.Count > 10 || n1 > 5)
        {
            // For large graphs, just try a few promising combinations
            return GenerateGreedyMappings(v1List, v2Candidates, n1);
        }

        // Generate all permutations for small graphs
        GeneratePermutations(v1List, v2Candidates, 0, new Dictionary<int, int>(), result);

        return result;
    }

    private void GeneratePermutations(List<int> v1List, List<int> v2Candidates, int index,
        Dictionary<int, int> current, List<Dictionary<int, int>> result)
    {
        if (index == v1List.Count)
        {
            result.Add(new Dictionary<int, int>(current));
            return;
        }

        // Get already used vertices in current mapping
        var usedVertices = current.Values.ToHashSet();

        foreach (var candidate in v2Candidates)
        {
            // Skip if this candidate is already used in current mapping
            if (usedVertices.Contains(candidate))
                continue;

            current[v1List[index]] = candidate;
            GeneratePermutations(v1List, v2Candidates, index + 1, current, result);
            current.Remove(v1List[index]);
        }
    }

    private List<Dictionary<int, int>> GenerateGreedyMappings(List<int> v1List, List<int> v2Candidates, int n1)
    {
        var result = new List<Dictionary<int, int>>();

        // Try a few random combinations
        var random = new Random(42);
        int sampleCount = Math.Min(100, (int)Math.Pow(v2Candidates.Count, Math.Min(n1, 3)));

        for (int i = 0; i < sampleCount; i++)
        {
            var mapping = new Dictionary<int, int>();
            var shuffled = v2Candidates.OrderBy(x => random.Next()).ToList();

            for (int j = 0; j < n1; j++)
            {
                mapping[v1List[j]] = shuffled[j % shuffled.Count];
            }

            result.Add(mapping);
        }

        return result;
    }

    private bool AreLayersIdentical(Dictionary<int, int> phi1, Dictionary<int, int> phi2)
    {
        // Two layers are identical if their mappings to V2 (non-placeholder vertices) are the same
        var v2Vertices1 = phi1.Values.Where(v => g2.ContainsVertex(v)).ToHashSet();
        var v2Vertices2 = phi2.Values.Where(v => g2.ContainsVertex(v)).ToHashSet();

        return v2Vertices1.SetEquals(v2Vertices2);
    }

    private List<Dictionary<int, int>> MergePlaceholders(List<Dictionary<int, int>> layers, List<int> v1List)
    {
        // Build a mapping from placeholder to its topological signature (neighbors)
        var placeholderSignatures = new Dictionary<int, HashSet<int>>();

        foreach (var layer in layers)
        {
            foreach (var (v1, v2) in layer)
            {
                if (!g2.ContainsVertex(v2) && !placeholderSignatures.ContainsKey(v2))
                {
                    var neighbors = new HashSet<int>();

                    // Find all neighbors of this placeholder in any layer
                    foreach (var otherLayer in layers)
                    {
                        foreach (var edge in g1.GetAllEdges())
                        {
                            if (otherLayer[edge.From] == v2)
                            {
                                neighbors.Add(otherLayer[edge.To]);
                            }
                            if (otherLayer[edge.To] == v2)
                            {
                                neighbors.Add(otherLayer[edge.From]);
                            }
                        }
                    }

                    placeholderSignatures[v2] = neighbors;
                }
            }
        }

        // Build merge mapping: placeholder -> representative
        var mergeMap = new Dictionary<int, int>();
        var processed = new HashSet<int>();

        foreach (var (p1, sig1) in placeholderSignatures)
        {
            if (processed.Contains(p1)) continue;

            mergeMap[p1] = p1; // Map to itself initially
            processed.Add(p1);

            // Find all placeholders with identical signature
            foreach (var (p2, sig2) in placeholderSignatures)
            {
                if (p1 != p2 && !processed.Contains(p2) && sig1.SetEquals(sig2))
                {
                    mergeMap[p2] = p1; // Merge p2 into p1
                    processed.Add(p2);
                }
            }
        }

        // Apply merging to all layers
        var mergedLayers = new List<Dictionary<int, int>>();
        foreach (var layer in layers)
        {
            var mergedLayer = new Dictionary<int, int>();
            foreach (var (v1, v2) in layer)
            {
                if (mergeMap.ContainsKey(v2))
                {
                    mergedLayer[v1] = mergeMap[v2];
                }
                else
                {
                    mergedLayer[v1] = v2;
                }
            }
            mergedLayers.Add(mergedLayer);
        }

        return mergedLayers;
    }

    private IEnumerable<int> GetIncomingNeighbors(Graph<int> graph, int vertex)
    {
        var incoming = new HashSet<int>();

        foreach (var v in graph.Vertices)
        {
            if (graph.HasEdge(v, vertex))
            {
                incoming.Add(v);
            }
        }

        return incoming;
    }

    private bool MappingsEqual(Dictionary<int, int> phi1, Dictionary<int, int> phi2)
    {
        if (phi1.Count != phi2.Count) return false;

        foreach (var kvp in phi1)
        {
            if (!phi2.TryGetValue(kvp.Key, out int value) || value != kvp.Value)
            {
                return false;
            }
        }

        return true;
    }

    private int CalculateTotalCost(List<Mapping> mappings)
    {
        var allAddedVertices = new HashSet<int>();
        var allAddedEdges = new HashSet<string>();

        foreach (var mapping in mappings)
        {
            foreach (var v in mapping.AddedVertices)
            {
                allAddedVertices.Add(v);
            }

            foreach (var edge in mapping.AddedEdges)
            {
                string edgeKey = edge.IsDirected
                    ? $"{edge.From}->{edge.To}"
                    : string.Join("-", new[] { edge.From, edge.To }.OrderBy(v => v));
                allAddedEdges.Add(edgeKey);
            }
        }

        int vertexCost = allAddedVertices.Count;
        int edgeCost = 0;

        foreach (var edgeKey in allAddedEdges)
        {
            edgeCost += edgeKey.Contains("->") ? 1 : 2;
        }

        return vertexCost + edgeCost;
    }
}
