// StS2Assistant - Draw Probability Calculator
// Implements hypergeometric distribution for card draw probabilities

using System;

namespace StS2Assistant.Logic;

/// <summary>
/// Calculates card draw probabilities using hypergeometric distribution.
/// Formula: P(X=k) = C(K,k) × C(N-K, n-k) / C(N, n)
/// Where:
///   N = population size (deck size)
///   K = success states in population (copies of desired card)
///   n = number of draws
///   k = number of desired successes
/// </summary>
public class DrawProbability
{
    // Cache for combination calculations to avoid recomputing
    private readonly Dictionary<(int n, int k), double> _combinationCache = new();

    /// <summary>
    /// Calculate probability of drawing at least one copy of a card
    /// </summary>
    /// <param name="populationSize">Total cards in deck</param>
    /// <param name="successStates">Number of copies of desired card in deck</param>
    /// <param name="draws">Number of cards to draw</param>
    /// <param name="desiredSuccesses">Minimum number of copies wanted (default 1)</param>
    /// <returns>Probability as value between 0.0 and 1.0</returns>
    public float CalculateProbability(int populationSize, int successStates, int draws, int desiredSuccesses = 1)
    {
        if (populationSize <= 0 || successStates <= 0 || draws <= 0)
            return 0f;
        
        if (successStates > populationSize)
            successStates = populationSize;
        
        if (draws > populationSize)
            draws = populationSize;
        
        // Calculate probability of drawing AT LEAST desiredSuccesses copies
        // P(X >= k) = 1 - P(X < k) = 1 - sum(P(X=i) for i in 0..k-1)
        double probability = 0;
        
        for (int k = desiredSuccesses; k <= Math.Min(successStates, draws); k++)
        {
            probability += HypergeometricProbability(populationSize, successStates, draws, k);
        }
        
        return (float)Math.Min(1.0, Math.Max(0.0, probability));
    }

    /// <summary>
    /// Calculate exact hypergeometric probability P(X=k)
    /// </summary>
    private double HypergeometricProbability(int N, int K, int n, int k)
    {
        // P(X=k) = C(K,k) × C(N-K, n-k) / C(N, n)
        
        if (k > K || k > n)
            return 0;
        
        if (n - k > N - K)
            return 0;
        
        try
        {
            double combinationsK = Combination(K, k);
            double combinationsRest = Combination(N - K, n - k);
            double combinationsTotal = Combination(N, n);
            
            if (combinationsTotal == 0)
                return 0;
            
            return (combinationsK * combinationsRest) / combinationsTotal;
        }
        catch (OverflowException)
        {
            // For large numbers, use logarithmic calculation
            return LogHypergeometricProbability(N, K, n, k);
        }
    }

    /// <summary>
    /// Calculate combinations C(n,k) = n! / (k! × (n-k)!)
    /// Uses caching to avoid recomputation
    /// </summary>
    private double Combination(int n, int k)
    {
        if (k < 0 || k > n)
            return 0;
        
        if (k == 0 || k == n)
            return 1;
        
        // Use symmetry: C(n,k) = C(n, n-k)
        if (k > n / 2)
            k = n - k;
        
        var cacheKey = (n, k);
        if (_combinationCache.TryGetValue(cacheKey, out var cached))
            return cached;
        
        // Calculate iteratively to avoid overflow
        double result = 1;
        for (int i = 0; i < k; i++)
        {
            result = result * (n - i) / (i + 1);
        }
        
        _combinationCache[cacheKey] = result;
        return result;
    }

    /// <summary>
    /// Fallback for large numbers using logarithms
    /// </summary>
    private double LogHypergeometricProbability(int N, int K, int n, int k)
    {
        // log(P) = log(C(K,k)) + log(C(N-K, n-k)) - log(C(N,n))
        
        double logCombK = LogCombination(K, k);
        double logCombRest = LogCombination(N - K, n - k);
        double logCombTotal = LogCombination(N, n);
        
        double logProb = logCombK + logCombRest - logCombTotal;
        
        return Math.Exp(logProb);
    }

    /// <summary>
    /// Calculate log of combination using log gamma function
    /// log(C(n,k)) = ln(n!) - ln(k!) - ln((n-k)!)
    /// </summary>
    private double LogCombination(int n, int k)
    {
        if (k < 0 || k > n)
            return double.NegativeInfinity;
        
        if (k == 0 || k == n)
            return 0;
        
        return LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k);
    }

    /// <summary>
    /// Calculate natural log of factorial using Stirling's approximation for large n
    /// </summary>
    private double LogFactorial(int n)
    {
        if (n <= 1)
            return 0;
        
        // For small n, calculate directly
        if (n < 20)
        {
            double result = 0;
            for (int i = 2; i <= n; i++)
            {
                result += Math.Log(i);
            }
            return result;
        }
        
        // Stirling's approximation for large n
        // ln(n!) ≈ n*ln(n) - n + 0.5*ln(2πn) + 1/(12n)
        double nDouble = n;
        return nDouble * Math.Log(nDouble) - nDouble + 
               0.5 * Math.Log(2 * Math.PI * nDouble) + 
               1.0 / (12 * nDouble);
    }

    /// <summary>
    /// Clear the combination cache
    /// </summary>
    public void ClearCache()
    {
        _combinationCache.Clear();
    }

    /// <summary>
    /// Get formatted probability string for UI display
    /// </summary>
    public string GetProbabilityString(float probability)
    {
        return $"{probability * 100:F1}%";
    }

    /// <summary>
    /// Calculate expected number of copies drawn
    /// E[X] = n × (K/N)
    /// </summary>
    public float CalculateExpectedCopies(int populationSize, int successStates, int draws)
    {
        if (populationSize <= 0)
            return 0f;
        
        return (float)draws * successStates / populationSize;
    }
}
