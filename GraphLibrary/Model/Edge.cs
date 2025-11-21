namespace GraphLibrary.Model
{
    public class Edge<T> : IEquatable<Edge<T>> where T : notnull
    {
        public T From { get; }
        public T To { get; }
        public double Weight { get; }
        public bool IsDirected { get; }

        public Edge(T from, T to, double weight = 1.0, bool isDirected = false)
        {
            From = from;
            To = to;
            Weight = weight;
            IsDirected = isDirected;
        }

        public override string ToString()
        {
            var arrow = IsDirected ? "->" : "<->";
            return Weight != 1.0
                ? $"{From} {arrow} {To} (weight: {Weight})"
                : $"{From} {arrow} {To}";
        }

        public bool Equals(Edge<T>? other)
        {
            if (other is null) return false;
            if (IsDirected)
                return From.Equals(other.From) && To.Equals(other.To);

            return (From.Equals(other.From) && To.Equals(other.To)) ||
                   (From.Equals(other.To) && To.Equals(other.From));
        }

        public override bool Equals(object? obj) => Equals(obj as Edge<T>);

        public override int GetHashCode()
        {
            if (IsDirected)
                return HashCode.Combine(From, To);

            // For undirected edges, order shouldn't matter
            var h1 = From.GetHashCode();
            var h2 = To.GetHashCode();
            return h1 ^ h2;
        }
    }
}
