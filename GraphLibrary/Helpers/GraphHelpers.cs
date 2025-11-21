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

            Graph<int> graph = new();

            // Initialize vertices
            for (int i = 0; i < vertexCount; i++)
            {
                graph.AddVertex(i);
            }

            // Read adjacency matrix
            for (int i = 0; i < vertexCount; i++)
            {
                line = reader.ReadLine() ??
                    throw new InvalidDataException("Unexpected end of file while reading adjacency matrix.");

                string[]? parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != vertexCount)
                    throw new InvalidDataException("Adjacency matrix row does not match vertex count.");

                for (int j = 0; j < vertexCount; j++)
                {
                    if (int.TryParse(parts[j], out int edgeCount) && edgeCount > 0)
                    {
                        // Add edge edgeCount times (for multigraphs)
                        for (int k = 0; k < edgeCount; k++)
                        {
                            graph.AddEdge(i, j);
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
