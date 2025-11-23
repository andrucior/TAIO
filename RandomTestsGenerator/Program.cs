using System;
using System.IO;

class RandomGraphTests
{
    static void Main()
    {
        string folder = "tests";
        Directory.CreateDirectory(folder);

        int numTests = 10; // ile plików chcesz wygenerować
        Random rnd = new Random();

        for (int t = 1; t <= numTests; t++)
        {
            // Rozmiary grafów
            int n1 = rnd.Next(3, 8);          // liczba wierzchołków G1
            int n2 = rnd.Next(n1, n1 + 6);    // liczba wierzchołków G2

            // Prawdopodobieństwa krawędzi
            double p1 = 0.3;
            double p2 = 0.2;

            int[,] G1 = new int[n1, n1];
            int[,] G2 = new int[n2, n2];

            // Losowy skierowany graf G1
            for (int i = 0; i < n1; i++)
                for (int j = 0; j < n1; j++)
                    if (i != j)
                        G1[i, j] = rnd.NextDouble() < p1 ? 1 : 0;

            // Losowy skierowany graf G2
            for (int i = 0; i < n2; i++)
                for (int j = 0; j < n2; j++)
                    if (i != j)
                        G2[i, j] = rnd.NextDouble() < p2 ? 1 : 0;

            string filePath = Path.Combine(folder, $"test_{t}.txt");
            using (var writer = new StreamWriter(filePath))
            {
                // G1
                writer.WriteLine(n1);
                for (int i = 0; i < n1; i++)
                {
                    for (int j = 0; j < n1; j++)
                    {
                        writer.Write(G1[i, j]);
                        if (j < n1 - 1) writer.Write(" ");
                    }
                    writer.WriteLine();
                }

                // G2
                writer.WriteLine(n2);
                for (int i = 0; i < n2; i++)
                {
                    for (int j = 0; j < n2; j++)
                    {
                        writer.Write(G2[i, j]);
                        if (j < n2 - 1) writer.Write(" ");
                    }
                    writer.WriteLine();
                }

                // Liczba kopii
                writer.WriteLine("1");
            }

            Console.WriteLine($"Generated test file: {filePath}");
        }
    }
}
