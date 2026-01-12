using System;
using System.Collections.Generic;
using System.Linq;

namespace ShortestPathAlgorithm
{
    /// <summary>
    /// Implementation of the O(m log^(2/3) n) SSSP algorithm from
    /// "Breaking the Sorting Barrier for Directed Single-Source Shortest Paths"
    /// by Duan, Mao, Mao, Shu, and Yin (2025)
    /// </summary>
    public class FastSSSP
    {
        private readonly int n; // number of vertices
        private readonly int m; // number of edges
        private readonly List<(int to, double weight)>[] adj; // adjacency list

        // Algorithm parameters
        private readonly int k; // log^(1/3)(n)
        private readonly int t; // log^(2/3)(n)
        private readonly int maxLevel; // ceil(log(n)/t)

        // Global state
        private double[] dHat; // distance estimates
        private int[] pred; // predecessor in shortest path
        private int[] pathLength; // number of edges in path (for tie-breaking)

        public FastSSSP(int vertices, List<(int from, int to, double weight)> edges)
        {
            n = vertices;
            m = edges.Count;

            // Compute parameters
            double logN = Math.Max(1, Math.Log2(n));
            k = Math.Max(2, (int)Math.Floor(Math.Pow(logN, 1.0 / 3.0)));
            t = Math.Max(1, (int)Math.Floor(Math.Pow(logN, 2.0 / 3.0)));
            maxLevel = (int)Math.Ceiling(logN / t);

            // Build adjacency list
            adj = new List<(int, double)>[n];
            for (int i = 0; i < n; i++)
                adj[i] = new List<(int, double)>();

            foreach (var (from, to, weight) in edges)
            {
                if (from >= 0 && from < n && to >= 0 && to < n)
                    adj[from].Add((to, weight));
            }

            // Initialize distance estimates
            dHat = new double[n];
            pred = new int[n];
            pathLength = new int[n];
        }

        /// <summary>
        /// Compute shortest paths from source vertex
        /// </summary>
        public double[] ComputeShortestPaths(int source)
        {
            // Initialize
            for (int i = 0; i < n; i++)
            {
                dHat[i] = double.PositiveInfinity;
                pred[i] = -1;
                pathLength[i] = int.MaxValue;
            }
            dHat[source] = 0;
            pred[source] = source;
            pathLength[source] = 0;

            // Call BMSSP with S = {source}, B = infinity
            var S = new HashSet<int> { source };
            BMSSP(maxLevel, double.PositiveInfinity, S);

            return (double[])dHat.Clone();
        }

        /// <summary>
        /// Compare two paths using lexicographic ordering (length, pathLength, vertex sequence)
        /// Returns true if (distU, pathLenU, u) <= (distV, pathLenV, v)
        /// </summary>
        private bool ComparePaths(double distU, int pathLenU, int u, double distV, int pathLenV, int v)
        {
            if (distU != distV) return distU < distV;
            if (pathLenU != pathLenV) return pathLenU < pathLenV;
            return u <= v;
        }

        /// <summary>
        /// Relax edge (u, v) with weight w
        /// Returns true if relaxation was successful
        /// </summary>
        private bool Relax(int u, int v, double w)
        {
            double newDist = dHat[u] + w;
            int newPathLen = pathLength[u] + 1;

            if (ComparePaths(newDist, newPathLen, u, dHat[v], pathLength[v], pred[v]))
            {
                dHat[v] = newDist;
                pred[v] = u;
                pathLength[v] = newPathLen;
                return true;
            }
            return false;
        }

