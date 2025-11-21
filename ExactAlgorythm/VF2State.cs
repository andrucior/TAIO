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

            // Track which edges need to be added
            foreach (var edge in g1.GetAllEdges())
            {
                int fromG2 = vertexMap[edge.From];
                int toG2 = vertexMap[edge.To];

                // Check if this edge exists in current graph
                if (!Graph.HasEdge(fromG2, toG2))
                {
                    addedEdges.Add(new Edge<int>(fromG2, toG2));
                }
            }

            return new Mapping(vertexMap, addedVertices, addedEdges);
        }
    }
}