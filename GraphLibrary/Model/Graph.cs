using System.Text;

namespace GraphLibrary.Model
{
    public class Graph<T> where T : notnull
    {
        private readonly Dictionary<T, List<(T neighbor, double weight)>> _adj;

        public bool IsDirected { get; }
        public int VertexCount => _adj.Count;
        public int EdgeCount => GetAllEdges().Count();

        public Graph(bool directed = false)
        {
            IsDirected = directed;
            _adj = [];
        }

        // Deep copy constructor
        public Graph(Graph<T> other)
        {
            IsDirected = other.IsDirected;
            _adj = [];

            foreach (var (vertex, neighbors) in other._adj)
            {
                _adj[vertex] = [.. neighbors];
            }
        }

        // Vertex operations
        public void AddVertex(T v)
        {
            if (!_adj.ContainsKey(v))
                _adj[v] = new List<(T neighbor, double weight)>();
        }

        public bool RemoveVertex(T v)
        {
            if (!_adj.ContainsKey(v)) return false;

            // Remove all edges pointing to this vertex
            foreach (var neighbors in _adj.Values)
            {
                neighbors.RemoveAll(n => n.neighbor.Equals(v));
            }

            return _adj.Remove(v);
        }

        public bool ContainsVertex(T v) => _adj.ContainsKey(v);

        // Edge operations
        public void AddEdge(T u, T v, double weight = 1.0)
        {
            if (weight < 0)
                throw new ArgumentException("Weight cannot be negative", nameof(weight));

            AddVertex(u);
            AddVertex(v);

            // Avoid duplicate edges
            if (!_adj[u].Any(n => n.neighbor.Equals(v)))
                _adj[u].Add((v, weight));

            if (!IsDirected && !_adj[v].Any(n => n.neighbor.Equals(u)))
                _adj[v].Add((u, weight));
        }

        public bool RemoveEdge(T u, T v)
        {
            if (!_adj.TryGetValue(u, out var list)) return false;

            bool removed = list.RemoveAll(n => n.neighbor.Equals(v)) > 0;

            if (!IsDirected && _adj.TryGetValue(v, out var reverseList))
                reverseList.RemoveAll(n => n.neighbor.Equals(u));

            return removed;
        }

        public bool RemoveEdge(Edge<T> e)
        {
            return RemoveEdge(e.From, e.To);
        }

        public bool HasEdge(T u, T v)
        {
            return _adj.TryGetValue(u, out var list) &&
                   list.Any(x => x.neighbor.Equals(v));
        }

        public double? GetEdgeWeight(T u, T v)
        {
            if (!_adj.TryGetValue(u, out var list)) return null;

            var edge = list.FirstOrDefault(x => x.neighbor.Equals(v));
            return edge.Equals(default) ? null : edge.weight;
        }

        // Graph properties
        public IEnumerable<T> Vertices => _adj.Keys;

        public IEnumerable<Edge<T>> GetAllEdges()
        {
            var seen = new HashSet<(T, T)>();

            foreach (var (from, neighbors) in _adj)
            {
                foreach (var (to, weight) in neighbors)
                {
                    if (IsDirected)
                    {
                        yield return new Edge<T>(from, to, weight, true);
                    }
                    else
                    {
                        var pair = (from.GetHashCode() < to.GetHashCode())
                            ? (from, to)
                            : (to, from);

                        if (seen.Add(pair))
                            yield return new Edge<T>(from, to, weight, false);
                    }
                }
            }
        }

        public IEnumerable<(T neighbor, double weight)> GetNeighbors(T v)
        {
            return _adj.TryGetValue(v, out var list) ? list : Enumerable.Empty<(T, double)>();
        }

        public int Degree(T v) => _adj.TryGetValue(v, out var l) ? l.Count : 0;

        public int OutDegree(T v) => Degree(v);

        public int InDegree(T v)
        {
            if (!IsDirected) return Degree(v);

            return _adj.Values.Count(list => list.Any(n => n.neighbor.Equals(v)));
        }

        // Traversal algorithms
        public List<T> BFS(T start)
        {
            if (!_adj.ContainsKey(start))
                throw new ArgumentException("Start vertex not in graph", nameof(start));

            var visited = new HashSet<T>();
            var queue = new Queue<T>();
            var order = new List<T>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                order.Add(v);

                foreach (var (n, _) in GetNeighbors(v))
                {
                    if (visited.Add(n))
                        queue.Enqueue(n);
                }
            }
            return order;
        }

        public List<T> DFS(T start)
        {
            if (!_adj.ContainsKey(start))
                throw new ArgumentException("Start vertex not in graph", nameof(start));

            var visited = new HashSet<T>();
            var order = new List<T>();
            DFSRec(start, visited, order);
            return order;
        }

        private void DFSRec(T v, HashSet<T> visited, List<T> order)
        {
            visited.Add(v);
            order.Add(v);

            foreach (var (n, _) in GetNeighbors(v))
            {
                if (!visited.Contains(n))
                    DFSRec(n, visited, order);
            }
        }

        // Shortest path algorithms
        public Dictionary<T, double> Dijkstra(T source)
        {
            if (!_adj.ContainsKey(source))
                throw new ArgumentException("Source vertex not in graph", nameof(source));

            var dist = Vertices.ToDictionary(v => v, v => double.PositiveInfinity);
            dist[source] = 0;

            var pq = new PriorityQueue<T, double>();
            pq.Enqueue(source, 0);

            while (pq.Count > 0)
            {
                pq.TryDequeue(out T? u, out double d);
                if (u is null || d > dist[u]) continue;

                foreach (var (v, w) in GetNeighbors(u))
                {
                    double nd = d + w;
                    if (nd < dist[v])
                    {
                        dist[v] = nd;
                        pq.Enqueue(v, nd);
                    }
                }
            }

            return dist;
        }

