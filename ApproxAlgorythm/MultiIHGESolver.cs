using GraphLibrary.Model;
using System;
using System.Collections.Generic;
using System.Linq;
namespace IHGEAlgorithm;
public class MultiIHGESolver
{
    private readonly Graph<int> g1;
    private readonly Graph<int> g2;
    private readonly bool isDirected;
    private int nextPlaceholderId;
    private int maxIterations = 500;
    private readonly Random rnd;
    private double gamma = 0.1;

    public MultiIHGESolver(Graph<int> g1, Graph<int> g2, int seed = 0)
    {
        this.g1 = g1;
        this.g2 = g2;
        this.isDirected = g1.IsDirected;
        this.nextPlaceholderId = g2.Vertices.Any() ? g2.Vertices.Max() + 1 : 1000;
        this.rnd = new Random(seed);
    }

    public SearchResult FindKCopies(int k)
    {
        if (k == 1) gamma = 0;
        
        if (k <= 0) throw new ArgumentException("k must be positive");

        var v1List = g1.Vertices.ToList();
        int n1 = v1List.Count;


        var baseCandidates = new List<int>();
        baseCandidates.AddRange(g2.Vertices);
        for (int i = 0; i < n1 * k; i++)
            baseCandidates.Add(nextPlaceholderId + i);

        List<Dictionary<int, int>> bestPhis = new List<Dictionary<int, int>>();
        int bestCost = int.MaxValue;

        int restarts = Math.Max(10, 8 + k * 1);
        for (int run = 0; run < restarts; run++)
        {
            var phis = InitializePhis(k, v1List, baseCandidates);


            int stableIterations = 0;
            int iter = 0;

            while (stableIterations < 5 && iter < maxIterations)
            {
                iter++;


                var newPhis = new List<Dictionary<int, int>>();

                for (int r = 0; r < k; r++)
                {
                    var costMatrix = BuildCostMatrixForLayer(r, v1List, baseCandidates, phis, newPhis);
                    var hungarian = new HungarianAlgorithm(costMatrix);
                    var assignment = hungarian.Solve();

                    var newPhi = new Dictionary<int, int>();
                    for (int i = 0; i < n1; i++)
                        newPhi[v1List[i]] = baseCandidates[assignment[i]];

                   if (k!=1) newPhi = EnforceDifference(newPhi, newPhis, v1List, baseCandidates);

                    newPhis.Add(newPhi);
                }

                newPhis = LocalImproveAll(newPhis, v1List, baseCandidates);

                if (k!=1) newPhis = EnsureAllDifferent(newPhis, v1List, baseCandidates);

                if (!AllMappingsEqual(phis, newPhis))
                {

                    stableIterations = 0;
                }
                else
                {
                    stableIterations++;
                }

                phis = newPhis;
            }


            phis = MergePlaceholders(phis);

            if (HasIdenticalLayers(phis) && (k != 1))
            {
                continue;
            }

            int cost = ComputeGlobalCost(phis);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestPhis = phis.Select(p => new Dictionary<int, int>(p)).ToList();
            }

            if (bestCost == 0 && !HasIdenticalLayers(bestPhis)) break;
        }

        var mappings = bestPhis.Select(phi => BuildMapping(phi)).ToList();
        int finalCost = ComputeGlobalCost(bestPhis);

