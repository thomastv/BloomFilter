using System.Runtime.CompilerServices;

namespace Unique.BloomFilter.Core;

/// <summary>
/// Provides mathematical utilities and calculations for Bloom filter optimization.
/// This static class contains methods for computing optimal parameters, hash functions,
/// and performance characteristics of Bloom filters.
/// </summary>
public static class BloomFilterMath
{
    /// <summary>
    /// Natural logarithm of 2 (ln(2)) - used frequently in Bloom filter calculations.
    /// Precalculated constant for performance optimization.
    /// </summary>
    private const double Ln2 = 0.6931471805599453;

    /// <summary>
    /// Square of ln(2) - used in bit array size calculations.
    /// Precalculated constant: (ln(2))²
    /// </summary>
    private const double Ln2Squared = 0.4804530139182014;

    /// <summary>
    /// Maximum stack allocation size in bytes (1KB).
    /// Filters requiring ≤ 1KB will use stackalloc, larger filters use heap allocation.
    /// </summary>
    public const int MaxStackAllocSize = 1024;

    /// <summary>
    /// Calculates the optimal bit array size for a Bloom filter given the expected number
    /// of elements and desired false positive rate.
    /// </summary>
    /// <param name="expectedElements">Expected number of elements to be added (n)</param>
    /// <param name="falsePositiveRate">Desired false positive rate (ε), must be between 0 and 1</param>
    /// <returns>Optimal bit array size (m) in bits</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when expectedElements ≤ 0 or falsePositiveRate is not between 0 and 1
    /// </exception>
    /// <remarks>
    /// Uses the formula: m = -n * ln(ε) / (ln(2))²
    /// where n = expectedElements, ε = falsePositiveRate, m = bit array size
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateOptimalBitArraySize(int expectedElements, double falsePositiveRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedElements);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(falsePositiveRate, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(falsePositiveRate, 1.0);

        // m = -n * ln(ε) / (ln(2))²
        var bitArraySize = (int)Math.Ceiling(-expectedElements * Math.Log(falsePositiveRate) / Ln2Squared);
        
        // Ensure minimum size and round up to next byte boundary for efficient memory access
        return Math.Max(8, (bitArraySize + 7) & ~7);
    }

