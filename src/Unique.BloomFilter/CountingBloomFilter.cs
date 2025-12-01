using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unique.BloomFilter.Core;

namespace Unique.BloomFilter;

/// <summary>
/// A Counting Bloom filter implementation that supports removal operations.
/// Uses byte counters for each hash position, allowing items to be removed
/// while maintaining the probabilistic guarantees of the filter.
/// </summary>
/// <remarks>
/// <para>
/// Counting Bloom filters extend basic Bloom filters by replacing each bit with a small counter.
/// This allows for removal operations while maintaining the "no false negatives" guarantee.
/// However, false positives are still possible, and removal can introduce false negatives
/// if counters overflow or underflow.
/// </para>
/// <para>
/// This implementation uses byte counters (0-255) with overflow detection.
/// When a counter would overflow, the operation is ignored to prevent data corruption.
/// Memory usage is 8x higher than basic Bloom filters due to the counter array.
/// </para>
/// </remarks>
public readonly ref struct CountingBloomFilter
{
    /// <summary>
    /// Array of byte counters, one per hash position. Each counter tracks how many
    /// hash functions have mapped to that position across all added items.
    /// </summary>
    private readonly Span<byte> _counters;
    
    /// <summary>
    /// Number of hash functions used by this filter. Same calculation as basic Bloom filter.
    /// </summary>
    private readonly int _hashFunctionCount;
    
    /// <summary>
    /// Size of the counter array. Corresponds to the bit array size in a basic Bloom filter.
    /// </summary>
    private readonly int _arraySize;

    /// <summary>
    /// Maximum value for a counter before overflow protection kicks in.
    /// Set to 255 since we use byte counters.
    /// </summary>
    private const byte MaxCounterValue = byte.MaxValue;

    /// <summary>
    /// Initializes a new Counting Bloom filter with the specified counter array and parameters.
    /// </summary>
    /// <param name="counters">Pre-allocated counter array as a Span&lt;byte&gt;</param>
    /// <param name="hashFunctionCount">Number of hash functions to use</param>
    /// <param name="arraySize">Size of the counter array</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when parameters are invalid or counters array is insufficient
    /// </exception>
    private CountingBloomFilter(Span<byte> counters, int hashFunctionCount, int arraySize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hashFunctionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arraySize);
        
        if (counters.Length < arraySize)
        {
            throw new ArgumentException($"Counter array too small. Required: {arraySize}, provided: {counters.Length}", nameof(counters));
        }

        _counters = counters;
        _hashFunctionCount = hashFunctionCount;
        _arraySize = arraySize;
    }

    /// <summary>
    /// Gets the number of hash functions used by this filter.
    /// </summary>
    public int HashFunctionCount => _hashFunctionCount;

    /// <summary>
    /// Gets the size of the counter array.
    /// </summary>
    public int ArraySize => _arraySize;

    /// <summary>
    /// Gets the memory usage of this filter in bytes (equal to array size since using byte counters).
    /// </summary>
    public int MemoryUsage => _counters.Length;

    /// <summary>
    /// Creates a new Counting Bloom filter optimized for the specified capacity and false positive rate.
    /// </summary>
    /// <param name="capacity">Expected number of elements to be added</param>
    /// <param name="falsePositiveRate">Desired false positive rate (between 0.0 and 1.0)</param>
    /// <param name="buffer">Optional pre-allocated buffer for counters</param>
    /// <returns>A new CountingBloomFilter instance</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when capacity ≤ 0 or falsePositiveRate is not between 0.0 and 1.0
    /// </exception>
    /// <example>
    /// <code>
    /// // Create a counting filter for 1000 items with 1% false positive rate
    /// var filter = CountingBloomFilter.Create(1000, 0.01);
    /// 
    /// // Add and remove items
    /// filter.Add("hello");
    /// filter.Add("world");
    /// filter.Remove("hello");  // Now only "world" remains
    /// 
    /// bool found = filter.MightContain("world"); // true
    /// bool removed = filter.MightContain("hello"); // false
    /// </code>
    /// </example>
    public static CountingBloomFilter Create(int capacity, double falsePositiveRate, Span<byte> buffer = default)
    {
        var (arraySize, hashFunctionCount, _) = BloomFilterMath.ComputeOptimalParameters(capacity, falsePositiveRate);
        
        // Note: For counting filters, we use the bit array size as counter array size
        // Memory usage is higher (1 byte per position vs 1 bit per position)
        Span<byte> counters;
        if (buffer.Length >= arraySize)
        {
            counters = buffer[..arraySize];
            counters.Clear();
        }
        else
        {
            counters = new byte[arraySize];
        }

        return new CountingBloomFilter(counters, hashFunctionCount, arraySize);
    }

    /// <summary>
    /// Creates a new Counting Bloom filter with manually specified parameters.
    /// </summary>
    /// <param name="arraySize">Size of the counter array</param>
    /// <param name="hashFunctionCount">Number of hash functions to use</param>
    /// <param name="buffer">Optional pre-allocated buffer</param>
    /// <returns>A new CountingBloomFilter instance</returns>
    public static CountingBloomFilter Create(int arraySize, int hashFunctionCount, Span<byte> buffer = default)
    {
        Span<byte> counters;
        if (buffer.Length >= arraySize)
        {
            counters = buffer[..arraySize];
            counters.Clear();
        }
        else
        {
            counters = new byte[arraySize];
        }

        return new CountingBloomFilter(counters, hashFunctionCount, arraySize);
    }

    /// <summary>
    /// Adds an item to the Counting Bloom filter by incrementing relevant counters.
    /// If any counter would overflow, the entire operation is skipped to maintain data integrity.
    /// </summary>
    /// <typeparam name="T">Type of the item to add</typeparam>
    /// <param name="item">Item to add to the filter</param>
    /// <returns>True if the item was successfully added, false if overflow would occur</returns>
    /// <remarks>
    /// <para>
    /// This operation increments k counters based on hash functions derived from the item.
    /// If any counter is already at maximum value (255), the operation is aborted to prevent overflow.
    /// </para>
    /// <para>
    /// Time complexity: O(k) where k is the number of hash functions.
    /// The operation is atomic - either all counters are incremented or none are.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Add<T>(T item) where T : notnull
    {
        var (hash1, hash2) = BloomFilterMath.GenerateHashValues(item);
        
        // First pass: check for potential overflows
        for (int i = 0; i < _hashFunctionCount; i++)
        {
            var index = BloomFilterMath.ComputeHashFunction(hash1, hash2, i, _arraySize);
            if (_counters[index] == MaxCounterValue)
            {
                return false; // Would overflow, abort operation
            }
        }
        
        // Second pass: increment all counters
        for (int i = 0; i < _hashFunctionCount; i++)
        {
            var index = BloomFilterMath.ComputeHashFunction(hash1, hash2, i, _arraySize);
            _counters[index]++;
        }
        
        return true;
    }

    /// <summary>
    /// Removes an item from the Counting Bloom filter by decrementing relevant counters.
    /// If any counter is already zero, the entire operation is skipped to prevent underflow.
    /// </summary>
    /// <typeparam name="T">Type of the item to remove</typeparam>
    /// <param name="item">Item to remove from the filter</param>
    /// <returns>True if the item was successfully removed, false if it was not present or underflow would occur</returns>
    /// <remarks>
    /// <para>
    /// This operation decrements k counters based on hash functions derived from the item.
    /// If any counter is already zero, the item was not in the filter (or has already been removed),
    /// so the operation is aborted to prevent underflow corruption.
    /// </para>
    /// <para>
    /// Time complexity: O(k) where k is the number of hash functions.
    /// The operation is atomic - either all counters are decremented or none are.
    /// Note: This can introduce false negatives if items were not actually added to the filter.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove<T>(T item) where T : notnull
    {
        var (hash1, hash2) = BloomFilterMath.GenerateHashValues(item);
        
        // First pass: check for potential underflows
        for (int i = 0; i < _hashFunctionCount; i++)
        {
            var index = BloomFilterMath.ComputeHashFunction(hash1, hash2, i, _arraySize);
            if (_counters[index] == 0)
            {
                return false; // Would underflow or item not present
            }
        }
        
        // Second pass: decrement all counters
        for (int i = 0; i < _hashFunctionCount; i++)
        {
            var index = BloomFilterMath.ComputeHashFunction(hash1, hash2, i, _arraySize);
            _counters[index]--;
        }
        
        return true;
    }

    /// <summary>
    /// Tests whether an item might be in the set by checking if all relevant counters are non-zero.
    /// </summary>
    /// <typeparam name="T">Type of the item to test</typeparam>
    /// <param name="item">Item to test for membership</param>
    /// <returns>True if the item might be present, false if definitely not present</returns>
    /// <remarks>
    /// Same semantics as basic Bloom filter: false positives possible, no false negatives
    /// (unless counters have been corrupted by overflow/underflow).
    /// Time complexity: O(k) where k is the number of hash functions.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MightContain<T>(T item) where T : notnull
    {
        var (hash1, hash2) = BloomFilterMath.GenerateHashValues(item);
        
        for (int i = 0; i < _hashFunctionCount; i++)
        {
            var index = BloomFilterMath.ComputeHashFunction(hash1, hash2, i, _arraySize);
            if (_counters[index] == 0)
            {
                return false; // Definitely not present
            }
        }
        
        return true; // Might be present
    }

    /// <summary>
    /// Clears the filter by resetting all counters to zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _counters.Clear();
    }

    /// <summary>
    /// Gets a read-only view of the counter array state.
    /// </summary>
    /// <returns>ReadOnlySpan&lt;byte&gt; containing the counter data</returns>
    public ReadOnlySpan<byte> GetCounters() => _counters;

    /// <summary>
    /// Estimates the number of unique items currently in the filter.
    /// This calculation accounts for the counting nature of the filter.
    /// </summary>
    /// <returns>Estimated number of items</returns>
    /// <remarks>
    /// Uses a modified estimation formula that considers counter values rather than just set bits.
    /// The estimate may be less accurate than basic Bloom filters due to counter interactions.
    /// </remarks>
    public int EstimateItemCount()
    {
        var totalCounterSum = 0L;
        var nonZeroCounters = 0;
        
        foreach (var counter in _counters)
        {
            if (counter > 0)
            {
                totalCounterSum += counter;
                nonZeroCounters++;
            }
        }
        
        if (nonZeroCounters == 0) return 0;
        
        // Estimate based on average counter value and number of hash functions
        var averageCounterValue = (double)totalCounterSum / nonZeroCounters;
        var estimatedItems = averageCounterValue * nonZeroCounters / _hashFunctionCount;
        
        return Math.Max(0, (int)Math.Round(estimatedItems));
    }

    /// <summary>
    /// Gets statistics about the current state of the counter array.
    /// </summary>
    /// <returns>Tuple containing (nonZeroCounters, maxCounterValue, averageCounterValue)</returns>
    public (int NonZeroCounters, byte MaxCounterValue, double AverageCounterValue) GetStatistics()
    {
        var nonZeroCounters = 0;
        var maxValue = (byte)0;
        var totalSum = 0L;
        
        foreach (var counter in _counters)
        {
            if (counter > 0)
            {
                nonZeroCounters++;
                totalSum += counter;
                if (counter > maxValue)
                {
                    maxValue = counter;
                }
            }
        }
        
        var averageValue = nonZeroCounters > 0 ? (double)totalSum / nonZeroCounters : 0.0;
        
        return (nonZeroCounters, maxValue, averageValue);
    }

    /// <summary>
    /// Checks if the filter is approaching counter overflow limits.
    /// </summary>
    /// <param name="threshold">Threshold percentage (0.0-1.0) of maximum counter value</param>
    /// <returns>True if any counter exceeds the threshold</returns>
    /// <remarks>
    /// Use this method to monitor filter health. When counters approach overflow,
    /// consider resizing the filter or using a different data structure.
    /// </remarks>
    public bool IsApproachingOverflow(double threshold = 0.9)
    {
        var thresholdValue = (byte)(MaxCounterValue * threshold);
        
        foreach (var counter in _counters)
        {
            if (counter >= thresholdValue)
            {
                return true;
            }
        }
        
        return false;
    }
}