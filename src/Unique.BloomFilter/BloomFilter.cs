using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unique.BloomFilter.Core;

namespace Unique.BloomFilter;

/// <summary>
/// A high-performance Bloom filter implementation using Span&lt;T&gt; for memory optimization.
/// This ref struct provides probabilistic set membership testing with configurable false positive rates.
/// Uses smart allocation strategies: stack allocation for small filters (≤1KB), heap for larger ones.
/// </summary>
/// <remarks>
/// <para>
/// Bloom filters are space-efficient probabilistic data structures designed to test whether an element
/// is a member of a set. False positive matches are possible, but false negatives are not.
/// </para>
/// <para>
/// This implementation uses System.HashCode with double hashing for generating multiple hash functions,
/// and leverages Span&lt;T&gt; for zero-allocation bit manipulation where possible.
/// </para>
/// <para>
/// Memory allocation strategy:
/// - Filters ≤ 1KB use stackalloc for maximum performance
/// - Larger filters use heap allocation with 64-bit aligned access patterns
/// </para>
/// </remarks>
public readonly ref struct BloomFilter
{
    /// <summary>
    /// The bit array storing the Bloom filter data. Uses Span&lt;byte&gt; for efficient memory access.
    /// Each bit represents a hash bucket, with set bits indicating potential membership.
    /// </summary>
    private readonly Span<byte> _bits;
    
    /// <summary>
    /// Number of hash functions used by this filter. Optimal value calculated based on
    /// bit array size and expected element count for minimizing false positive rate.
    /// </summary>
    private readonly int _hashFunctionCount;
    
    /// <summary>
    /// Size of the bit array in bits (not bytes). Used for hash function calculations
    /// and bounds checking during bit manipulation operations.
    /// </summary>
    private readonly int _bitArraySize;

    /// <summary>
    /// Initializes a new Bloom filter with the specified bit array and parameters.
    /// This constructor is typically called by factory methods that handle memory allocation.
    /// </summary>
    /// <param name="bits">Pre-allocated bit array as a Span&lt;byte&gt;</param>
    /// <param name="hashFunctionCount">Number of hash functions to use (k)</param>
    /// <param name="bitArraySize">Size of the bit array in bits (m)</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when hashFunctionCount ≤ 0, bitArraySize ≤ 0, or bits length is insufficient
    /// </exception>
    private BloomFilter(Span<byte> bits, int hashFunctionCount, int bitArraySize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hashFunctionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bitArraySize);
        
        var requiredBytes = (bitArraySize + 7) / 8;
        if (bits.Length < requiredBytes)
        {
            throw new ArgumentException($"Bit array too small. Required: {requiredBytes} bytes, provided: {bits.Length} bytes", nameof(bits));
        }

        _bits = bits;
        _hashFunctionCount = hashFunctionCount;
        _bitArraySize = bitArraySize;
    }

    /// <summary>
    /// Gets the number of hash functions used by this Bloom filter.
    /// </summary>
    public int HashFunctionCount => _hashFunctionCount;

    /// <summary>
    /// Gets the size of the bit array in bits.
    /// </summary>
    public int BitArraySize => _bitArraySize;

    /// <summary>
    /// Gets the memory usage of this Bloom filter in bytes.
    /// </summary>
    public int MemoryUsage => _bits.Length;

    /// <summary>
    /// Creates a new Bloom filter optimized for the specified capacity and false positive rate.
    /// Automatically calculates optimal bit array size and hash function count.
    /// </summary>
    /// <param name="capacity">Expected number of elements to be added</param>
    /// <param name="falsePositiveRate">Desired false positive rate (between 0.0 and 1.0)</param>
    /// <param name="buffer">Optional pre-allocated buffer. If not provided, appropriate allocation strategy will be used</param>
    /// <returns>A new BloomFilter instance configured with optimal parameters</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when capacity ≤ 0 or falsePositiveRate is not between 0.0 and 1.0
    /// </exception>
    /// <example>
    /// <code>
    /// // Create a filter for 1000 items with 1% false positive rate
    /// var filter = BloomFilter.Create(1000, 0.01);
    /// 
    /// // Add items
    /// filter.Add("hello");
    /// filter.Add(42);
    /// 
    /// // Test membership
    /// bool found = filter.MightContain("hello"); // true
    /// bool notFound = filter.MightContain("world"); // likely false
    /// </code>
    /// </example>
    public static BloomFilter Create(int capacity, double falsePositiveRate, Span<byte> buffer = default)
    {
        var (bitArraySize, hashFunctionCount, memoryUsage) = BloomFilterMath.ComputeOptimalParameters(capacity, falsePositiveRate);
        
        // Use provided buffer if available and sufficient, otherwise allocate appropriately
        Span<byte> bits;
        if (buffer.Length >= memoryUsage)
        {
            bits = buffer[..memoryUsage];
            bits.Clear(); // Ensure clean state
        }
        else if (BloomFilterMath.ShouldUseStackAllocation(bitArraySize))
        {
            // For small filters, use stack allocation - this will be handled by the caller
            throw new InvalidOperationException("Stack allocation must be handled by caller. Use CreateWithStackAlloc for small filters.");
        }
        else
        {
            // Allocate on heap for larger filters
            bits = new byte[memoryUsage];
        }

        return new BloomFilter(bits, hashFunctionCount, bitArraySize);
    }

    /// <summary>
    /// Creates a new Bloom filter with manually specified parameters.
    /// Use this method when you want precise control over bit array size and hash function count.
    /// </summary>
    /// <param name="bitArraySize">Size of the bit array in bits</param>
    /// <param name="hashFunctionCount">Number of hash functions to use</param>
    /// <param name="buffer">Optional pre-allocated buffer</param>
    /// <returns>A new BloomFilter instance with the specified parameters</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when bitArraySize ≤ 0 or hashFunctionCount ≤ 0
    /// </exception>
    public static BloomFilter Create(int bitArraySize, int hashFunctionCount, Span<byte> buffer = default)
    {
        var memoryUsage = BloomFilterMath.CalculateMemoryUsage(bitArraySize);
        
        Span<byte> bits;
        if (buffer.Length >= memoryUsage)
        {
            bits = buffer[..memoryUsage];
            bits.Clear();
        }
        else
        {
            bits = new byte[memoryUsage];
        }

        return new BloomFilter(bits, hashFunctionCount, bitArraySize);
    }

    /// <summary>
    /// Adds an item to the Bloom filter. After adding, MightContain() for this item will always return true.
    /// Items cannot be removed from a basic Bloom filter.
    /// </summary>
    /// <typeparam name="T">Type of the item to add (must be non-null)</typeparam>
    /// <param name="item">Item to add to the filter</param>
    /// <remarks>
    /// This operation sets k bits in the bit array based on hash functions derived from the item.
    /// Time complexity: O(k) where k is the number of hash functions.
    /// The operation is safe to call multiple times with the same item.
    /// </remarks>
    /// <example>
    /// <code>
    /// var filter = BloomFilter.Create(1000, 0.01);
    /// filter.Add("hello");
    /// filter.Add(42);
    /// filter.Add(new Guid());
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add<T>(T item) where T : notnull
    {
        var (hash1, hash2) = BloomFilterMath.GenerateHashValues(item);
        
        for (int i = 0; i < _hashFunctionCount; i++)
        {
            var bitIndex = BloomFilterMath.ComputeHashFunction(hash1, hash2, i, _bitArraySize);
            SetBit(bitIndex);
        }
    }

    /// <summary>
    /// Tests whether an item might be in the set. Returns true if the item might be present,
    /// false if the item is definitely not present.
    /// </summary>
    /// <typeparam name="T">Type of the item to test (must be non-null)</typeparam>
    /// <param name="item">Item to test for membership</param>
    /// <returns>
    /// True if the item might be in the set (with possibility of false positive),
    /// False if the item is definitely not in the set (no false negatives)
    /// </returns>
    /// <remarks>
    /// <para>
    /// This operation checks k bits in the bit array. If any bit is unset, the item is definitely not present.
    /// If all bits are set, the item might be present (could be a false positive).
    /// </para>
    /// <para>
    /// Time complexity: O(k) where k is the number of hash functions.
    /// False positive probability depends on the filter's current load and configuration.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var filter = BloomFilter.Create(1000, 0.01);
    /// filter.Add("hello");
    /// 
    /// bool found = filter.MightContain("hello");    // Always true
    /// bool notFound = filter.MightContain("world"); // Likely false, but could be true (false positive)
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MightContain<T>(T item) where T : notnull
    {
        var (hash1, hash2) = BloomFilterMath.GenerateHashValues(item);
        
        for (int i = 0; i < _hashFunctionCount; i++)
        {
            var bitIndex = BloomFilterMath.ComputeHashFunction(hash1, hash2, i, _bitArraySize);
            if (!GetBit(bitIndex))
            {
                return false; // Definitely not present
            }
        }
        
        return true; // Might be present (could be false positive)
    }

    /// <summary>
    /// Clears the Bloom filter, removing all items. After clearing, MightContain() will return false for all items.
    /// </summary>
    /// <remarks>
    /// This operation resets all bits to zero, effectively creating an empty filter.
    /// Time complexity: O(n) where n is the size of the bit array in bytes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _bits.Clear();
    }

    /// <summary>
    /// Gets a read-only view of the internal bit array state.
    /// Useful for serialization, debugging, or creating snapshots of the filter state.
    /// </summary>
    /// <returns>ReadOnlySpan&lt;byte&gt; containing the bit array data</returns>
    /// <remarks>
    /// The returned span reflects the current state of the filter and should not be modified.
    /// Changes to the original filter after calling this method may be reflected in the returned span.
    /// </remarks>
    public ReadOnlySpan<byte> GetState() => _bits;

    /// <summary>
    /// Estimates the number of unique items that have been added to this filter.
    /// This is an approximation based on the number of set bits and filter parameters.
    /// </summary>
    /// <returns>Estimated number of unique items added</returns>
    /// <remarks>
    /// Uses the formula: n ≈ -m * ln(1 - X/m) / k
    /// where m = bit array size, X = number of set bits, k = hash function count
    /// The estimate becomes less accurate as the filter approaches saturation.
    /// </remarks>
    public int EstimateItemCount()
    {
        var setBits = CountSetBits();
        if (setBits == 0) return 0;
        if (setBits == _bitArraySize) return int.MaxValue; // Saturated filter
        
        var ratio = (double)setBits / _bitArraySize;
        var estimate = -_bitArraySize * Math.Log(1.0 - ratio) / _hashFunctionCount;
        
        return Math.Max(0, (int)Math.Round(estimate));
    }

    /// <summary>
    /// Calculates the current false positive probability based on the filter's state.
    /// </summary>
    /// <returns>Current estimated false positive probability</returns>
    public double GetCurrentFalsePositiveRate()
    {
        var estimatedItems = EstimateItemCount();
        if (estimatedItems == 0) return 0.0;
        
        return BloomFilterMath.CalculateActualFalsePositiveRate(_bitArraySize, _hashFunctionCount, estimatedItems);
    }

    /// <summary>
    /// Sets a specific bit in the bit array to 1.
    /// </summary>
    /// <param name="bitIndex">Index of the bit to set (must be within bounds)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetBit(int bitIndex)
    {
        var byteIndex = bitIndex >> 3;    // Divide by 8 (bitIndex / 8)
        var bitOffset = bitIndex & 7;     // Modulo 8 (bitIndex % 8)
        
        _bits[byteIndex] |= (byte)(1 << bitOffset);
    }

    /// <summary>
    /// Gets the value of a specific bit in the bit array.
    /// </summary>
    /// <param name="bitIndex">Index of the bit to get (must be within bounds)</param>
    /// <returns>True if the bit is set (1), false if unset (0)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool GetBit(int bitIndex)
    {
        var byteIndex = bitIndex >> 3;    // Divide by 8
        var bitOffset = bitIndex & 7;     // Modulo 8
        
        return (_bits[byteIndex] & (1 << bitOffset)) != 0;
    }

    /// <summary>
    /// Counts the total number of set bits in the filter.
    /// Used for statistics and item count estimation.
    /// </summary>
    /// <returns>Number of bits set to 1</returns>
    private int CountSetBits()
    {
        var count = 0;
        var span = MemoryMarshal.Cast<byte, ulong>(_bits);
        
        // Count set bits in 64-bit chunks for better performance
        foreach (var chunk in span)
        {
            count += BitOperations.PopCount(chunk);
        }
        
        // Handle remaining bytes that don't fit in 64-bit chunks
        var remainderStart = span.Length * 8;
        for (int i = remainderStart; i < _bits.Length; i++)
        {
            count += BitOperations.PopCount(_bits[i]);
        }
        
        return count;
    }
}