        /// <summary>
        /// FindPivots procedure (Algorithm 1)
        /// Performs k relaxation steps and identifies pivot vertices
        /// </summary>
        private (HashSet<int> P, HashSet<int> W) FindPivots(double B, HashSet<int> S)
        {
            var W = new HashSet<int>(S);
            var layers = new List<HashSet<int>>();
            layers.Add(new HashSet<int>(S));

            // Relax for k steps
            for (int i = 1; i <= k; i++)
            {
                var Wi = new HashSet<int>();

                foreach (int u in layers[i - 1])
                {
                    foreach (var (v, w) in adj[u])
                    {
                        double newDist = dHat[u] + w;
                        if (ComparePaths(newDist, pathLength[u] + 1, u, dHat[v], pathLength[v], pred[v]))
                        {
                            dHat[v] = newDist;
                            pred[v] = u;
                            pathLength[v] = pathLength[u] + 1;

                            if (newDist < B)
                            {
                                Wi.Add(v);
                            }
                        }
                    }
                }

                foreach (int v in Wi)
                    W.Add(v);

                layers.Add(Wi);

                // Early termination if W gets too large
                if (W.Count > k * S.Count)
                {
                    return (new HashSet<int>(S), W);
                }
            }

            // Build forest F and find pivots
            // F contains edges (u,v) where u,v in W and dHat[v] = dHat[u] + w(u,v)
            var parent = new Dictionary<int, int>();
            var children = new Dictionary<int, List<int>>();

            foreach (int u in W)
            {
                children[u] = new List<int>();
            }

            foreach (int u in W)
            {
                foreach (var (v, w) in adj[u])
                {
                    if (W.Contains(v) && Math.Abs(dHat[v] - (dHat[u] + w)) < 1e-12)
                    {
                        if (!parent.ContainsKey(v) || ComparePaths(dHat[u], pathLength[u], u,
                            dHat[parent[v]] - w, pathLength[parent[v]] - 1, parent[v]))
                        {
                            if (parent.ContainsKey(v) && children.ContainsKey(parent[v]))
                            {
                                children[parent[v]].Remove(v);
                            }
                            parent[v] = u;
                            if (!children.ContainsKey(u))
                                children[u] = new List<int>();
                            children[u].Add(v);
                        }
                    }
                }
            }

            // Find roots in S with subtree size >= k
            var subtreeSize = new Dictionary<int, int>();

            int ComputeSubtreeSize(int v)
            {
                if (subtreeSize.ContainsKey(v))
                    return subtreeSize[v];

                int size = 1;
                if (children.ContainsKey(v))
                {
                    foreach (int child in children[v])
                    {
                        size += ComputeSubtreeSize(child);
                    }
                }
                subtreeSize[v] = size;
                return size;
            }

            var P = new HashSet<int>();
            foreach (int u in S)
            {
                if (!parent.ContainsKey(u) || !W.Contains(parent[u]))
                {
                    // u is a root
                    int size = ComputeSubtreeSize(u);
                    if (size >= k)
                    {
                        P.Add(u);
                    }
                }
            }

            // If no pivots found, use all of S
            if (P.Count == 0)
                P = new HashSet<int>(S);

            return (P, W);
        }

        /// <summary>
        /// Base case: mini Dijkstra for l=0
        /// </summary>
        private (double BPrime, HashSet<int> U) BaseCase(double B, HashSet<int> S)
        {
            if (S.Count == 0)
                return (B, new HashSet<int>());

            int x = S.First();
            var U0 = new HashSet<int> { x };

            // Priority queue: (distance, vertex)
            var pq = new SortedSet<(double dist, int pathLen, int vertex)>(
                Comparer<(double, int, int)>.Create((a, b) =>
                {
                    int cmp = a.Item1.CompareTo(b.Item1);
                    if (cmp != 0) return cmp;
                    cmp = a.Item2.CompareTo(b.Item2);
                    if (cmp != 0) return cmp;
                    return a.Item3.CompareTo(b.Item3);
                }));

            var inQueue = new Dictionary<int, (double, int)>();
            pq.Add((dHat[x], pathLength[x], x));
            inQueue[x] = (dHat[x], pathLength[x]);

            while (pq.Count > 0 && U0.Count < k + 1)
            {
                var (dist, pathLen, u) = pq.Min;
                pq.Remove(pq.Min);
                inQueue.Remove(u);

                if (!U0.Contains(u))
                    U0.Add(u);

                foreach (var (v, w) in adj[u])
                {
                    double newDist = dHat[u] + w;
                    int newPathLen = pathLength[u] + 1;

                    if (newDist < B && ComparePaths(newDist, newPathLen, u, dHat[v], pathLength[v], pred[v]))
                    {
                        // Remove old entry if exists
                        if (inQueue.ContainsKey(v))
                        {
                            var old = inQueue[v];
                            pq.Remove((old.Item1, old.Item2, v));
                        }

                        dHat[v] = newDist;
                        pred[v] = u;
                        pathLength[v] = newPathLen;

                        if (!U0.Contains(v))
                        {
                            pq.Add((newDist, newPathLen, v));
                            inQueue[v] = (newDist, newPathLen);
                        }
                    }
                }
            }

            if (U0.Count <= k)
            {
                return (B, U0);
            }
            else
            {
                double maxDist = U0.Max(v => dHat[v]);
                var U = new HashSet<int>(U0.Where(v => dHat[v] < maxDist));
                return (maxDist, U);
            }
        }