        public (Dictionary<T, double> distances, Dictionary<T, T?> previous)
            DijkstraWithPath(T source)
        {
            if (!_adj.ContainsKey(source))
                throw new ArgumentException("Source vertex not in graph", nameof(source));

            var dist = Vertices.ToDictionary(v => v, v => double.PositiveInfinity);
            var prev = Vertices.ToDictionary(v => v, v => (T?)default);
            dist[source] = 0;

            var pq = new PriorityQueue<T, double>();
            pq.Enqueue(source, 0);

            while (pq.Count > 0)
            {
                pq.TryDequeue(out T? u, out double d);
                if (u is null || d > dist[u]) continue;

                foreach (var (v, w) in GetNeighbors(u))
                {
                    double nd = d + w;
                    if (nd < dist[v])
                    {
                        dist[v] = nd;
                        prev[v] = u;
                        pq.Enqueue(v, nd);
                    }
                }
            }

            return (dist, prev);
        }

        public List<T>? GetShortestPath(T source, T target)
        {
            var (_, prev) = DijkstraWithPath(source);

            if (prev[target] == null && !source.Equals(target))
                return null;

            var path = new List<T>();
            var current = target;

            while (current != null)
            {
                path.Add(current);
                if (current.Equals(source)) break;
                current = prev[current]!;
            }

            path.Reverse();
            return path[0].Equals(source) ? path : null;
        }

        // Cycle detection
        public bool HasCycle()
        {
            return IsDirected ? HasCycleDirected() : HasCycleUndirected();
        }

        private bool HasCycleDirected()
        {
            var visited = new HashSet<T>();
            var stack = new HashSet<T>();

            foreach (var v in Vertices)
                if (HasCycleDFS(v, visited, stack))
                    return true;

            return false;
        }

        private bool HasCycleDFS(T v, HashSet<T> visited, HashSet<T> stack)
        {
            if (stack.Contains(v)) return true;
            if (visited.Contains(v)) return false;

            visited.Add(v);
            stack.Add(v);

            foreach (var (n, _) in GetNeighbors(v))
                if (HasCycleDFS(n, visited, stack))
                    return true;

            stack.Remove(v);
            return false;
        }

        private bool HasCycleUndirected()
        {
            var visited = new HashSet<T>();

            foreach (var v in Vertices)
            {
                if (!visited.Contains(v))
                {
                    if (HasCycleUndirectedDFS(v, default, visited))
                        return true;
                }
            }

            return false;
        }

        private bool HasCycleUndirectedDFS(T v, T? parent, HashSet<T> visited)
        {
            visited.Add(v);

            foreach (var (n, _) in GetNeighbors(v))
            {
                if (!visited.Contains(n))
                {
                    if (HasCycleUndirectedDFS(n, v, visited))
                        return true;
                }
                else if (parent == null || !n.Equals(parent))
                {
                    return true;
                }
            }

            return false;
        }

        // Topological sort (directed graphs only)
        public List<T>? TopologicalSort()
        {
            if (!IsDirected)
                throw new InvalidOperationException("Topological sort only works on directed graphs");

            if (HasCycleDirected())
                return null;

            var visited = new HashSet<T>();
            var result = new Stack<T>();

            foreach (var v in Vertices)
                if (!visited.Contains(v))
                    TopSortDFS(v, visited, result);

            return result.ToList();
        }

        private void TopSortDFS(T v, HashSet<T> visited, Stack<T> stack)
        {
            visited.Add(v);

            foreach (var (n, _) in GetNeighbors(v))
                if (!visited.Contains(n))
                    TopSortDFS(n, visited, stack);

            stack.Push(v);
        }

        // Connectivity
        public bool IsConnected()
        {
            if (VertexCount == 0) return true;

            var reachable = BFS(Vertices.First());
            return reachable.Count == VertexCount;
        }

        public List<List<T>> GetConnectedComponents()
        {
            var visited = new HashSet<T>();
            var components = new List<List<T>>();

            foreach (var v in Vertices)
            {
                if (!visited.Contains(v))
                {
                    var component = BFS(v);
                    components.Add(component);
                    visited.UnionWith(component);
                }
            }

            return components;
        }

        // Utility methods
        public void Clear()
        {
            _adj.Clear();
        }

        public Graph<T> GetTranspose()
        {
            if (!IsDirected)
                throw new InvalidOperationException("Transpose only applies to directed graphs");

            var transposed = new Graph<T>(true);

            foreach (var v in Vertices)
                transposed.AddVertex(v);

            foreach (var (from, neighbors) in _adj)
            {
                foreach (var (to, weight) in neighbors)
                    transposed.AddEdge(to, from, weight);
            }

            return transposed;
        }

        public override string ToString()
        {
            return $"Graph (Vertices: {VertexCount}, Edges: {EdgeCount}, Directed: {IsDirected})";
        }

        public string DisplayDetailedInfo()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(this.ToString());
            var edges = GetAllEdges();
            if (!edges.Any())
            {
                stringBuilder.AppendLine("No edges yet");
            }
            else
            {
                foreach (Edge<T> e in GetAllEdges())
                {
                    stringBuilder.AppendLine(e.ToString());
                }
            }
            return stringBuilder.ToString();
        }
    }
}