        return new SearchResult(mappings, finalCost);
    }

    private List<Dictionary<int, int>> InitializePhis(int k, List<int> v1List, List<int> candidates)
    {
        var phis = new List<Dictionary<int, int>>();
        var usedSets = new List<HashSet<int>>();

        for (int r = 0; r < k; r++)
        {
            Dictionary<int, int> phi = null;
            int attempts = 0;

            while (attempts < 100)
            {
                var shuffled = candidates.OrderBy(x => rnd.Next()).ToList();
                phi = new Dictionary<int, int>();

                for (int i = 0; i < v1List.Count; i++)
                {
                    phi[v1List[i]] = shuffled[i];
                }

                var currentSet = phi.Values.Where(v => g2.ContainsVertex(v)).ToHashSet();
                bool isDifferent = true;

                foreach (var prevSet in usedSets)
                {
                    if (currentSet.SetEquals(prevSet))
                    {
                        isDifferent = false;
                        break;
                    }
                }

                if (isDifferent)
                {
                    usedSets.Add(currentSet);
                    break;
                }

                attempts++;
            }

            phis.Add(phi);
        }

        return phis;
    }

    private double[,] BuildCostMatrixForLayer(int layerIdx, List<int> v1List,
        List<int> candidates, List<Dictionary<int, int>> oldPhis, List<Dictionary<int, int>> newPhis)
    {
        int n1 = v1List.Count;
        int nC = candidates.Count;
        var cost = new double[n1, nC];
        var currentPhi = oldPhis[layerIdx];

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

                    if (currentPhi.TryGetValue(t, out int c_t))
                    {
                        if (g1.HasEdge(u, t))
                        {
                            if (!g2.HasEdge(c, c_t))
                            {

                                var origEdge = g1.GetAllEdges().FirstOrDefault(e => e.From == u && e.To == t);
                                if (origEdge != null)
                                {
                                    bool bothPlaceholders = isPlaceholder && !g2.ContainsVertex(c_t);
                                    if (!bothPlaceholders || u < t) 
                                    {
                                        w += origEdge.IsDirected ? 1.0 : 2.0;
                                    }
                                }
                            }
                        }

                        if (g1.HasEdge(t, u))
                        {
                            if (!g2.HasEdge(c_t, c))
                            {
                                var origEdge = g1.GetAllEdges().FirstOrDefault(e => e.From == t && e.To == u);
                                if (origEdge != null)
                                {
                                    bool bothPlaceholders = isPlaceholder && !g2.ContainsVertex(c_t);
                                    if (!bothPlaceholders || u < t)
                                    {
                                        w += origEdge.IsDirected ? 1.0 : 2.0;
                                    }
                                }
                            }
                        }
                    }
                }

                if (!isPlaceholder)
                {
                    foreach (var completedPhi in newPhis)
                    {
                        if (completedPhi.ContainsValue(c))
                        {
                            w += gamma; // Silna kara
                        }
                    }

                    for (int s = 0; s < layerIdx; s++)
                    {
                        if (oldPhis[s].TryGetValue(u, out int prev) && prev == c)
                        {
                            w += gamma;
                        }
                    }
                }

                cost[i, j] = w;
            }
        }

        return cost;
    }

    private Dictionary<int, int> EnforceDifference(Dictionary<int, int> phi,
        List<Dictionary<int, int>> previousPhis, List<int> v1List, List<int> candidates)
    {
        foreach (var prevPhi in previousPhis)
        {
            if (AreMappingsIdenticalInG2(phi, prevPhi))
            {
                return FindCheapestDifference(phi, prevPhi, v1List, candidates);
            }
        }
        return phi;
    }

    private bool AreMappingsIdenticalInG2(Dictionary<int, int> phi1, Dictionary<int, int> phi2)
    {
        var set1 = phi1.Values.Where(v => g2.ContainsVertex(v)).ToHashSet();
        var set2 = phi2.Values.Where(v => g2.ContainsVertex(v)).ToHashSet();

        return set1.SetEquals(set2);
    }

    private Dictionary<int, int> FindCheapestDifference(Dictionary<int, int> phi,
        Dictionary<int, int> conflictPhi, List<int> v1List, List<int> candidates)
    {
        var bestPhi = new Dictionary<int, int>(phi);
        int bestCost = int.MaxValue;

        foreach (var u in v1List)
        {
            int original = phi[u];

            foreach (var c in candidates.Where(x => !g2.ContainsVertex(x)))
            {
                if (c == original) continue;

                phi[u] = c;

                if (!AreMappingsIdenticalInG2(phi, conflictPhi))
                {
                    int newCost = ComputeSingleMappingCost(phi);
                    if (newCost < bestCost)
                    {
                        bestCost = newCost;
                        bestPhi = new Dictionary<int, int>(phi);
                    }
                }
            }

            phi[u] = original;
        }

        if (AreMappingsIdenticalInG2(bestPhi, conflictPhi))
        {
            var usedInConflict = conflictPhi.Values.Where(v => g2.ContainsVertex(v)).ToHashSet();

            foreach (var u in v1List)
            {
                int original = phi[u];

                foreach (var c in candidates.Where(x => g2.ContainsVertex(x) && !usedInConflict.Contains(x)))
                {
                    if (c == original) continue;

                    phi[u] = c;

                    if (!AreMappingsIdenticalInG2(phi, conflictPhi))
                    {
                        int newCost = ComputeSingleMappingCost(phi);
                        if (newCost < bestCost)
                        {
                            bestCost = newCost;
                            bestPhi = new Dictionary<int, int>(phi);
                        }
                    }
                }

                phi[u] = original;
            }
        }

        return bestPhi;
    }
    private int ComputeSingleMappingCost(Dictionary<int, int> phi)
    {
        int cost = 0;
        var reverse = new Dictionary<int, int>();
        foreach (var (u, v) in phi)
        {
            if (g2.ContainsVertex(v))
            {
                if (reverse.ContainsKey(v))
                    cost += 1000000;
                else
                    reverse[v] = u;
            }
        }
        foreach (var v in phi.Values)
            if (!g2.ContainsVertex(v))
                cost++;

        foreach (var edge in g1.GetAllEdges())
        {
            int u = phi[edge.From];
            int v = phi[edge.To];

            if (edge.IsDirected)
            {
                if (!g2.HasEdge(u, v))
                    cost += 1;
            }
            else
            {
                if (g2.IsDirected)
                {

                    if (!g2.HasEdge(u, v)) cost += 1;
                    if (!g2.HasEdge(v, u)) cost += 1;
                }
                else
                {

                    if (!g2.HasEdge(u, v) && !g2.HasEdge(v, u))
                        cost += 2;
                }
            }
        }

        return cost;
    }

    private List<Dictionary<int, int>> EnsureAllDifferent(List<Dictionary<int, int>> phis,
        List<int> v1List, List<int> candidates)
    {
        var result = new List<Dictionary<int, int>>();

        foreach (var phi in phis)
        {
            var fixedPhi = phi;

            foreach (var existing in result)
            {
                if (AreMappingsIdenticalInG2(fixedPhi, existing))
                {
                    fixedPhi = FindCheapestDifference(fixedPhi, existing, v1List, candidates);
                }
            }

            result.Add(fixedPhi);
        }

        return result;
    }

    private List<Dictionary<int, int>> LocalImproveAll(List<Dictionary<int, int>> phis,
        List<int> v1List, List<int> candidates)
    {
        var currentPhis = phis.Select(p => new Dictionary<int, int>(p)).ToList();
        int bestCost = ComputeGlobalCost(currentPhis);
        bool improved = true;

        while (improved)
        {
            improved = false;

            for (int r = 0; r < currentPhis.Count; r++)
            {
                var used = new HashSet<int>();
                foreach (var p in currentPhis)
                    foreach (var v in p.Values)
                        used.Add(v);

                var unused = candidates.Where(c => !used.Contains(c)).ToList();

                foreach (var u in v1List)
                {
                    int old = currentPhis[r][u];

                    foreach (var cand in unused)
                    {
                        if (cand == old) continue;
                        currentPhis[r][u] = cand;
                        int c = ComputeGlobalCost(currentPhis);

                        if (c < bestCost && !HasIdenticalLayers(currentPhis))
                        {
                            bestCost = c;
                            improved = true;
                            used.Remove(old);
                            used.Add(cand);
                            unused = candidates.Where(x => !used.Contains(x)).ToList();
                            break;
                        }
                        else
                        {
                            currentPhis[r][u] = old;
                        }
                    }
                    if (improved) break;
                }
                if (improved) break;
            }

            if (improved) continue;

            for (int r = 0; r < currentPhis.Count && !improved; r++)
            {
                for (int i = 0; i < v1List.Count && !improved; i++)
                {
                    for (int j = i + 1; j < v1List.Count && !improved; j++)
                    {
                        int ui = v1List[i], uj = v1List[j];
                        int a = currentPhis[r][ui], b = currentPhis[r][uj];
                        if (a == b) continue;

                        currentPhis[r][ui] = b;
                        currentPhis[r][uj] = a;

                        int c = ComputeGlobalCost(currentPhis);

                        if (c < bestCost && !HasIdenticalLayers(currentPhis))
                        {
                            bestCost = c;
                            improved = true;
                            break;
                        }
                        else
                        {
                            currentPhis[r][ui] = a;
                            currentPhis[r][uj] = b;
                        }
                    }
                }
            }
        }

        return currentPhis;
    }

    private bool HasIdenticalLayers(List<Dictionary<int, int>> phis)
    {
        for (int i = 0; i < phis.Count; i++)
        {
            for (int j = i + 1; j < phis.Count; j++)
            {
                if (AreMappingsIdenticalInG2(phis[i], phis[j]))
                    return true;
            }
        }
        return false;
    }

    private List<Dictionary<int, int>> MergePlaceholders(List<Dictionary<int, int>> phis)
    {
        var allPlaceholders = new HashSet<int>();
        foreach (var phi in phis)
        {
            foreach (var v in phi.Values)
            {
                if (!g2.ContainsVertex(v))
                    allPlaceholders.Add(v);
            }
        }

        var neighborMap = new Dictionary<int, HashSet<(int, int, bool)>>();

        foreach (var p in allPlaceholders)
        {
            var neighbors = new HashSet<(int, int, bool)>();

            foreach (var phi in phis)
            {
                foreach (var kv in phi)
                {
                    if (kv.Value == p)
                    {
                        int u = kv.Key;
                        foreach (var edge in g1.GetAllEdges())
                        {
                            if (edge.From == u && phi.TryGetValue(edge.To, out int target))
                                neighbors.Add((target, 1, true));
                            if (edge.To == u && phi.TryGetValue(edge.From, out int source))
                                neighbors.Add((source, 2, true));
                        }
                    }
                }
            }

            neighborMap[p] = neighbors;
        }

        var mergeMap = new Dictionary<int, int>();
        var placeholderList = allPlaceholders.ToList();

        for (int i = 0; i < placeholderList.Count; i++)
        {
            if (mergeMap.ContainsKey(placeholderList[i])) continue;

            for (int j = i + 1; j < placeholderList.Count; j++)
            {
                if (mergeMap.ContainsKey(placeholderList[j])) continue;

                if (neighborMap[placeholderList[i]].SetEquals(neighborMap[placeholderList[j]]))
                {
                    mergeMap[placeholderList[j]] = placeholderList[i];
                }
            }
        }

        var newPhis = new List<Dictionary<int, int>>();
        foreach (var phi in phis)
        {
            var newPhi = new Dictionary<int, int>();
            foreach (var kv in phi)
            {
                int target = kv.Value;
                while (mergeMap.ContainsKey(target))
                    target = mergeMap[target];
                newPhi[kv.Key] = target;
            }
            newPhis.Add(newPhi);
        }

        return newPhis;
    }
    private int ComputeGlobalCost(List<Dictionary<int, int>> phis)
    {
        var addedVertices = new HashSet<int>();
        var addedDirectedArcs = new HashSet<(int, int)>(); 
        var addedUndirectedEdges = new HashSet<(int, int)>();

        foreach (var phi in phis)
        {
            foreach (var v in phi.Values)
                if (!g2.ContainsVertex(v))
                    addedVertices.Add(v);

            foreach (var edge in g1.GetAllEdges())
            {
                int u = phi[edge.From];
                int v = phi[edge.To];

                if (edge.IsDirected)
                {
                    if (!g2.HasEdge(u, v))
                        addedDirectedArcs.Add((u, v));
                }
                else
                {
                    if (g2.IsDirected)
                    {
                        if (!g2.HasEdge(u, v)) addedDirectedArcs.Add((u, v));
                        if (!g2.HasEdge(v, u)) addedDirectedArcs.Add((v, u));
                    }
                    else
                    {
                        var key = u < v ? (u, v) : (v, u);
                        if (!g2.HasEdge(u, v) && !g2.HasEdge(v, u))
                            addedUndirectedEdges.Add(key);
                    }
                }
            }
        }

        int edgeCost = 0;
        edgeCost += addedDirectedArcs.Count * 1;
        edgeCost += addedUndirectedEdges.Count * 2;

        return addedVertices.Count + edgeCost;
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

            if (edge.IsDirected)
            {
                if (!g2.HasEdge(u, v))
                    addedE.Add(new Edge<int>(u, v, 1.0, true));
            }
            else
            {
                if (g2.IsDirected)
                {
                    if (!g2.HasEdge(u, v)) addedE.Add(new Edge<int>(u, v, 1.0, true));
                    if (!g2.HasEdge(v, u)) addedE.Add(new Edge<int>(v, u, 1.0, true));
                }
                else
                {
                    if (!g2.HasEdge(u, v) && !g2.HasEdge(v, u))
                    {
                        // dodaj jako nie-skierowana (typu Edge z IsDirected=false)
                        addedE.Add(new Edge<int>(u, v, 1.0, false));
                    }
                }
            }
        }

        return new Mapping(phi, addedV, addedE);
    }

    private bool AllMappingsEqual(List<Dictionary<int, int>> phis1, List<Dictionary<int, int>> phis2)
    {
        if (phis1.Count != phis2.Count) return false;

        for (int i = 0; i < phis1.Count; i++)
        {
            if (!MappingsEqual(phis1[i], phis2[i]))
                return false;
        }

        return true;
    }

    private bool MappingsEqual(Dictionary<int, int> phi1, Dictionary<int, int> phi2)
    {
        if (phi1.Count != phi2.Count) return false;

        foreach (var kv in phi1)
        {
            if (!phi2.TryGetValue(kv.Key, out int v) || v != kv.Value)
                return false;
        }

        return true;
    }
}