        /// <summary>
        /// Bounded Multi-Source Shortest Path (Algorithm 3)
        /// </summary>
        private (double BPrime, HashSet<int> U) BMSSP(int level, double B, HashSet<int> S)
        {
            if (S.Count == 0)
                return (B, new HashSet<int>());

            // Base case
            if (level == 0)
            {
                return BaseCase(B, S);
            }

            // FindPivots
            var (P, W) = FindPivots(B, S);

            if (P.Count == 0)
            {
                return (B, new HashSet<int>(W.Where(v => dHat[v] < B)));
            }

            // Initialize data structure D
            var D = new PartialSortingDS((int)Math.Pow(2, (level - 1) * t), B);

            foreach (int x in P)
            {
                if (dHat[x] < B)
                    D.Insert(x, dHat[x]);
            }

            double BPrime0 = P.Where(x => dHat[x] < double.PositiveInfinity).Any()
                ? P.Where(x => dHat[x] < double.PositiveInfinity).Min(x => dHat[x])
                : B;

            var U = new HashSet<int>();
            double currentBPrime = BPrime0;
            int maxUSize = (int)(k * Math.Pow(2, level * t));

            int iteration = 0;
            while (U.Count < maxUSize && !D.IsEmpty())
            {
                iteration++;

                // Pull from D
                var (Bi, Si) = D.Pull();

                if (Si.Count == 0)
                    break;

                // Recursive call
                var (BiPrime, Ui) = BMSSP(level - 1, Bi, Si);

                // Add Ui to U
                foreach (int v in Ui)
                    U.Add(v);

                currentBPrime = BiPrime;

                // Relax edges from Ui
                var K = new List<(int vertex, double dist)>();

                foreach (int u in Ui)
                {
                    foreach (var (v, w) in adj[u])
                    {
                        double newDist = dHat[u] + w;
                        if (ComparePaths(newDist, pathLength[u] + 1, u, dHat[v], pathLength[v], pred[v]))
                        {
                            dHat[v] = newDist;
                            pred[v] = u;
                            pathLength[v] = pathLength[u] + 1;

                            if (newDist >= Bi && newDist < B)
                            {
                                D.Insert(v, newDist);
                            }
                            else if (newDist >= BiPrime && newDist < Bi)
                            {
                                K.Add((v, newDist));
                            }
                        }
                    }
                }

                // Batch prepend K and vertices from Si with updated distances
                foreach (int x in Si)
                {
                    if (dHat[x] >= BiPrime && dHat[x] < Bi)
                    {
                        K.Add((x, dHat[x]));
                    }
                }

                if (K.Count > 0)
                {
                    D.BatchPrepend(K);
                }

                // Check for large workload
                if (U.Count >= maxUSize)
                {
                    break;
                }
            }

            // Add vertices from W with distance < BPrime
            double finalBPrime = D.IsEmpty() ? B : currentBPrime;
            foreach (int x in W)
            {
                if (dHat[x] < finalBPrime)
                {
                    U.Add(x);
                }
            }

            return (finalBPrime, U);
        }
    }

