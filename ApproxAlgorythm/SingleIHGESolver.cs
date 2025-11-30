using GraphLibrary.Model;
using System;
using System.Collections.Generic;
using System.Linq;
namespace IHGEAlgorithm;

public class SingleIHGESolver
{
    private readonly Graph<int> g1;
    private readonly Graph<int> g2;
    private readonly bool isDirected;
    private int nextPlaceholderId;
    private int maxIterations = 500;
    private readonly Random rnd = new Random(0);

public SingleIHGESolver(Graph<int> g1, Graph<int> g2)
    {
        this.g1 = g1;
        this.g2 = g2;
        this.isDirected = g1.IsDirected;
        this.nextPlaceholderId = g2.Vertices.Any() ? g2.Vertices.Max() + 1 : 1000;
    }

    public SearchResult FindSingleCopy()
    {
        var v1List = g1.Vertices.ToList();
        int n1 = v1List.Count;

        var baseCandidates = new List<int>();
        baseCandidates.AddRange(g2.Vertices);
        for (int i = 0; i < n1; i++)
            baseCandidates.Add(nextPlaceholderId + i);

        Dictionary<int, int>? bestPhi = null;
        int bestCost = int.MaxValue;

        int restarts = 15; 
        for (int run = 0; run < restarts; run++)
        {
            var candidates = baseCandidates.OrderBy(x => rnd.Next()).ToList();

            var phi = new Dictionary<int, int>();
            for (int i = 0; i < n1; i++)
                phi[v1List[i]] = candidates[i];

            bool changed = true;
            int stableiterations = 0;
            int iter = 0;

            while (stableiterations < 5 && iter < maxIterations)
            {
                iter++;
                changed = false;

                var costMatrix = BuildCostMatrix(v1List, candidates, phi);

                // Hungarian
                var hungarian = new HungarianAlgorithm(costMatrix);
                var assignment = hungarian.Solve();

                var newPhi = new Dictionary<int, int>();
                for (int i = 0; i < n1; i++)
                    newPhi[v1List[i]] = candidates[assignment[i]];

                newPhi = LocalImprove(newPhi, v1List, candidates);

                if (!MappingsEqual(phi, newPhi))
                {
                    changed = true;
                    phi = newPhi;
                }
            }

            int cost = ComputeTrueCost(phi);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestPhi = new Dictionary<int, int>(phi);
            }

            if (changed == true) stableiterations++; else stableiterations = 0;
            if (bestCost == 0) break;
        }

        var mapping = BuildMapping(bestPhi ?? new Dictionary<int, int>());
        return new SearchResult(new List<Mapping> { mapping }, mapping.Cost);
    }

    private double[,] BuildCostMatrix(List<int> v1List, List<int> candidates, Dictionary<int, int> phi)
    {
        int n1 = v1List.Count;
        int nC = candidates.Count;
        var cost = new double[n1, nC];

        for (int i = 0; i < n1; i++)
        {
            int u = v1List[i];

            for (int j = 0; j < nC; j++)
            {
                int c = candidates[j];
                double w = 0;

                bool isPlaceholder = !g2.ContainsVertex(c);
                if (isPlaceholder) w += 1.0; 
                foreach (var t in v1List)
                {
                    if (t == u) continue;

                    if (phi.TryGetValue(t, out int c_t))
                    {
                        if (g1.HasEdge(u, t) && !g2.HasEdge(c, c_t)) w += 1.0;
                        if (g1.HasEdge(t, u) && !g2.HasEdge(c_t, c)) w += 1.0;
                    }
                    else
                    {
                        if (g1.HasEdge(u, t)) w += 1.0;
                        if (g1.HasEdge(t, u)) w += 1.0;
                    }
                }

                cost[i, j] = w;
            }
        }

        return cost;
    }

    private int ComputeTrueCost(Dictionary<int, int> phi)
    {
        int cost = 0;
        var addedV = new HashSet<int>();
        foreach (var v in phi.Values)
            if (!g2.ContainsVertex(v))
                addedV.Add(v);
        cost += addedV.Count;

        foreach (var e in g1.GetAllEdges())
        {
            int u = phi[e.From];
            int v = phi[e.To];
            if (!g2.HasEdge(u, v))
                cost += 1;
        }

        return cost;
    }

    private Dictionary<int, int> LocalImprove(Dictionary<int, int> phi, List<int> v1List, List<int> candidates)
    {
        var currentPhi = new Dictionary<int, int>(phi);
        int bestCost = ComputeTrueCost(currentPhi);
        bool improved = true;

        while (improved)
        {
            improved = false;

            var used = new HashSet<int>(currentPhi.Values);
            var unused = candidates.Where(c => !used.Contains(c)).ToList();

            foreach (var u in v1List)
            {
                int old = currentPhi[u];
                foreach (var cand in unused)
                {
                    if (cand == old) continue;
                    currentPhi[u] = cand;
                    int c = ComputeTrueCost(currentPhi);
                    if (c < bestCost)
                    {
                        bestCost = c;
                        improved = true;
                        used.Remove(old); used.Add(cand);
                        unused = candidates.Where(x => !used.Contains(x)).ToList();
                        break;
                    }
                    else
                    {
                        currentPhi[u] = old; 
                    }
                }
                if (improved) break;
            }
            if (improved) continue;

            for (int i = 0; i < v1List.Count && !improved; i++)
            {
                for (int j = i + 1; j < v1List.Count && !improved; j++)
                {
                    int ui = v1List[i], uj = v1List[j];
                    int a = currentPhi[ui], b = currentPhi[uj];
                    if (a == b) continue;
                    // swap
                    currentPhi[ui] = b;
                    currentPhi[uj] = a;
                    int c = ComputeTrueCost(currentPhi);
                    if (c < bestCost)
                    {
                        bestCost = c;
                        improved = true;
                        break;
                    }
                    else
                    {
                        // revert
                        currentPhi[ui] = a;
                        currentPhi[uj] = b;
                    }
                }
            }
        }

        return currentPhi;
    }

    private Mapping BuildMapping(Dictionary<int, int> phi)
    {
        var addedV = new HashSet<int>();
        var addedE = new List<Edge<int>>();

        foreach (var v in phi.Values)
            if (!g2.ContainsVertex(v))
                addedV.Add(v);

        foreach (var edge in g1.GetAllEdges())
        {
            int u = phi[edge.From];
            int v = phi[edge.To];

            if (!g2.HasEdge(u, v))
                addedE.Add(new Edge<int>(u, v, 1.0, isDirected));
        }

        return new Mapping(phi, addedV, addedE);
    }

    private bool MappingsEqual(Dictionary<int, int> phi1, Dictionary<int, int> phi2)
    {
        if (phi1.Count != phi2.Count) return false;
        foreach (var kv in phi1)
        {
            if (!phi2.TryGetValue(kv.Key, out int v) || v != kv.Value) return false;
        }
        return true;
    }
}
