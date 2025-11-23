using System;
using System.Collections.Generic;
using System.Linq;

public class HungarianAlgorithm
{
    private readonly double[,] original;
    private readonly int rows, cols, n;
    private readonly double[,] cost;
    private readonly int[] rowCover, colCover;
    private readonly int[,] mask;

    public HungarianAlgorithm(double[,] costMatrix)
    {
        original = costMatrix;
        rows = costMatrix.GetLength(0);
        cols = costMatrix.GetLength(1);
        n = Math.Max(rows, cols);

        cost = new double[n, n];

        // Copy into square matrix
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                cost[i, j] = (i < rows && j < cols) ? costMatrix[i, j] : 0;

        rowCover = new int[n];
        colCover = new int[n];
        mask = new int[n, n];
    }

    public int[] Solve()
    {
        Step1();
        Step2();
        int step = 3;

        while (true)
        {
            switch (step)
            {
                case 3: step = Step3(); break;
                case 4: step = Step4(); break;
                case 5: step = Step5(); break;
                case 6: step = Step6(); break;
                case 7:
                    return Extract();
            }
        }
    }

    // --- STEP 1: Subtract row minima ---
    private void Step1()
    {
        for (int i = 0; i < n; i++)
        {
            double min = double.PositiveInfinity;
            for (int j = 0; j < n; j++)
                if (cost[i, j] < min)
                    min = cost[i, j];

            for (int j = 0; j < n; j++)
                cost[i, j] -= min;
        }
    }

    // --- STEP 2: Subtract column minima ---
    private void Step2()
    {
        for (int j = 0; j < n; j++)
        {
            double min = double.PositiveInfinity;
            for (int i = 0; i < n; i++)
                if (cost[i, j] < min)
                    min = cost[i, j];

            for (int i = 0; i < n; i++)
                cost[i, j] -= min;
        }
    }

    // --- STEP 3: Star independent zeros ---
    private int Step3()
    {
        Array.Clear(rowCover, 0, n);
        Array.Clear(colCover, 0, n);

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (Math.Abs(cost[i, j]) < 1e-12 &&
                    rowCover[i] == 0 && colCover[j] == 0)
                {
                    mask[i, j] = 1;  // star
                    rowCover[i] = 1;
                    colCover[j] = 1;
                }

        Array.Clear(rowCover, 0, n);
        Array.Clear(colCover, 0, n);

        return 4;
    }

    // --- STEP 4: Cover columns with starred zeros ---
    private int Step4()
    {
        int coverCount = 0;

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (mask[i, j] == 1)
                    colCover[j] = 1;

        for (int j = 0; j < n; j++)
            if (colCover[j] == 1)
                coverCount++;

        return (coverCount >= n) ? 7 : 5;
    }

    // --- STEP 5: Prime zeros, find augmenting path ---
    private int Step5()
    {
        int[] zero = FindUncoveredZero();

        while (zero[0] == -1)
        {
            return 6; // go adjust matrix
        }

        int row = zero[0];
        int col = zero[1];

        mask[row, col] = 2; // prime

        int starCol = FindStarInRow(row);

        if (starCol != -1)
        {
            rowCover[row] = 1;
            colCover[starCol] = 0;
            return 5;
        }

        Augment(row, col);
        Array.Clear(rowCover, 0, n);
        Array.Clear(colCover, 0, n);

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                if (mask[i, j] == 2)
                    mask[i, j] = 0;

        return 4;
    }

    // --- STEP 6: Adjust matrix ---
    private int Step6()
    {
        double min = double.PositiveInfinity;

        for (int i = 0; i < n; i++)
            if (rowCover[i] == 0)
                for (int j = 0; j < n; j++)
                    if (colCover[j] == 0 && cost[i, j] < min)
                        min = cost[i, j];

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                if (rowCover[i] == 1) cost[i, j] += min;
                if (colCover[j] == 0) cost[i, j] -= min;
            }

        return 5;
    }

    // --- Utility methods ---

    private int[] FindUncoveredZero()
    {
        for (int i = 0; i < n; i++)
            if (rowCover[i] == 0)
                for (int j = 0; j < n; j++)
                    if (colCover[j] == 0 && Math.Abs(cost[i, j]) < 1e-12)
                        return new int[] { i, j };
        return new int[] { -1, -1 };
    }

    private int FindStarInRow(int row)
    {
        for (int j = 0; j < n; j++)
            if (mask[row, j] == 1) return j;
        return -1;
    }

    private int FindStarInCol(int col)
    {
        for (int i = 0; i < n; i++)
            if (mask[i, col] == 1) return i;
        return -1;
    }

    private int FindPrimeInRow(int row)
    {
        for (int j = 0; j < n; j++)
            if (mask[row, j] == 2) return j;
        return -1;
    }

    // Build augmenting path
    private void Augment(int row, int col)
    {
        List<(int r, int c)> path = new();
        path.Add((row, col));

        while (true)
        {
            int r = FindStarInCol(path[^1].c);
            if (r == -1) break;

            path.Add((r, path[^1].c));

            int c = FindPrimeInRow(r);
            path.Add((r, c));
        }

        foreach (var (r, c) in path)
            mask[r, c] = mask[r, c] == 1 ? 0 : 1;
    }

    // Extract assignment
    private int[] Extract()
    {
        int[] result = new int[rows];

        for (int i = 0; i < rows; i++)
        {
            result[i] = -1;
            for (int j = 0; j < cols; j++)
                if (mask[i, j] == 1)
                    result[i] = j;
        }

        return result;
    }
}
