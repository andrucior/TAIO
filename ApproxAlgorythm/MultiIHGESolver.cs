using GraphLibrary.Model;
using System;
using System.Collections.Generic;
using System.Linq;

public class MultiIHGESolver
{
    private readonly Graph<int> g1;
    private readonly Graph<int> g2;
    private readonly bool isDirected;
    private int nextPlaceholderId;
    private int maxIterations = 500;
    private readonly Random rnd;
    private double gamma = 10.0; // Kara za identyczne odwzorowania

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
        if (k <= 0) throw new ArgumentException("k must be positive");

        var v1List = g1.Vertices.ToList();
        int n1 = v1List.Count;

        // Kandydaci bazowi: wierzchołki G2 + placeholdery dla każdej warstwy
        var baseCandidates = new List<int>();
        baseCandidates.AddRange(g2.Vertices);
        for (int i = 0; i < n1 * k; i++)
            baseCandidates.Add(nextPlaceholderId + i);

        List<Dictionary<int, int>> bestPhis = null;
        int bestCost = int.MaxValue;

        int restarts = Math.Min(15, 5 + k * 2); // Więcej restartów dla większych k
        for (int run = 0; run < restarts; run++)
        {
            var phis = InitializePhis(k, v1List, baseCandidates);

            bool changed = true;
            int stableIterations = 0;
            int iter = 0;

            while (stableIterations < 5 && iter < maxIterations)
            {
                iter++;
                changed = false;

                var newPhis = new List<Dictionary<int, int>>();

                // Aktualizuj każdą warstwę
                for (int r = 0; r < k; r++)
                {
                    var costMatrix = BuildCostMatrixForLayer(r, v1List, baseCandidates, phis);
                    var hungarian = new HungarianAlgorithm(costMatrix);
                    var assignment = hungarian.Solve();

                    var newPhi = new Dictionary<int, int>();
                    for (int i = 0; i < n1; i++)
                        newPhi[v1List[i]] = baseCandidates[assignment[i]];

                    // Forced difference - wymuszenie różnorodności
                    newPhi = EnforceDifference(newPhi, newPhis, v1List, baseCandidates);

                    newPhis.Add(newPhi);
                }

                // Lokalna poprawa dla wszystkich warstw jednocześnie
                newPhis = LocalImproveAll(newPhis, v1List, baseCandidates);

                if (!AllMappingsEqual(phis, newPhis))
                {
                    changed = true;
                    stableIterations = 0;
                }
                else
                {
                    stableIterations++;
                }

                phis = newPhis;
            }

            // Scalanie placeholderów między warstwami
            phis = MergePlaceholders(phis);

            int cost = ComputeGlobalCost(phis);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestPhis = phis.Select(p => new Dictionary<int, int>(p)).ToList();
            }

