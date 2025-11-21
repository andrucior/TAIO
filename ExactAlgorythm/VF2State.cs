using GraphLibrary.Model;

namespace ExactAlgorythm
{
    // State representation for VF2
    public class VF2State
    {
        public Graph<int> Graph { get; }
        private int _nextVertexId;
        private readonly Graph<int> _originalGraph; // Track original graph state
        
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
            
            if (g1.IsDirected)
            {
                // For DIRECTED graphs: track each direction separately
                // An edge can be unidirectional (cost 1) or bidirectional (cost 2)
                var processedEdges = new HashSet<(int, int)>();
                
                foreach (var edge in g1.GetAllEdges())
                {
                    int fromG2 = vertexMap[edge.From];
                    int toG2 = vertexMap[edge.To];
                    
                    if (processedEdges.Contains((fromG2, toG2)))
                        continue;
                    
                    bool needsForward = !Graph.HasEdge(fromG2, toG2);
                    bool needsReverse = !Graph.HasEdge(toG2, fromG2);
                    
                    // Check if reverse edge exists in G1
                    bool hasReverseInG1 = g1.HasEdge(edge.To, edge.From);
                    
                    if (needsForward && needsReverse && hasReverseInG1)
                    {
                        // Need bidirectional edge
                        addedEdges.Add(new Edge<int>(fromG2, toG2, 1.0, false)); // Undirected = bidirectional
                        processedEdges.Add((fromG2, toG2));
                        processedEdges.Add((toG2, fromG2));
                    }
                    else if (needsForward)
                    {
                        // Need only forward edge
                        addedEdges.Add(new Edge<int>(fromG2, toG2, 1.0, true));
                        processedEdges.Add((fromG2, toG2));
                    }
                    
                    // Handle reverse edge separately if it exists in G1 but not as part of bidirectional
                    if (hasReverseInG1 && needsReverse && !needsForward)
                    {
                        addedEdges.Add(new Edge<int>(toG2, fromG2, 1.0, true));
                        processedEdges.Add((toG2, fromG2));
                    }
                }
            }
            else
            {
                // For UNDIRECTED graphs: each edge is automatically bidirectional
                var processedEdges = new HashSet<(int, int)>();
                
                foreach (var edge in g1.GetAllEdges())
                {
                    int fromG2 = vertexMap[edge.From];
                    int toG2 = vertexMap[edge.To];
                    
                    // Normalize to avoid duplicates
                    var edgeKey = fromG2 < toG2 ? (fromG2, toG2) : (toG2, fromG2);
                    
                    if (processedEdges.Add(edgeKey))
                    {
                        // Check if edge exists in either direction
                        if (!Graph.HasEdge(fromG2, toG2))
                        {
                            addedEdges.Add(new Edge<int>(fromG2, toG2, 1.0, false)); // Undirected
                        }
                    }
                }
            }
            
            return new Mapping(vertexMap, addedVertices, addedEdges);
        }
    }
}