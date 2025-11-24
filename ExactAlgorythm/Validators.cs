using GraphLibrary.Model;
using System.Collections.Generic;
using System.Linq;

namespace ExactAlgorythm
{
    /// <summary>
    /// Validator for VF2 algorithm results — option A:
    /// build global extended graph from all mappings and validate each mapping in that context.
    /// </summary>
    public static class ResultValidator
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new();
            public List<string> Warnings { get; set; } = new();

            public void AddError(string error) => Errors.Add(error);
            public void AddWarning(string warning) => Warnings.Add(warning);

            public override string ToString()
            {
                if (IsValid)
                    return "✓ Solution is VALID";

                var result = "✗ Solution is INVALID\n";
                if (Errors.Count > 0)
                {
                    result += "\nErrors:\n";
                    result += string.Join("\n", Errors.Select(e => $"  - {e}"));
                }
                if (Warnings.Count > 0)
                {
                    result += "\nWarnings:\n";
                    result += string.Join("\n", Warnings.Select(w => $"  - {w}"));
                }
                return result;
            }
        }

        /// <summary>
        /// Validates the whole search result (using option A: global extended graph built from ALL mappings).
        /// </summary>
        public static ValidationResult Validate(
            Graph<int> g1,
            Graph<int> g2,
            SearchResult result,
            int requiredCopies)
        {
            var validation = new ValidationResult();

            // Build full global extended graph from ALL mappings (this represents G'_2 after applying all mappings)
            var globalExtended = BuildExtendedGraph(g2, result.Mappings);

            // Validate mappings uniqueness (images must differ)
            ValidateMappings(result, validation);

            // Check 1: Correct number of copies
            if (result.Mappings.Count != requiredCopies)
            {
                validation.AddError(
                    $"Expected {requiredCopies} copies, but found {result.Mappings.Count}");
            }

            // Validate each mapping individually but in the context of ALL previous mappings.
            // For mapping i (1-based), the "cumulative before" graph should include mappings 0..i-2.
            for (int i = 0; i < result.Mappings.Count; i++)
            {
                // Build cumulative graph = base g2 + mappings[0..i-1)  (i.e., previous mappings only)
                var cumulativeBefore = BuildExtendedGraph(g2, result.Mappings.Take(i));
                ValidateMapping(g1, g2, cumulativeBefore, result.Mappings[i], i + 1, validation);
            }

            // Check 3: Verify total cost calculation (global)
            ValidateCostCalculation(g2, result, validation);

            // Check 4: Graph type consistency
            if (g1.IsDirected != g2.IsDirected)
            {
                validation.AddWarning(
                    "Pattern and target graphs have different directedness");
            }

            validation.IsValid = validation.Errors.Count == 0;
            return validation;
        }

        /// <summary>
        /// Validates a single mapping from G1 to G2, in the context of 'cumulativeBefore' graph
        /// which represents G2 extended by all previous mappings (but NOT including this mapping).
        /// The 'extendedForThisCopy' is built internally as cumulativeBefore + this mapping's additions.
        /// </summary>
        private static void ValidateMapping(
            Graph<int> g1,
            Graph<int> baseG2,
            Graph<int> cumulativeBefore,
            Mapping mapping,
            int copyNumber,
            ValidationResult validation)
        {
            // Build graph that includes previous mappings and this mapping's own added vertices/edges
            var extendedForThisCopy = BuildExtendedGraph(cumulativeBefore, new[] { mapping });

            // --- 1) All vertices from G1 must be mapped (mapping might map some to newly added placeholder vertices)
            if (mapping.VertexMap.Count != g1.VertexCount)
            {
                validation.AddError(
                    $"Copy {copyNumber}: Incomplete mapping - expected {g1.VertexCount} vertices, got {mapping.VertexMap.Count}");
            }

            foreach (var v1 in g1.Vertices)
            {
                if (!mapping.VertexMap.ContainsKey(v1))
                {
                    validation.AddError(
                        $"Copy {copyNumber}: Vertex {v1} from G1 is not mapped");
                }
            }

            // --- 2) Injectivity within this mapping
            var usedG2Vertices = new HashSet<int>();
            foreach (var v2 in mapping.VertexMap.Values)
            {
                if (!usedG2Vertices.Add(v2))
                {
                    validation.AddError(
                        $"Copy {copyNumber}: Vertex {v2} from G2 is used multiple times within this copy");
                }
            }

            // --- 3) For every edge (u->v) in G1, extendedForThisCopy must contain edge f(u)->f(v)
            foreach (var u in g1.Vertices)
            {
                if (!mapping.VertexMap.TryGetValue(u, out int f_u))
                    continue;

                foreach (var neighInfo in g1.GetNeighbors(u))
                {
                    int v = neighInfo.neighbor;
                    if (!mapping.VertexMap.TryGetValue(v, out int f_v))
                        continue;

                    if (!extendedForThisCopy.HasEdge(f_u, f_v))
                    {
                        validation.AddError(
                            $"Copy {copyNumber}: Edge {u}->{v} (mapped to {f_u}->{f_v}) is missing in extended G2 (considering previous mappings)");
                    }

                    // For undirected graphs, ensure the reverse exists as well
                    if (!g1.IsDirected)
                    {
                        if (!extendedForThisCopy.HasEdge(f_v, f_u))
                        {
                            validation.AddError(
                                $"Copy {copyNumber}: Reverse edge {f_v}->{f_u} is missing in undirected graph (considering previous mappings)");
                        }
                    }
                }
            }

            // --- 4) Verify that added vertices declared by this mapping are not originally present in baseG2
            foreach (var addedVertex in mapping.AddedVertices)
            {
                if (baseG2.ContainsVertex(addedVertex))
                {
                    validation.AddError(
                        $"Copy {copyNumber}: Vertex {addedVertex} marked as 'added' but exists in original G2");
                }
            }

            // --- 5) Verify added edges reference existing vertices in the global extended graph (cumulativeBefore + this mapping)
            foreach (var edge in mapping.AddedEdges)
            {
                bool fromExists = extendedForThisCopy.ContainsVertex(edge.From);
                bool toExists = extendedForThisCopy.ContainsVertex(edge.To);

                if (!fromExists)
                {
                    validation.AddError(
                        $"Copy {copyNumber}: Added edge {edge.From}->{edge.To} references non-existent vertex {edge.From}");
                }
                if (!toExists)
                {
                    validation.AddError(
                        $"Copy {copyNumber}: Added edge {edge.From}->{edge.To} references non-existent vertex {edge.To}");
                }

                // If this edge was already present in cumulativeBefore, warn (or count accordingly)
                if (cumulativeBefore.HasEdge(edge.From, edge.To) || (!edge.IsDirected && cumulativeBefore.HasEdge(edge.To, edge.From)))
                {
                    validation.AddWarning(
                        $"Copy {copyNumber}: Edge {edge.From}->{edge.To} marked as 'added' but already exists in earlier graph");
                }
            }

            // --- 6) Verify mapping cost: cost should equal number of NEW vertices added by this mapping
            // and number of NEW edges added by this mapping (directed cost=1, undirected=2),
            // where "new" is relative to cumulativeBefore.
            int newVertexCount = mapping.AddedVertices.Count(v => !cumulativeBefore.ContainsVertex(v));

            int newEdgeCost = 0;
            foreach (var edge in mapping.AddedEdges)
            {
                bool alreadyThere = cumulativeBefore.HasEdge(edge.From, edge.To)
                                    || (!edge.IsDirected && cumulativeBefore.HasEdge(edge.To, edge.From));
                if (!alreadyThere)
                {
                    newEdgeCost += edge.IsDirected ? 1 : 2;
                }
            }

            int expectedCost = newVertexCount + newEdgeCost;
            if (mapping.Cost != expectedCost)
            {
                validation.AddError(
                    $"Copy {copyNumber}: Cost mismatch - calculated {expectedCost}, reported {mapping.Cost}");
            }
        }

    

        /// <summary>
        /// Builds an extended graph by applying a sequence of mappings on top of base graph g2.
        /// Each mapping may add vertices and edges. Mappings are applied in order.
        /// </summary>
        private static Graph<int> BuildExtendedGraph(Graph<int> baseG2, IEnumerable<Mapping> mappings)
        {
            // Start with a copy of baseG2
            var extended = new Graph<int>(baseG2.IsDirected);

            // Copy vertices
            foreach (var v in baseG2.Vertices)
                extended.AddVertex(v);

            // Copy edges (respecting directionality)
            foreach (var v in baseG2.Vertices)
            {
                foreach (var nei in baseG2.GetNeighbors(v))
                {
                    // Add edge v -> nei.neighbor if not already added
                    if (!extended.HasEdge(v, nei.neighbor))
                        extended.AddEdge(v, nei.neighbor);
                }
            }

            // Apply mappings in order (they may add vertices and edges)
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    // Add declared added vertices
                    foreach (var newV in mapping.AddedVertices)
                    {
                        if (!extended.ContainsVertex(newV))
                            extended.AddVertex(newV);
                    }

                    // Add declared added edges (directed/undirected logic depends on Edge.IsDirected)
                    foreach (var e in mapping.AddedEdges)
                    {
                        if (e.IsDirected)
                        {
                            if (!extended.HasEdge(e.From, e.To))
                                extended.AddEdge(e.From, e.To);
                        }
                        else
                        {
                            if (!extended.HasEdge(e.From, e.To))
                                extended.AddEdge(e.From, e.To);
                            if (!extended.HasEdge(e.To, e.From))
                                extended.AddEdge(e.To, e.From);
                        }
                    }

                    // Also ensure that mapped images (mapping.VertexMap.Values) exist as vertices
                    foreach (var img in mapping.VertexMap.Values)
                    {
                        if (!extended.ContainsVertex(img))
                            extended.AddVertex(img);
                    }
                }
            }

            return extended;
        }

        /// <summary>
        /// Validates that mappings don't have identical image sets (Im(Mi) != Im(Mj)).
        /// Image set defined as mapping.VertexMap.Values U mapping.AddedVertices.
        /// </summary>
        public static void ValidateMappings(
            SearchResult result,
            ValidationResult validation)
        {
            var imagesList = result.Mappings
                .Select(m => new HashSet<int>(m.VertexMap.Values.Concat(m.AddedVertices)))
                .ToList();

            for (int i = 0; i < imagesList.Count; i++)
            {
                for (int j = i + 1; j < imagesList.Count; j++)
                {
                    if (imagesList[i].SetEquals(imagesList[j]))
                    {
                        validation.AddError($"Mappings {i + 1} and {j + 1} have identical image sets.");
                    }
                }
            }
        }

        /// <summary>
        /// Validates total cost calculation across all mappings (unique added vertices & unique added edges).
        /// Edge uniqueness for cost uses directedness: directed edges keyed as "u->v", undirected as ordered "min-max".
        /// </summary>
        private static void ValidateCostCalculation(
            Graph<int> baseG2,
            SearchResult result,
            ValidationResult validation)
        {
            // all added vertices across mappings (distinct)
            var allAddedVertices = result.Mappings
                .SelectMany(m => m.AddedVertices)
                .Where(v => !baseG2.ContainsVertex(v)) // only truly new relative to base
                .Distinct()
                .ToHashSet();

            // all added edges across mappings (only those that are new relative to baseG2)
            var addedEdgeKeys = new HashSet<string>();
            int totalEdgeCost = 0;

            // Helper to determine if an edge already existed in baseG2
            bool EdgeExistsInBase(int a, int b, bool isDirected)
            {
                if (baseG2.HasEdge(a, b))
                    return true;
                if (!isDirected && baseG2.HasEdge(b, a))
                    return true;
                return false;
            }

            foreach (var mapping in result.Mappings)
            {
                foreach (var e in mapping.AddedEdges)
                {
                    // If base already had it, skip (cost was not incurred now)
                    if (EdgeExistsInBase(e.From, e.To, e.IsDirected))
                        continue;

                    string key = e.IsDirected
                        ? $"{e.From}->{e.To}"
                        : string.Join("-", new[] { e.From, e.To }.OrderBy(x => x));

                    if (addedEdgeKeys.Add(key))
                    {
                        totalEdgeCost += e.IsDirected ? 1 : 2;
                    }
                }
            }

            int expectedTotalCost = allAddedVertices.Count + totalEdgeCost;

            if (result.TotalCost != expectedTotalCost)
            {
                validation.AddError(
                    $"Total cost mismatch - calculated {expectedTotalCost}, reported {result.TotalCost}");
            }

            if (result.AddedVertices != allAddedVertices.Count)
            {
                validation.AddError(
                    $"Added vertices count mismatch - calculated {allAddedVertices.Count}, reported {result.AddedVertices}");
            }

            if (result.AddedEdges != totalEdgeCost)
            {
                validation.AddError(
                    $"Added edges cost mismatch - calculated {totalEdgeCost}, reported {result.AddedEdges}");
            }
        }
    }

    // Extension: if you don't already have AllUnorderedPairs on IList<SearchResult.Mappings> etc.
    public static class IEnumerableExtensions
    {
        public static IEnumerable<(T, T)> AllUnorderedPairs<T>(this IList<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    yield return (list[i], list[j]);
                }
            }
        }
    }
}