            if (bestCost == 0) break;
        }

        var mappings = bestPhis.Select(phi => BuildMapping(phi)).ToList();
        int finalCost = ComputeGlobalCost(bestPhis);
        return new SearchResult(mappings, finalCost);
    }

    private List<Dictionary<int, int>> InitializePhis(int k, List<int> v1List, List<int> candidates)
    {
        var phis = new List<Dictionary<int, int>>();
        var shuffled = candidates.OrderBy(x => rnd.Next()).ToList();

        for (int r = 0; r < k; r++)
        {
            var phi = new Dictionary<int, int>();
            int offset = r * v1List.Count;
            for (int i = 0; i < v1List.Count; i++)
            {
                phi[v1List[i]] = shuffled[(offset + i) % shuffled.Count];
            }
            phis.Add(phi);
        }

        return phis;
    }

    private double[,] BuildCostMatrixForLayer(int layerIdx, List<int> v1List,
        List<int> candidates, List<Dictionary<int, int>> phis)
    {
        int n1 = v1List.Count;
        int nC = candidates.Count;
        var cost = new double[n1, nC];
        var currentPhi = phis[layerIdx];

        for (int i = 0; i < n1; i++)
        {
            int u = v1List[i];

            for (int j = 0; j < nC; j++)
            {
                int c = candidates[j];
                double w = 0;

                // Koszt placeholdera
                bool isPlaceholder = !g2.ContainsVertex(c);
                if (isPlaceholder) w += 1.0;

                // Koszt brakujących krawędzi (heurystyczny)
                foreach (var t in v1List)
                {
                    if (t == u) continue;

                    if (currentPhi.TryGetValue(t, out int c_t))
                    {
                        if (g1.HasEdge(u, t) && !g2.HasEdge(c, c_t)) w += 1.0;
                        if (g1.HasEdge(t, u) && !g2.HasEdge(c_t, c)) w += 1.0;
                    }
                }

                // Kara różnorodności - penalizuj identyczne przypisania z wcześniejszych warstw
                double diversityPenalty = 0;
                for (int s = 0; s < layerIdx; s++)
                {
                    if (phis[s].TryGetValue(u, out int prev) && prev == c && !isPlaceholder)
                    {
                        diversityPenalty += gamma;
                    }
                }
                w += diversityPenalty;

                cost[i, j] = w;
            }
        }

        return cost;
    }

    private Dictionary<int, int> EnforceDifference(Dictionary<int, int> phi,
        List<Dictionary<int, int>> previousPhis, List<int> v1List, List<int> candidates)
    {
        // Sprawdź, czy obecne phi jest identyczne z którąś z poprzednich warstw
        foreach (var prevPhi in previousPhis)
        {
            if (AreMappingsIdenticalInG2(phi, prevPhi))
            {
                // Znajdź najtańszą zmianę, która sprawi, że będą różne
                return FindCheapestDifference(phi, prevPhi, v1List, candidates);
            }
        }
        return phi;
    }

    private bool AreMappingsIdenticalInG2(Dictionary<int, int> phi1, Dictionary<int, int> phi2)
    {
        var set1 = new HashSet<int>(phi1.Values.Where(v => g2.ContainsVertex(v)));
        var set2 = new HashSet<int>(phi2.Values.Where(v => g2.ContainsVertex(v)));

        if (set1.Count != set2.Count) return false;
        return set1.SetEquals(set2);
    }

    private Dictionary<int, int> FindCheapestDifference(Dictionary<int, int> phi,
        Dictionary<int, int> conflictPhi, List<int> v1List, List<int> candidates)
    {
        var bestPhi = new Dictionary<int, int>(phi);
        int bestCost = ComputeTrueCost(new List<Dictionary<int, int>> { phi });

        // Próbuj zmienić po jednym przypisaniu
        foreach (var u in v1List)
        {
            int original = phi[u];

            foreach (var c in candidates)
            {
                if (c == original) continue;

                phi[u] = c;

                if (!AreMappingsIdenticalInG2(phi, conflictPhi))
                {
                    int newCost = ComputeTrueCost(new List<Dictionary<int, int>> { phi });
                    if (newCost < bestCost)
                    {
                        bestCost = newCost;
                        bestPhi = new Dictionary<int, int>(phi);
                    }
                }
            }

            phi[u] = original;
        }

        return bestPhi;
    }

    private List<Dictionary<int, int>> LocalImproveAll(List<Dictionary<int, int>> phis,
        List<int> v1List, List<int> candidates)
    {
        var currentPhis = phis.Select(p => new Dictionary<int, int>(p)).ToList();
        int bestCost = ComputeGlobalCost(currentPhis);
        bool improved = true;
        int maxLocalIters = 50;
        int iter = 0;

        while (improved && iter < maxLocalIters)
        {
            improved = false;
            iter++;

            // Próbuj zmiany w każdej warstwie
            for (int r = 0; r < currentPhis.Count; r++)
            {
                var used = new HashSet<int>();
                foreach (var p in currentPhis)
                    foreach (var v in p.Values)
                        used.Add(v);

                var unused = candidates.Where(c => !used.Contains(c)).ToList();

                // Single reassignments
                foreach (var u in v1List)
                {
                    int old = currentPhis[r][u];

                    foreach (var cand in unused)
                    {
                        currentPhis[r][u] = cand;
                        int c = ComputeGlobalCost(currentPhis);

                        if (c < bestCost && !HasIdenticalLayers(currentPhis))
                        {
                            bestCost = c;
                            improved = true;
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

                // Pairwise swaps w ramach warstwy
                for (int i = 0; i < v1List.Count && !improved; i++)
                {
                    for (int j = i + 1; j < v1List.Count && !improved; j++)
                    {
                        int ui = v1List[i], uj = v1List[j];
                        int a = currentPhis[r][ui], b = currentPhis[r][uj];

                        currentPhis[r][ui] = b;
                        currentPhis[r][uj] = a;

                        int c = ComputeGlobalCost(currentPhis);

                        if (c < bestCost && !HasIdenticalLayers(currentPhis))
                        {
                            bestCost = c;
                            improved = true;
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
        // Znajdź wszystkie placeholdery
        var allPlaceholders = new HashSet<int>();
        foreach (var phi in phis)
        {
            foreach (var v in phi.Values)
            {
                if (!g2.ContainsVertex(v))
                    allPlaceholders.Add(v);
            }
        }

        // Buduj mapę: placeholder -> sąsiedzi (po dodaniu wszystkich krawędzi)
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
                        // Sprawdź wszystkie krawędzie z u
                        foreach (var edge in g1.GetAllEdges())
                        {
                            if (edge.From == u && phi.TryGetValue(edge.To, out int target))
                                neighbors.Add((target, 1, true)); // out edge
                            if (edge.To == u && phi.TryGetValue(edge.From, out int source))
                                neighbors.Add((source, 2, true)); // in edge
                        }
                    }
                }
            }

            neighborMap[p] = neighbors;
        }

        // Scalaj placeholdery z identycznymi sąsiadami
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

        // Zastosuj scalenie
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

    private int ComputeTrueCost(List<Dictionary<int, int>> phis)
    {
        return ComputeGlobalCost(phis);
    }

    private int ComputeGlobalCost(List<Dictionary<int, int>> phis)
    {
        // Globalny koszt = unikalne dodane wierzchołki + unikalne dodane krawędzie
        var addedVertices = new HashSet<int>();
        var addedEdges = new HashSet<(int, int)>();

        foreach (var phi in phis)
        {
            // Dodane wierzchołki
            foreach (var v in phi.Values)
            {
                if (!g2.ContainsVertex(v))
                    addedVertices.Add(v);
            }

            // Dodane krawędzie
            foreach (var edge in g1.GetAllEdges())
            {
                int u = phi[edge.From];
                int v = phi[edge.To];

                if (!g2.HasEdge(u, v))
                    addedEdges.Add((u, v));
            }
        }

        return addedVertices.Count + addedEdges.Count;
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