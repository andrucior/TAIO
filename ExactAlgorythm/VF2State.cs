using GraphLibrary.Model;

namespace ExactAlgorythm
{
    // State representation for VF2
    public class VF2State
    {
        public Graph<int> Graph { get; }
        private int _nextVertexId;
        private readonly Graph<int> _originalGraph;
        
        public VF2State(Graph<int> graph, int nextVertexId)
        {
            Graph = new Graph<int>(graph);
            _originalGraph = new Graph<int>(graph);
            _nextVertexId = nextVertexId;
        }
        
        private VF2State(Graph<int> graph, Graph<int> originalGraph, int nextVertexId)
        {
            Graph = new Graph<int>(graph);
            _originalGraph = originalGraph;
            _nextVertexId = nextVertexId;
        }
        
        public int GetNextVertexId() => _nextVertexId;
        
        public VF2State AddVertex(int newVertex)
        {
            var newGraph = new Graph<int>(Graph);
            newGraph.AddVertex(newVertex);
            return new VF2State(newGraph, _originalGraph, _nextVertexId + 1);
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
            
            // Check if both graphs are undirected
            bool bothUndirected = !g1.IsDirected && !Graph.IsDirected;
            
            if (bothUndirected)
            {
                // CASE 1: Both undirected - one edge represents both directions
                var processedEdges = new HashSet<(int, int)>();
                
                foreach (var edge in g1.GetAllEdges())
                {
                    int fromG2 = vertexMap[edge.From];
                    int toG2 = vertexMap[edge.To];
                    
                    // Normalize: smaller vertex first to avoid duplicates
                    var edgeKey = fromG2 < toG2 ? (fromG2, toG2) : (toG2, fromG2);
                    
                    if (!processedEdges.Add(edgeKey))
                        continue;
                    
                    if (!Graph.HasEdge(fromG2, toG2))
                    {
                        // Undirected edge missing (cost 2 - both directions)
                        addedEdges.Add(new Edge<int>(fromG2, toG2, 1.0, false));
                    }
                }
            }
            else if (g1.IsDirected)
            {
                // CASE 2: G1 is directed - treat each direction independently
                var processedEdges = new HashSet<(int, int)>();
                
                foreach (var edge in g1.GetAllEdges())
                {
                    int fromG2 = vertexMap[edge.From];
                    int toG2 = vertexMap[edge.To];
                    
                    // Don't normalize - each direction is separate!
                    if (!processedEdges.Add((fromG2, toG2)))
                        continue;
                    
                    if (!Graph.HasEdge(fromG2, toG2))
                    {
                        // Add missing directed edge (cost 1)
                        addedEdges.Add(new Edge<int>(fromG2, toG2, 1.0, true));
                    }
                }
            }
            else
            {
                // CASE 3: G1 undirected, G2 directed
                // Each undirected edge in G1 needs BOTH directions in G2
                var processedEdges = new HashSet<(int, int)>();
                
                foreach (var edge in g1.GetAllEdges())
                {
                    int fromG2 = vertexMap[edge.From];
                    int toG2 = vertexMap[edge.To];
                    
                    // Normalize to avoid processing same edge pair twice
                    var edgeKey = fromG2 < toG2 ? (fromG2, toG2) : (toG2, fromG2);
                    
                    if (!processedEdges.Add(edgeKey))
                        continue;
                    
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