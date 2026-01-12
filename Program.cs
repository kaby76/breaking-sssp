using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ShortestPathAlgorithm;

class Program
{
    static Random rand = new Random(42);

    static void Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("  SSSP Algorithm Tests");
        Console.WriteLine("  Based on Duan et al. (2025)");
        Console.WriteLine("===========================================\n");

        // Run all tests
        TestSimpleGraph();
        TestLinearGraph();
        TestCompleteGraph();
        TestSparseRandomGraph();
        TestDenseRandomGraph();
        TestDisconnectedGraph();
        TestSingleVertex();
        TestNegativeWeightFree();
        PerformanceComparison();

        Console.WriteLine("\n===========================================");
        Console.WriteLine("  All tests completed!");
        Console.WriteLine("===========================================");
    }

    static void TestSimpleGraph()
    {
        Console.WriteLine("Test 1: Simple Graph");
        Console.WriteLine("--------------------");

        // Simple graph:
        //     1
        //    / \
        //   2   3
        //    \ /
        //     4
        int n = 5;
        var edges = new List<(int, int, double)>
        {
            (0, 1, 1.0),  // 0 -> 1
            (0, 2, 4.0),  // 0 -> 2
            (1, 2, 2.0),  // 1 -> 2
            (1, 3, 5.0),  // 1 -> 3
            (2, 3, 1.0),  // 2 -> 3
            (3, 4, 3.0),  // 3 -> 4
        };

        var fastSSP = new FastSSSP(n, edges);
        var dijkstra = new Dijkstra(n, edges);

        var fastResult = fastSSP.ComputeShortestPaths(0);
        var dijkstraResult = dijkstra.ComputeShortestPaths(0);

        Console.WriteLine("Source: 0");
        Console.WriteLine("Expected (Dijkstra) vs Fast SSSP:");
        bool passed = true;
        for (int i = 0; i < n; i++)
        {
            bool match = Math.Abs(fastResult[i] - dijkstraResult[i]) < 1e-9;
            Console.WriteLine($"  Vertex {i}: Dijkstra={dijkstraResult[i]:F2}, FastSSP={fastResult[i]:F2} {(match ? "✓" : "✗")}");
            if (!match) passed = false;
        }
        Console.WriteLine($"Result: {(passed ? "PASSED" : "FAILED")}\n");
    }

    static void TestLinearGraph()
    {
        Console.WriteLine("Test 2: Linear Graph (Chain)");
        Console.WriteLine("----------------------------");

        int n = 10;
        var edges = new List<(int, int, double)>();
        for (int i = 0; i < n - 1; i++)
        {
            edges.Add((i, i + 1, 1.0));
        }

        var fastSSP = new FastSSSP(n, edges);
        var dijkstra = new Dijkstra(n, edges);

        var fastResult = fastSSP.ComputeShortestPaths(0);
        var dijkstraResult = dijkstra.ComputeShortestPaths(0);

        bool passed = CompareResults(fastResult, dijkstraResult);
        Console.WriteLine($"Graph: 0 -> 1 -> 2 -> ... -> {n - 1}");
        Console.WriteLine($"Expected distance to vertex {n - 1}: {n - 1}");
        Console.WriteLine($"Fast SSSP result: {fastResult[n - 1]}");
        Console.WriteLine($"Result: {(passed ? "PASSED" : "FAILED")}\n");
    }

    static void TestCompleteGraph()
    {
        Console.WriteLine("Test 3: Complete Graph (Small)");
        Console.WriteLine("------------------------------");

        int n = 6;
        var edges = new List<(int, int, double)>();
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i != j)
                {
                    // Direct edge has weight 10, via intermediate is cheaper
                    double weight = (j == (i + 1) % n) ? 1.0 : 10.0;
                    edges.Add((i, j, weight));
                }
            }
        }

        var fastSSP = new FastSSSP(n, edges);
        var dijkstra = new Dijkstra(n, edges);

        var fastResult = fastSSP.ComputeShortestPaths(0);
        var dijkstraResult = dijkstra.ComputeShortestPaths(0);

        bool passed = CompareResults(fastResult, dijkstraResult);
        Console.WriteLine($"Vertices: {n}, Edges: {edges.Count}");
        PrintResults(fastResult, dijkstraResult, Math.Min(n, 6));
        Console.WriteLine($"Result: {(passed ? "PASSED" : "FAILED")}\n");
    }

    static void TestSparseRandomGraph()
    {
        Console.WriteLine("Test 4: Sparse Random Graph");
        Console.WriteLine("---------------------------");

        int n = 100;
        int m = 300; // ~3 edges per vertex
        var edges = GenerateRandomGraph(n, m);

        var fastSSP = new FastSSSP(n, edges);
        var dijkstra = new Dijkstra(n, edges);

        var fastResult = fastSSP.ComputeShortestPaths(0);
        var dijkstraResult = dijkstra.ComputeShortestPaths(0);

        bool passed = CompareResults(fastResult, dijkstraResult);
        int reachable = fastResult.Count(d => d < double.PositiveInfinity);
        Console.WriteLine($"Vertices: {n}, Edges: {m}");
        Console.WriteLine($"Reachable vertices from source: {reachable}");
        Console.WriteLine($"Result: {(passed ? "PASSED" : "FAILED")}\n");
    }

    static void TestDenseRandomGraph()
    {
        Console.WriteLine("Test 5: Dense Random Graph");
        Console.WriteLine("--------------------------");

        int n = 50;
        int m = 500;
        var edges = GenerateRandomGraph(n, m);

        var fastSSP = new FastSSSP(n, edges);
        var dijkstra = new Dijkstra(n, edges);

        var fastResult = fastSSP.ComputeShortestPaths(0);
        var dijkstraResult = dijkstra.ComputeShortestPaths(0);

        bool passed = CompareResults(fastResult, dijkstraResult);
        Console.WriteLine($"Vertices: {n}, Edges: {m}");
        Console.WriteLine($"Result: {(passed ? "PASSED" : "FAILED")}\n");
    }

    static void TestDisconnectedGraph()
    {
        Console.WriteLine("Test 6: Disconnected Graph");
        Console.WriteLine("--------------------------");

        int n = 10;
        var edges = new List<(int, int, double)>
        {
            // Component 1: 0, 1, 2
            (0, 1, 1.0),
            (1, 2, 1.0),
            // Component 2: 3, 4, 5 (unreachable from 0)
            (3, 4, 1.0),
            (4, 5, 1.0),
        };

        var fastSSP = new FastSSSP(n, edges);
        var dijkstra = new Dijkstra(n, edges);

        var fastResult = fastSSP.ComputeShortestPaths(0);
        var dijkstraResult = dijkstra.ComputeShortestPaths(0);

        bool passed = CompareResults(fastResult, dijkstraResult);
        Console.WriteLine("Two disconnected components");
        Console.WriteLine($"Distance to vertex 2 (reachable): {fastResult[2]}");
        Console.WriteLine($"Distance to vertex 5 (unreachable): {(double.IsPositiveInfinity(fastResult[5]) ? "∞" : fastResult[5].ToString())}");
        Console.WriteLine($"Result: {(passed ? "PASSED" : "FAILED")}\n");
    }

    static void TestSingleVertex()
    {
        Console.WriteLine("Test 7: Single Vertex");
        Console.WriteLine("---------------------");

        int n = 1;
        var edges = new List<(int, int, double)>();

        var fastSSP = new FastSSSP(n, edges);
        var dijkstra = new Dijkstra(n, edges);

        var fastResult = fastSSP.ComputeShortestPaths(0);
        var dijkstraResult = dijkstra.ComputeShortestPaths(0);

        bool passed = CompareResults(fastResult, dijkstraResult);
        Console.WriteLine($"Distance to self: {fastResult[0]}");
        Console.WriteLine($"Result: {(passed ? "PASSED" : "FAILED")}\n");
    }

    static void TestNegativeWeightFree()
    {
        Console.WriteLine("Test 8: Various Edge Weights");
        Console.WriteLine("----------------------------");

        int n = 8;
        var edges = new List<(int, int, double)>
        {
            (0, 1, 0.5),
            (0, 2, 2.5),
            (1, 3, 1.5),
            (2, 3, 0.5),
            (3, 4, 3.0),
            (1, 5, 4.0),
            (5, 6, 0.1),
            (6, 7, 0.2),
            (4, 7, 1.0),
            (2, 5, 1.0),
        };

        var fastSSP = new FastSSSP(n, edges);
        var dijkstra = new Dijkstra(n, edges);

        var fastResult = fastSSP.ComputeShortestPaths(0);
        var dijkstraResult = dijkstra.ComputeShortestPaths(0);

        bool passed = CompareResults(fastResult, dijkstraResult);
        PrintResults(fastResult, dijkstraResult, n);
        Console.WriteLine($"Result: {(passed ? "PASSED" : "FAILED")}\n");
    }

    static void PerformanceComparison()
    {
        Console.WriteLine("Test 9: Performance Comparison");
        Console.WriteLine("------------------------------");

        int[] sizes = { 100, 500, 1000, 2000 };

        foreach (int n in sizes)
        {
            int m = n * 3; // Sparse graph
            var edges = GenerateRandomGraph(n, m);

            var fastSSP = new FastSSSP(n, edges);
            var dijkstra = new Dijkstra(n, edges);

            // Warm up
            fastSSP.ComputeShortestPaths(0);
            dijkstra.ComputeShortestPaths(0);

            // Time Fast SSSP
            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                fastSSP = new FastSSSP(n, edges);
                fastSSP.ComputeShortestPaths(0);
            }
            sw1.Stop();
            double fastTime = sw1.ElapsedMilliseconds / 5.0;

            // Time Dijkstra
            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                dijkstra = new Dijkstra(n, edges);
                dijkstra.ComputeShortestPaths(0);
            }
            sw2.Stop();
            double dijkstraTime = sw2.ElapsedMilliseconds / 5.0;

            // Verify correctness
            fastSSP = new FastSSSP(n, edges);
            dijkstra = new Dijkstra(n, edges);
            bool correct = CompareResults(fastSSP.ComputeShortestPaths(0), dijkstra.ComputeShortestPaths(0));

            Console.WriteLine($"n={n,5}, m={m,6}: FastSSP={fastTime,8:F2}ms, Dijkstra={dijkstraTime,8:F2}ms, Correct={correct}");
        }
        Console.WriteLine();
    }

    static List<(int, int, double)> GenerateRandomGraph(int n, int m)
    {
        var edges = new List<(int, int, double)>();
        var existing = new HashSet<(int, int)>();

        // Ensure connectivity from vertex 0
        for (int i = 1; i < n && edges.Count < m; i++)
        {
            int from = rand.Next(i);
            edges.Add((from, i, rand.NextDouble() * 10 + 0.1));
            existing.Add((from, i));
        }

        // Add random edges
        while (edges.Count < m)
        {
            int from = rand.Next(n);
            int to = rand.Next(n);
            if (from != to && !existing.Contains((from, to)))
            {
                edges.Add((from, to, rand.NextDouble() * 10 + 0.1));
                existing.Add((from, to));
            }
        }

        return edges;
    }

    static bool CompareResults(double[] a, double[] b, double epsilon = 1e-9)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (double.IsPositiveInfinity(a[i]) && double.IsPositiveInfinity(b[i]))
                continue;
            if (Math.Abs(a[i] - b[i]) > epsilon)
                return false;
        }
        return true;
    }

    static void PrintResults(double[] fast, double[] dijkstra, int limit)
    {
        for (int i = 0; i < Math.Min(fast.Length, limit); i++)
        {
            string f = double.IsPositiveInfinity(fast[i]) ? "∞" : fast[i].ToString("F2");
            string d = double.IsPositiveInfinity(dijkstra[i]) ? "∞" : dijkstra[i].ToString("F2");
            bool match = Math.Abs(fast[i] - dijkstra[i]) < 1e-9 ||
                        (double.IsPositiveInfinity(fast[i]) && double.IsPositiveInfinity(dijkstra[i]));
            Console.WriteLine($"  Vertex {i}: Dijkstra={d,8}, FastSSP={f,8} {(match ? "✓" : "✗")}");
        }
    }
}
