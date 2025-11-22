using GraphLibrary.Model;

namespace ExactAlgorythm
{
    // State representation for VF2
    public class VF2State
    {
        public Graph<int> Graph { get; }
        private int _nextVertexId;
        private readonly Graph<int> _originalGraph; // Track original graph state
        private readonly bool _bothUndirected; // Cache: whether both G1 and G2 are undirected
        
        public VF2State(Graph<int> graph, int nextVertexId)
        {
            Graph = new Graph<int>(graph);
            _originalGraph = new Graph<int>(graph);
            _nextVertexId = nextVertexId;
            _bothUndirected = !graph.IsDirected; // Will be set properly when CreateMapping is called
        }
        
        private VF2State(Graph<int> graph, Graph<int> originalGraph, int nextVertexId, bool bothUndirected)
        {
            Graph = new Graph<int>(graph);
            _originalGraph = originalGraph;
            _nextVertexId = nextVertexId;
            _bothUndirected = bothUndirected;
        }
        
        public int GetNextVertexId() => _nextVertexId;
        
        public VF2State AddVertex(int newVertex)
        {
            var newGraph = new Graph<int>(Graph);
            newGraph.AddVertex(newVertex);
            return new VF2State(newGraph, _originalGraph, _nextVertexId + 1, _bothUndirected);
        }
        
        public Mapping CreateMapping(Graph<int> g1, Dictionary<int, int> vertexMap)
        {
            var addedVertices = new HashSet<int>();
            var addedEdges = new List<Edge<int>>();
            
            // Track which vertices were added (not in original graph)
            foreach (var v2 in vertexMap.Values)
            {
                if (!_originalGraph.ContainsVertex(v2))
                    addedVertices.Add(v2);
            }
            
            // Determine if both graphs are undirected (check only once)
            bool bothUndirected = !g1.IsDirected && !Graph.IsDirected;
            
            var processedEdges = new HashSet<(int, int)>();
            
            foreach (var edge in g1.GetAllEdges())
            {
                int fromG2 = vertexMap[edge.From];
                int toG2 = vertexMap[edge.To];
                
                // Normalize edge key to avoid processing duplicates
                var edgeKey = fromG2 < toG2 ? (fromG2, toG2) : (toG2, fromG2);
                
                if (!processedEdges.Add(edgeKey))
                    continue;
                
                if (bothUndirected)
                {
                    // Both undirected: check only one direction
                    // Graph.AddEdge(a,b) automatically adds both a->b and b->a
                    if (!Graph.HasEdge(fromG2, toG2))
                    {
                        addedEdges.Add(new Edge<int>(fromG2, toG2, 1.0, false)); // cost 2
                    }
                }
                else
                {
                    // At least one directed: check BOTH directions separately
                    bool hasForward = Graph.HasEdge(fromG2, toG2);
                    bool hasReverse = Graph.HasEdge(toG2, fromG2);
                    
                    if (!hasForward && !hasReverse)
                    {
                        // Both directions missing (cost 2)
                        addedEdges.Add(new Edge<int>(fromG2, toG2, 1.0, false));
                    }
                    else if (!hasForward)
                    {
                        // Only forward missing (cost 1)
                        addedEdges.Add(new Edge<int>(fromG2, toG2, 1.0, true));
                    }
                    else if (!hasReverse)
                    {
                        // Only reverse missing (cost 1)
                        addedEdges.Add(new Edge<int>(toG2, fromG2, 1.0, true));
                    }
                    // else: both exist, cost 0
                }
            }
            
            return new Mapping(vertexMap, addedVertices, addedEdges);
        }
    }
}