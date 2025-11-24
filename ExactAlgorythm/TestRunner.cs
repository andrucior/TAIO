using GraphLibrary.Helpers;
using System.Diagnostics;

namespace ExactAlgorythm
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

            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           AUTOMATED TEST SUITE - VF2 ALGORITHM                ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            int totalTests = 0;
            int passedTests = 0;
            int failedTests = 0;
            int unknownTests = 0;

            var results = new List<TestResult>();

            foreach (var testFile in testFiles)
            {
                if (testFile == null) continue;
                
                totalTests++;
                var result = RunSingleTest(Path.Combine(testsDirectory, testFile), testFile);
                results.Add(result);

                switch (result.Status)
                {
                    case TestStatus.Passed:
                        passedTests++;
                        break;
                    case TestStatus.Failed:
                        failedTests++;
                        break;
                    case TestStatus.Unknown:
                        unknownTests++;
                        break;
                }
            }

            // Print summary
            Console.WriteLine();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                        TEST SUMMARY                           ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"Total tests:   {totalTests}");
            Console.WriteLine($"✓ Passed:      {passedTests} ({(totalTests > 0 ? passedTests * 100.0 / totalTests : 0):F1}%)");
            Console.WriteLine($"✗ Failed:      {failedTests} ({(totalTests > 0 ? failedTests * 100.0 / totalTests : 0):F1}%)");
            Console.WriteLine($"? Unknown:     {unknownTests}");
            Console.WriteLine();

            if (failedTests > 0)
            {
                Console.WriteLine("Failed tests:");
                foreach (var result in results.Where(r => r.Status == TestStatus.Failed))
                {
                    Console.WriteLine($"  - {result.TestName}: Expected {result.ExpectedCost}, Got {result.ActualCost}");
                }
                Console.WriteLine();
            }

            if (unknownTests > 0)
            {
                Console.WriteLine("Tests without expected cost defined:");
                foreach (var result in results.Where(r => r.Status == TestStatus.Unknown))
                {
                    Console.WriteLine($"  - {result.TestName}: Got {result.ActualCost}");
                }
                Console.WriteLine();
            }

            // Overall result
            if (failedTests == 0 && unknownTests == 0)
            {
                Console.WriteLine("🎉 ALL TESTS PASSED! 🎉");
            }
            else if (failedTests == 0)
            {
                Console.WriteLine("✓ All known tests passed (some tests have no expected cost)");
            }
            else
            {
                Console.WriteLine($"⚠ {failedTests} test(s) failed!");
            }
        }

        private TestResult RunSingleTest(string filePath, string testName)
        {
            var result = new TestResult { TestName = testName };

            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.Write($"Running: {testName,-30} ");

            try
            {
                // Read graphs from file
                var (g1, g2) = GraphHelpers.FromFile(filePath, out int copies);

                // Create solver and run
                var solver = new VF2Solver(g1, g2);
                
                var stopwatch = Stopwatch.StartNew();
                var searchResult = solver.FindKCopies(copies);
                stopwatch.Stop();

                result.ActualCost = searchResult.TotalCost;
                result.ExecutionTime = stopwatch.ElapsedMilliseconds;
                result.Success = true;

                // Check against expected cost
                if (_expectedCosts.TryGetValue(testName, out int expectedCost))
                {
                    result.ExpectedCost = expectedCost;
                    result.HasExpectedCost = true;

                    if (result.ActualCost == expectedCost)
                    {
                        result.Status = TestStatus.Passed;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ PASS");
                        Console.ResetColor();
                        Console.WriteLine($"  Cost: {result.ActualCost}, Time: {result.ExecutionTime}ms");
                    }
                    else
                    {
                        result.Status = TestStatus.Failed;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"✗ FAIL");
                        Console.ResetColor();
                        Console.WriteLine($"  Expected: {expectedCost}, Got: {result.ActualCost}, Time: {result.ExecutionTime}ms");
                    }
                }
                else
                {
                    result.Status = TestStatus.Unknown;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"? UNKNOWN");
                    Console.ResetColor();
                    Console.WriteLine($"  Cost: {result.ActualCost}, Time: {result.ExecutionTime}ms (no expected cost defined)");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Status = TestStatus.Failed;
                result.ErrorMessage = ex.Message;
                
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ ERROR");
                Console.ResetColor();
                Console.WriteLine($"  {ex.Message}");
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