    /// <summary>
    /// Partial sorting data structure from Lemma 3.3
    /// Supports Insert, BatchPrepend, and Pull operations
    /// </summary>
    public class PartialSortingDS
    {
        private readonly int M; // block size parameter
        private readonly double upperBound;

        // Using SortedDictionary for simplicity (not optimal but correct)
        private SortedDictionary<double, HashSet<int>> buckets;
        private Dictionary<int, double> vertexToValue;

        public PartialSortingDS(int m, double B)
        {
            M = Math.Max(1, m);
            upperBound = B;
            buckets = new SortedDictionary<double, HashSet<int>>();
            vertexToValue = new Dictionary<int, double>();
        }

        public bool IsEmpty() => vertexToValue.Count == 0;

        public void Insert(int key, double value)
        {
            if (value >= upperBound)
                return;

            // Remove old entry if exists
            if (vertexToValue.TryGetValue(key, out double oldValue))
            {
                if (value >= oldValue)
                    return; // New value is not better

                if (buckets.TryGetValue(oldValue, out var bucket))
                {
                    bucket.Remove(key);
                    if (bucket.Count == 0)
                        buckets.Remove(oldValue);
                }
            }

            // Insert new entry
            vertexToValue[key] = value;
            if (!buckets.ContainsKey(value))
                buckets[value] = new HashSet<int>();
            buckets[value].Add(key);
        }

        public void BatchPrepend(List<(int vertex, double dist)> items)
        {
            foreach (var (vertex, dist) in items)
            {
                Insert(vertex, dist);
            }
        }

        public (double bound, HashSet<int> vertices) Pull()
        {
            if (vertexToValue.Count == 0)
                return (upperBound, new HashSet<int>());

            var result = new HashSet<int>();
            double lastValue = 0;

            // Get up to M vertices with smallest values
            while (result.Count < M && buckets.Count > 0)
            {
                var firstKey = buckets.Keys.First();
                var bucket = buckets[firstKey];

                foreach (int v in bucket.ToList())
                {
                    if (result.Count >= M)
                        break;

                    result.Add(v);
                    lastValue = firstKey;
                    bucket.Remove(v);
                    vertexToValue.Remove(v);
                }

                if (bucket.Count == 0)
                    buckets.Remove(firstKey);
            }

            // Determine bound
            double bound;
            if (buckets.Count == 0)
            {
                bound = upperBound;
            }
            else
            {
                bound = buckets.Keys.First();
            }

            return (bound, result);
        }
    }

    /// <summary>
    /// Standard Dijkstra's algorithm for comparison
    /// </summary>
    public class Dijkstra
    {
        private readonly int n;
        private readonly List<(int to, double weight)>[] adj;

        public Dijkstra(int vertices, List<(int from, int to, double weight)> edges)
        {
            n = vertices;
            adj = new List<(int, double)>[n];
            for (int i = 0; i < n; i++)
                adj[i] = new List<(int, double)>();

            foreach (var (from, to, weight) in edges)
            {
                if (from >= 0 && from < n && to >= 0 && to < n)
                    adj[from].Add((to, weight));
            }
        }

        public double[] ComputeShortestPaths(int source)
        {
            var dist = new double[n];
            for (int i = 0; i < n; i++)
                dist[i] = double.PositiveInfinity;
            dist[source] = 0;

            var pq = new SortedSet<(double dist, int vertex)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    int cmp = a.Item1.CompareTo(b.Item1);
                    return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
                }));

            pq.Add((0, source));

            while (pq.Count > 0)
            {
                var (d, u) = pq.Min;
                pq.Remove(pq.Min);

                if (d > dist[u])
                    continue;

                foreach (var (v, w) in adj[u])
                {
                    double newDist = dist[u] + w;
                    if (newDist < dist[v])
                    {
                        pq.Remove((dist[v], v));
                        dist[v] = newDist;
                        pq.Add((newDist, v));
                    }
                }
            }

            return dist;
        }
    }
}
