using GraphLibrary.Helpers;
using IHGEAlgorithm;
using System.Diagnostics;

namespace ApproxAlgorythm
{
    public class TestRunnerV2
    {
        public void RunAllTests(string testsDirectory = "testy", int repeats = 1)
        {
            if (!Directory.Exists(testsDirectory))
            {
                Console.WriteLine($"Directory '{testsDirectory}' not found!");
                return;
            }

            var testFiles = Directory.GetFiles(testsDirectory, "*.txt")
                .OrderBy(f => f)
                .ToList();

            if (testFiles.Count == 0)
            {
                Console.WriteLine("No tests found.");
                return;
            }

            Console.WriteLine($"Running {testFiles.Count} tests (repeats: {repeats})");
            Console.WriteLine("TestName\t\tTime(ms)\tCost");

            long totalTime = 0;
            long minTime = long.MaxValue;
            long maxTime = 0;

            foreach (var file in testFiles)
            {
                for (int i = 0; i < repeats; i++)
                {
                    var result = RunSingleTest(file);

                    totalTime += result.ExecutionTime;
                    minTime = Math.Min(minTime, result.ExecutionTime);
                    maxTime = Math.Max(maxTime, result.ExecutionTime);

                    Console.WriteLine($"{Path.GetFileName(file)}\t{result.ExecutionTime}\t{result.ActualCost}");
                }
            }

            long totalRuns = testFiles.Count * repeats;
            double avgTime = totalRuns > 0 ? totalTime / (double)totalRuns : 0;

            Console.WriteLine();
            Console.WriteLine("----- SUMMARY -----");
            Console.WriteLine($"Runs: {totalRuns}");
            Console.WriteLine($"Avg time: {avgTime:F2} ms");
            Console.WriteLine($"Min time: {minTime} ms");
            Console.WriteLine($"Max time: {maxTime} ms");
        }

        private TestResult RunSingleTest(string filePath)
        {
            var result = new TestResult { TestName = Path.GetFileName(filePath) };

            try
            {
                var (g1, g2) = GraphHelpers.FromFile(filePath, out int copies);
                var solver = new MultiIHGESolver(g1, g2);

                var stopwatch = Stopwatch.StartNew();
                var searchResult = solver.FindKCopies(copies);
                stopwatch.Stop();

                result.ActualCost = searchResult.TotalCost;
                result.ExecutionTime = stopwatch.ElapsedMilliseconds;
            }
            catch
            {
                result.ActualCost = -1;
                result.ExecutionTime = -1;
            }

            return result;
        }

        private class TestResult
        {
            public string TestName { get; set; } = "";
            public int ActualCost { get; set; }
            public long ExecutionTime { get; set; }
        }
    }
}
