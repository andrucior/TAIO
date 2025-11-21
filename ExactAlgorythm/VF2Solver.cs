using GraphLibrary.Model;

namespace ExactAlgorythm
{
    public class VF2Solver
    {
        private readonly Graph<int> _g1; // Pattern graph
        private readonly Graph<int> _g2Original; // Original target graph
        private int _nextVertexId; // For creating new vertices

        public VF2Solver(Graph<int> g1, Graph<int> g2)
        {
            _g1 = g1;
            _g2Original = new Graph<int>(g2);
            _nextVertexId = g2.Vertices.Any() ? g2.Vertices.Max() + 1 : 0;
        }

        public SearchResult FindKCopies(int k)
        {
            int bestCost = int.MaxValue;
            List<Mapping>? bestMappings = null;

            // Start recursive search from level 1
            FindKCopiesRecursive(
                k,
                new Graph<int>(_g2Original),
                [],
                0,
                ref bestCost,
                ref bestMappings
            );

            if (bestMappings == null)
            {
                // Fallback: create k independent copies
                bestMappings = CreateIndependentCopies(k);
                bestCost = bestMappings.Sum(m => m.Cost);
            }

            return new SearchResult(bestMappings, bestCost);
        }

        private void FindKCopiesRecursive(
            int remainingCopies,
            Graph<int> currentG2,
            List<Mapping> selectedMappings,
            int currentCost,
            ref int bestCost,
            ref List<Mapping>? bestMappings)
        {
            // Pruning: if current cost exceeds best, stop
            if (currentCost >= bestCost)
                return;

            // Base case: found k copies
            if (remainingCopies == 0)
            {
                if (currentCost < bestCost)
                {
                    bestCost = currentCost;
                    bestMappings = new List<Mapping>(selectedMappings);
                }
                return;
            }

            // Generate all possible mappings for current level
            var mappingQueue = GenerateAllMappings(currentG2, selectedMappings);

            // Try each mapping in order of increasing cost
            foreach (var mapping in mappingQueue.OrderBy(m => m.Cost))
            {
                // Pruning
                if (currentCost + mapping.Cost >= bestCost)
                    break;

                // Create extended graph
                var extendedG2 = ApplyMapping(currentG2, mapping);

                // Add mapping to selected set
                var newSelected = new List<Mapping>(selectedMappings) { mapping };

                // Recurse to next level
                FindKCopiesRecursive(
                    remainingCopies - 1,
                    extendedG2,
                    newSelected,
                    currentCost + mapping.Cost,
                    ref bestCost,
                    ref bestMappings
                );
            }
        }

        private List<Mapping> GenerateAllMappings(Graph<int> g2, List<Mapping> usedMappings)
        {
            var result = new List<Mapping>();
            var usedVertexSets = new HashSet<string>(
                usedMappings.Select(m => string.Join(",", m.VertexMap.Values.OrderBy(v => v)))
            );

            // Start VF2 matching
            var initialState = new VF2State(g2, _nextVertexId);
            VF2Match(initialState, [], usedVertexSets, result);

            return result;
        }

        private void VF2Match(
            VF2State state,
            Dictionary<int, int> currentMapping,
            HashSet<string> usedVertexSets,
            List<Mapping> results)
        {
            // All vertices of G1 are mapped
            if (currentMapping.Count == _g1.VertexCount)
            {
                // Check if this vertex set is already used
                string vertexSetKey = string.Join(",", currentMapping.Values.OrderBy(v => v));
                if (!usedVertexSets.Contains(vertexSetKey))
                {
                    var mapping = state.CreateMapping(_g1, currentMapping);
                    results.Add(mapping);
                }
                return;
            }

            // Select next vertex from G1
            var unmappedG1 = _g1.Vertices.Where(v => !currentMapping.ContainsKey(v)).ToList();
            if (unmappedG1.Count == 0)
                return;

            int n = SelectNextVertex(unmappedG1, currentMapping);

            // Try mapping to existing vertices in G2
            foreach (int m in state.Graph.Vertices)
            {
                if (currentMapping.ContainsValue(m))
                    continue;

                if (IsFeasible(n, m, currentMapping, state.Graph))
                {
                    var newMapping = new Dictionary<int, int>(currentMapping) { [n] = m };
                    VF2Match(state, newMapping, usedVertexSets, results);
                }
            }

            // Try creating a new vertex
            int newVertex = state.GetNextVertexId();
            var newState = state.AddVertex(newVertex);
            var mappingWithNew = new Dictionary<int, int>(currentMapping) { [n] = newVertex };
            VF2Match(newState, mappingWithNew, usedVertexSets, results);
        }

        private int SelectNextVertex(List<int> unmapped, Dictionary<int, int> mapping)
        {
            // Heuristic: select vertex with highest degree or most mapped neighbors
            return unmapped
                .OrderByDescending(v => _g1.Degree(v))
                .ThenByDescending(v => _g1.GetNeighbors(v).Count(n => mapping.ContainsKey((int)n.neighbor)))
                .First();
        }

        private bool IsFeasible(int n, int m, Dictionary<int, int> mapping, Graph<int> g2)
        {
            // Check if all required edges can be satisfied
            int requiredNewEdges = 0;

            foreach (var (n1, m1) in mapping)
            {
                bool g1HasEdge = _g1.HasEdge(n, n1);
                bool g2HasEdge = g2.HasEdge(m, m1);

                if (g1HasEdge && !g2HasEdge)
                    requiredNewEdges++;
            }

            // Always feasible - we can add edges
            return true;
        }

        private Graph<int> ApplyMapping(Graph<int> g2, Mapping mapping)
        {
            var result = new Graph<int>(g2);

            // Add new vertices
            foreach (var v in mapping.AddedVertices)
            {
                result.AddVertex(v);
                if (v >= _nextVertexId)
                    _nextVertexId = v + 1;
            }

            // Add new edges
            foreach (var edge in mapping.AddedEdges)
            {
                if (!result.HasEdge(edge.From, edge.To))
                    result.AddEdge(edge.From, edge.To);
            }

            return result;
        }

        private List<Mapping> CreateIndependentCopies(int k)
        {
            var mappings = new List<Mapping>();
            int currentVertex = _nextVertexId;
            
            for (int i = 0; i < k; i++)
            {
                var vertexMap = new Dictionary<int, int>();
                var addedVertices = new HashSet<int>();
                var addedEdges = new List<Edge<int>>();
                
                // Map each vertex to a new vertex
                foreach (var v in _g1.Vertices)
                {
                    vertexMap[v] = currentVertex;
                    addedVertices.Add(currentVertex);
                    currentVertex++;
                }
                
                // Add all edges preserving graph type
                foreach (var edge in _g1.GetAllEdges())
                {
                    var newEdge = new Edge<int>(
                        vertexMap[edge.From], 
                        vertexMap[edge.To],
                        1.0,
                        _g1.IsDirected  // Preserve directed/undirected nature
                    );
                    addedEdges.Add(newEdge);
                }
                
                mappings.Add(new Mapping(vertexMap, addedVertices, addedEdges));
            }
            
            return mappings;
        }
    }
}