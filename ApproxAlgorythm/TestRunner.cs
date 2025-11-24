using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GraphLibrary.Helpers;
using GraphLibrary.Model;

namespace IHGEAlgorithm
{
    public class TestRunner
    {
        private readonly Dictionary<string, int> _expectedCosts = new()
        {
            // Testy nieskierowane
            { "test1.txt", 0 },
            { "test2.txt", 2 },
            { "test3.txt", 4 },
            { "test4.txt", 6 },
            { "test5.txt", 4 },
            { "test6.txt", 8 },
            { "test7.txt", 19 },
            { "test8.txt", 2 },
            
            // Testy skierowane
            { "test1_skier.txt", 3 },
            { "test2_skier.txt", 8 },
            { "test3_skier.txt", 3 },
            { "test4_skier.txt", 2 },
            { "test5_skier.txt", 11 },
            { "test6_skier.txt", 5 }
        };

        public void RunAllTests(string testsDirectory = "testy")
        {
            if (!Directory.Exists(testsDirectory))
            {
                Console.WriteLine($"ERROR: Directory '{testsDirectory}' not found!");
                return;
            }

            var testFiles = Directory.GetFiles(testsDirectory, "*.txt")
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .OrderBy(f => f)
                .ToList();

            if (testFiles.Count == 0)
            {
                Console.WriteLine($"No test files found in '{testsDirectory}'");
                return;
            }

            Console.WriteLine("=== IHGEAlgorithm Test Suite ===");
            Console.WriteLine();

            int totalTests = 0, passedTests = 0, failedTests = 0, unknownTests = 0;
            var results = new List<TestResult>();

            foreach (var testFile in testFiles)
            {
                totalTests++;
                var result = RunSingleTest(Path.Combine(testsDirectory, testFile), testFile);
                results.Add(result);

                switch (result.Status)
                {
                    case TestStatus.Passed: passedTests++; break;
                    case TestStatus.Failed: failedTests++; break;
                    case TestStatus.Unknown: unknownTests++; break;
                }
            }

            // Podsumowanie
            Console.WriteLine();
            Console.WriteLine("=== Test Summary ===");
            Console.WriteLine($"Total tests: {totalTests}");
            Console.WriteLine($"Passed: {passedTests}");
            Console.WriteLine($"Failed: {failedTests}");
            Console.WriteLine($"Unknown: {unknownTests}");
            Console.WriteLine();

            if (failedTests > 0)
            {
                Console.WriteLine("Failed tests:");
                foreach (var r in results.Where(r => r.Status == TestStatus.Failed))
                    Console.WriteLine($"  {r.TestName}: Expected {r.ExpectedCost}, Got {r.ActualCost}");
            }

            if (unknownTests > 0)
            {
                Console.WriteLine("Tests without expected cost:");
                foreach (var r in results.Where(r => r.Status == TestStatus.Unknown))
                    Console.WriteLine($"  {r.TestName}: Got {r.ActualCost}");
            }
        }

        private TestResult RunSingleTest(string filePath, string testName)
        {
            var result = new TestResult { TestName = testName };

            Console.Write($"Running {testName}... ");

            try
            {
                var (g1, g2) = GraphHelpers.FromFile(filePath, out int copies);
                var solver = new MultiIHGESolver(g1, g2);

                var stopwatch = Stopwatch.StartNew();
                var searchResult = solver.FindKCopies(copies);
                stopwatch.Stop();

                result.ActualCost = searchResult.TotalCost;
                result.ExecutionTime = stopwatch.ElapsedMilliseconds;
                result.Success = true;

                if (_expectedCosts.TryGetValue(testName, out int expected))
                {
                    result.ExpectedCost = expected;
                    result.HasExpectedCost = true;

                    if (result.ActualCost == expected)
                    {
                        result.Status = TestStatus.Passed;
                        Console.WriteLine("PASS");
                    }
                    else
                    {
                        result.Status = TestStatus.Failed;
                        Console.WriteLine("FAIL");
                    }
                }
                else
                {
                    result.Status = TestStatus.Unknown;
                    Console.WriteLine("UNKNOWN");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Status = TestStatus.Failed;
                result.ErrorMessage = ex.Message;
                Console.WriteLine($"ERROR: {ex.Message}");
            }

            return result;
        }

        private class TestResult
        {
            public string TestName { get; set; } = "";
            public bool Success { get; set; }
            public int ActualCost { get; set; }
            public int? ExpectedCost { get; set; }
            public bool HasExpectedCost { get; set; }
            public long ExecutionTime { get; set; }
            public TestStatus Status { get; set; }
            public string? ErrorMessage { get; set; }
        }

        private enum TestStatus
        {
            Passed,
            Failed,
            Unknown
        }
    }
}
