using System;
using System.IO;

public static class TestFileGenerator
{
    private static Random rng = new Random();

    // Generates a random adjacency matrix for an undirected (simple) graph with no self-loops
    private static int[,] RandomAdjacencyMatrix(int n, double edgeProbability)
    {
        var matrix = new int[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                // random edge with probability edgeProbability
                int edge = rng.NextDouble() < edgeProbability ? 1 : 0;

                matrix[i, j] = edge;
                matrix[j, i] = edge; // undirected
            }

            matrix[i, i] = 0; // no self-loops
        }

        return matrix;
    }

    public static void GenerateRandomTestFile(
        string path,
        int n1,
        int n2,
        int kMin,
        int kMax,
        double densityG1 = 0.5,
        double densityG2 = 0.3)
    {
        var g1 = RandomAdjacencyMatrix(n1, densityG1);
        var g2 = RandomAdjacencyMatrix(n2, densityG2);
        int k = rng.Next(kMin, kMax + 1);

        using (var writer = new StreamWriter(path))
        {
            // write n1
            writer.WriteLine(n1);

            // write matrix G1
            for (int i = 0; i < n1; i++)
            {
                for (int j = 0; j < n1; j++)
                {
                    writer.Write(g1[i, j]);
                    if (j + 1 < n1) writer.Write(" ");
                }
                writer.WriteLine();
            }

            // write n2
            writer.WriteLine(n2);

            // write matrix G2
            for (int i = 0; i < n2; i++)
            {
                for (int j = 0; j < n2; j++)
                {
                    writer.Write(g2[i, j]);
                    if (j + 1 < n2) writer.Write(" ");
                }
                writer.WriteLine();
            }

            // write k
            writer.WriteLine(k);
        }
    }
}
