using GraphLibrary.Model;
using System;
using System.IO;
using System.Linq;

namespace GraphLibrary.Helpers
{
    public static class GraphHelpers
    {
        // Reads a single graph from a StreamReader starting at current position
        private static Graph<int> ReadGraph(StreamReader reader)
        {
            // Read number of vertices
            string? line = reader.ReadLine() ?? 
                throw new InvalidDataException("Unexpected end of file while reading number of vertices.");
            
            if (!int.TryParse(line, out int vertexCount))
                throw new InvalidDataException("Invalid number of vertices.");

            // Read adjacency matrix first to check if directed
            var matrix = new int[vertexCount, vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                line = reader.ReadLine() ??
                    throw new InvalidDataException("Unexpected end of file while reading adjacency matrix.");

                string[]? parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != vertexCount)
                    throw new InvalidDataException("Adjacency matrix row does not match vertex count.");

                for (int j = 0; j < vertexCount; j++)
                {
                    if (!int.TryParse(parts[j], out matrix[i, j]))
                        throw new InvalidDataException("Invalid edge count in adjacency matrix.");
                }
            }

            // Check if matrix is symmetric (undirected) or asymmetric (directed)
            bool isDirected = false;
            for (int i = 0; i < vertexCount && !isDirected; i++)
            {
                for (int j = i + 1; j < vertexCount; j++)
                {
                    if (matrix[i, j] != matrix[j, i])
                    {
                        isDirected = true;
                        break;
                    }
                }
            }

            Graph<int> graph = new(isDirected);

            // Initialize vertices
            for (int i = 0; i < vertexCount; i++)
            {
                graph.AddVertex(i);
            }

            // Add edges based on matrix
            for (int i = 0; i < vertexCount; i++)
            {
                for (int j = 0; j < vertexCount; j++)
                {
                    if (matrix[i, j] > 0)
                    {
                        // For directed graphs, add edge from i to j
                        // For undirected graphs, add only once (i <= j to avoid duplicates)
                        if (isDirected || i <= j)
                        {
                            for (int k = 0; k < matrix[i, j]; k++)
                            {
                                graph.AddEdge(i, j);
                            }
                        }
                    }
                }
            }

            return graph;
        }

        // Reads two graphs from a file
        public static (Graph<int> firstGraph, Graph<int> secondGraph) FromFile(string path, out int copies)
        {
            copies = 1;
            using StreamReader reader = new(path);

            var firstGraph = ReadGraph(reader);
            var secondGraph = ReadGraph(reader);
            
            if (!int.TryParse(reader.ReadLine(), out copies))
                copies = 1;

            // Additional data can be read from reader here if needed

            return (firstGraph, secondGraph);
        }

        public static Graph<int> Graph(int nOfVertices, bool IsDirected = false)
        {
            Graph<int> graph = new(IsDirected);
            for (int i = 0; i < nOfVertices; i++)
            {
                graph.AddVertex(i);
            }
            return graph;
        }
    }
}