    /// <summary>
    /// Calculates the optimal number of hash functions for a Bloom filter given the
    /// bit array size and expected number of elements.
    /// </summary>
    /// <param name="bitArraySize">Size of the bit array (m) in bits</param>
    /// <param name="expectedElements">Expected number of elements to be added (n)</param>
    /// <returns>Optimal number of hash functions (k)</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when bitArraySize ≤ 0 or expectedElements ≤ 0
    /// </exception>
    /// <remarks>
    /// Uses the formula: k = (m/n) * ln(2)
    /// where m = bitArraySize, n = expectedElements, k = number of hash functions
    /// Result is clamped between 1 and 64 for practical implementation limits
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateOptimalHashFunctionCount(int bitArraySize, int expectedElements)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bitArraySize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedElements);

        // k = (m/n) * ln(2)
        var optimalK = (double)bitArraySize / expectedElements * Ln2;
        
        // Clamp to reasonable bounds: minimum 1, maximum 64 hash functions
        return Math.Clamp((int)Math.Round(optimalK), 1, 64);
    }

    /// <summary>
    /// Calculates the actual false positive probability for a Bloom filter with given parameters.
    /// </summary>
    /// <param name="bitArraySize">Size of the bit array (m) in bits</param>
    /// <param name="hashFunctionCount">Number of hash functions (k)</param>
    /// <param name="insertedElements">Number of elements actually inserted (n)</param>
    /// <returns>Actual false positive probability (ε)</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any parameter is ≤ 0
    /// </exception>
    /// <remarks>
    /// Uses the formula: ε ≈ (1 - e^(-kn/m))^k
    /// where m = bitArraySize, k = hashFunctionCount, n = insertedElements
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateActualFalsePositiveRate(int bitArraySize, int hashFunctionCount, int insertedElements)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bitArraySize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hashFunctionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(insertedElements);

        // ε ≈ (1 - e^(-kn/m))^k
        var exponent = -(double)hashFunctionCount * insertedElements / bitArraySize;
        var probability = Math.Pow(1.0 - Math.Exp(exponent), hashFunctionCount);
        
        return Math.Clamp(probability, 0.0, 1.0);
    }

    /// <summary>
    /// Generates hash values using double hashing technique from System.HashCode.
    /// This method creates two independent hash values that can be combined to generate
    /// multiple hash functions with good distribution properties.
    /// </summary>
    /// <typeparam name="T">Type of the item to hash</typeparam>
    /// <param name="item">Item to generate hash values for</param>
    /// <returns>Tuple containing (hash1, hash2) as unsigned 32-bit values</returns>
    /// <remarks>
    /// Uses System.HashCode with different seeds to generate two independent hash values.
    /// These can be combined using the formula: hash_i = (hash1 + i * hash2) % m
    /// to generate k different hash functions with minimal computational overhead.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (uint Hash1, uint Hash2) GenerateHashValues<T>(T item) where T : notnull
    {
        // Generate first hash with default seed
        var hashCode1 = new HashCode();
        hashCode1.Add(item);
        var hash1 = (uint)hashCode1.ToHashCode();

        // Generate second hash with different seed for independence
        var hashCode2 = new HashCode();
        hashCode2.Add(item);
        hashCode2.Add(0x9e3779b9); // Golden ratio bits as additional entropy
        var hash2 = (uint)hashCode2.ToHashCode();

        return (hash1, hash2);
    }

    /// <summary>
    /// Computes the i-th hash function value using double hashing technique.
    /// </summary>
    /// <param name="hash1">First hash value from GenerateHashValues</param>
    /// <param name="hash2">Second hash value from GenerateHashValues</param>
    /// <param name="hashIndex">Index of the hash function (i), zero-based</param>
    /// <param name="bitArraySize">Size of the bit array to map into</param>
    /// <returns>Bit index in the range [0, bitArraySize)</returns>
    /// <remarks>
    /// Uses the formula: hash_i = (hash1 + i * hash2) % bitArraySize
    /// This provides good distribution properties while avoiding the overhead
    /// of computing independent hash functions for each index.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ComputeHashFunction(uint hash1, uint hash2, int hashIndex, int bitArraySize)
    {
        // Double hashing: hash_i = (hash1 + i * hash2) % m
        var combinedHash = (hash1 + (uint)hashIndex * hash2) % (uint)bitArraySize;
        return (int)combinedHash;
    }

    /// <summary>
    /// Determines whether a Bloom filter should use stack allocation based on its memory requirements.
    /// </summary>
    /// <param name="bitArraySize">Size of the bit array in bits</param>
    /// <returns>True if the filter should use stackalloc, false for heap allocation</returns>
    /// <remarks>
    /// Filters requiring ≤ 1KB (MaxStackAllocSize) use stack allocation for better performance.
    /// Larger filters use heap allocation to avoid stack overflow risks.
    /// The threshold is conservative to maintain stack safety in most scenarios.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldUseStackAllocation(int bitArraySize)
    {
        var bytesRequired = (bitArraySize + 7) / 8; // Round up to nearest byte
        return bytesRequired <= MaxStackAllocSize;
    }

    /// <summary>
    /// Calculates the memory usage in bytes for a Bloom filter with the given bit array size.
    /// </summary>
    /// <param name="bitArraySize">Size of the bit array in bits</param>
    /// <returns>Memory usage in bytes</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateMemoryUsage(int bitArraySize)
    {
        return (bitArraySize + 7) / 8; // Round up to nearest byte
    }

    /// <summary>
    /// Computes optimal Bloom filter parameters for given capacity and error rate.
    /// This is a convenience method that calculates both bit array size and hash function count.
    /// </summary>
    /// <param name="capacity">Expected number of elements</param>
    /// <param name="falsePositiveRate">Desired false positive rate</param>
    /// <returns>Tuple containing (bitArraySize, hashFunctionCount, memoryUsage)</returns>
    public static (int BitArraySize, int HashFunctionCount, int MemoryUsage) ComputeOptimalParameters(
        int capacity, 
        double falsePositiveRate)
    {
        var bitArraySize = CalculateOptimalBitArraySize(capacity, falsePositiveRate);
        var hashFunctionCount = CalculateOptimalHashFunctionCount(bitArraySize, capacity);
        var memoryUsage = CalculateMemoryUsage(bitArraySize);

        return (bitArraySize, hashFunctionCount, memoryUsage);
    }
}