using Unique.BloomFilter.Core;

namespace Unique.BloomFilter.Tests;

/// <summary>
/// Unit tests for the BloomFilterMath utility class.
/// Tests mathematical calculations and parameter optimization functions.
/// </summary>
public class BloomFilterMathTests
{
    [Theory]
    [InlineData(1000, 0.01, 9585)]  // ~9.6 bits per element for 1% FPR
    [InlineData(10000, 0.01, 95840)]
    [InlineData(1000, 0.001, 14376)] // ~14.4 bits per element for 0.1% FPR
    public void CalculateOptimalBitArraySize_ReturnsExpectedSize(int capacity, double fpr, int expectedMinSize)
    {
        // Act
        var actualSize = BloomFilterMath.CalculateOptimalBitArraySize(capacity, fpr);
        
        // Assert - should be close to theoretical minimum and byte-aligned
        actualSize.Should().BeGreaterThanOrEqualTo(expectedMinSize);
        actualSize.Should().BeGreaterThanOrEqualTo(8); // Minimum size
        (actualSize % 8).Should().Be(0); // Byte-aligned
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CalculateOptimalBitArraySize_ThrowsForInvalidCapacity(int invalidCapacity)
    {
        // Act & Assert
        var act = () => BloomFilterMath.CalculateOptimalBitArraySize(invalidCapacity, 0.01);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.0)]
    [InlineData(1.1)]
    public void CalculateOptimalBitArraySize_ThrowsForInvalidFalsePositiveRate(double invalidFpr)
    {
        // Act & Assert
        var act = () => BloomFilterMath.CalculateOptimalBitArraySize(1000, invalidFpr);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(9600, 1000, 7)] // Optimal k should be around 7 for this ratio
    [InlineData(4800, 1000, 3)]
    [InlineData(19200, 1000, 13)]
    public void CalculateOptimalHashFunctionCount_ReturnsReasonableValues(int bitArraySize, int capacity, int expectedK)
    {
        // Act
        var actualK = BloomFilterMath.CalculateOptimalHashFunctionCount(bitArraySize, capacity);
        
        // Assert - should be within reasonable range of expected value
        actualK.Should().BeInRange(Math.Max(1, expectedK - 2), Math.Min(64, expectedK + 2));
        actualK.Should().BeInRange(1, 64); // Clamped bounds
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculateOptimalHashFunctionCount_ThrowsForInvalidParameters(int invalidValue)
    {
        // Act & Assert
        var bitArrayAct = () => BloomFilterMath.CalculateOptimalHashFunctionCount(invalidValue, 1000);
        var capacityAct = () => BloomFilterMath.CalculateOptimalHashFunctionCount(1000, invalidValue);
        
        bitArrayAct.Should().Throw<ArgumentOutOfRangeException>();
        capacityAct.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CalculateActualFalsePositiveRate_ReturnsExpectedProbability()
    {
        // Arrange - parameters for ~1% false positive rate
        const int bitArraySize = 9600;
        const int hashFunctionCount = 7;
        const int insertedElements = 1000;
        
        // Act
        var actualFpr = BloomFilterMath.CalculateActualFalsePositiveRate(bitArraySize, hashFunctionCount, insertedElements);
        
        // Assert - should be close to 1% (0.01)
        actualFpr.Should().BeInRange(0.005, 0.02);
        actualFpr.Should().BeInRange(0.0, 1.0); // Valid probability range
    }

    [Fact]
    public void GenerateHashValues_ProducesDifferentValuesForDifferentInputs()
    {
        // Arrange
        var item1 = "hello";
        var item2 = "world";
        var item3 = 42;
        
        // Act
        var (hash1_1, hash2_1) = BloomFilterMath.GenerateHashValues(item1);
        var (hash1_2, hash2_2) = BloomFilterMath.GenerateHashValues(item2);
        var (hash1_3, hash2_3) = BloomFilterMath.GenerateHashValues(item3);
        
        // Assert - different items should produce different hash values
        hash1_1.Should().NotBe(hash1_2);
        hash1_1.Should().NotBe(hash1_3);
        hash2_1.Should().NotBe(hash2_2);
        
        // Hash1 and Hash2 for same item should be different
        hash1_1.Should().NotBe(hash2_1);
        hash1_2.Should().NotBe(hash2_2);
    }

    [Fact]
    public void GenerateHashValues_ProducesConsistentValuesForSameInput()
    {
        // Arrange
        var item = "test";
        
        // Act
        var (hash1_first, hash2_first) = BloomFilterMath.GenerateHashValues(item);
        var (hash1_second, hash2_second) = BloomFilterMath.GenerateHashValues(item);
        
        // Assert - same input should always produce same hash values
        hash1_first.Should().Be(hash1_second);
        hash2_first.Should().Be(hash2_second);
    }

    [Fact]
    public void ComputeHashFunction_ProducesDifferentIndicesForDifferentHashNumbers()
    {
        // Arrange
        const uint hash1 = 12345;
        const uint hash2 = 67890;
        const int bitArraySize = 1000;
        
        // Act
        var index0 = BloomFilterMath.ComputeHashFunction(hash1, hash2, 0, bitArraySize);
        var index1 = BloomFilterMath.ComputeHashFunction(hash1, hash2, 1, bitArraySize);
        var index2 = BloomFilterMath.ComputeHashFunction(hash1, hash2, 2, bitArraySize);
        
        // Assert
        index0.Should().BeInRange(0, bitArraySize - 1);
        index1.Should().BeInRange(0, bitArraySize - 1);
        index2.Should().BeInRange(0, bitArraySize - 1);
        
        // Different hash indices should produce different bit indices (usually)
        // Note: There's a small chance they could be equal due to modulo, so we test multiple
        var indices = new[] { index0, index1, index2 };
        indices.Distinct().Should().HaveCount(3, "hash functions should produce different indices");
    }

    [Theory]
    [InlineData(800)] // 100 bytes < 1KB
    [InlineData(8000)] // 1000 bytes ≈ 1KB 
    [InlineData(8192)] // Exactly 1KB in bits
    public void ShouldUseStackAllocation_ReturnsTrueForSmallFilters(int bitArraySize)
    {
        // Act
        var shouldUseStack = BloomFilterMath.ShouldUseStackAllocation(bitArraySize);
        
        // Assert
        var memoryRequired = (bitArraySize + 7) / 8;
        if (memoryRequired <= BloomFilterMath.MaxStackAllocSize)
        {
            shouldUseStack.Should().BeTrue();
        }
        else
        {
            shouldUseStack.Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(10000)] // > 1KB
    [InlineData(80000)] // Much larger
    public void ShouldUseStackAllocation_ReturnsFalseForLargeFilters(int bitArraySize)
    {
        // Act
        var shouldUseStack = BloomFilterMath.ShouldUseStackAllocation(bitArraySize);
        
        // Assert
        shouldUseStack.Should().BeFalse();
    }

    [Fact]
    public void CalculateMemoryUsage_ReturnsCorrectByteCount()
    {
        // Arrange
        var testCases = new[]
        {
            (BitArraySize: 8, ExpectedBytes: 1),
            (BitArraySize: 15, ExpectedBytes: 2),
            (BitArraySize: 16, ExpectedBytes: 2),
            (BitArraySize: 1000, ExpectedBytes: 125),
            (BitArraySize: 1001, ExpectedBytes: 126)
        };
        
        foreach (var (bitArraySize, expectedBytes) in testCases)
        {
            // Act
            var actualBytes = BloomFilterMath.CalculateMemoryUsage(bitArraySize);
            
            // Assert
            actualBytes.Should().Be(expectedBytes, 
                $"bit array size {bitArraySize} should require {expectedBytes} bytes");
        }
    }

    [Fact]
    public void ComputeOptimalParameters_ReturnsConsistentValues()
    {
        // Arrange
        const int capacity = 1000;
        const double falsePositiveRate = 0.01;
        
        // Act
        var (bitArraySize, hashFunctionCount, memoryUsage) = 
            BloomFilterMath.ComputeOptimalParameters(capacity, falsePositiveRate);
        
        // Assert
        bitArraySize.Should().BeGreaterThan(0);
        hashFunctionCount.Should().BeInRange(1, 64);
        memoryUsage.Should().Be((bitArraySize + 7) / 8);
        
        // Verify consistency with individual methods
        var expectedBitArraySize = BloomFilterMath.CalculateOptimalBitArraySize(capacity, falsePositiveRate);
        var expectedHashFunctionCount = BloomFilterMath.CalculateOptimalHashFunctionCount(expectedBitArraySize, capacity);
        var expectedMemoryUsage = BloomFilterMath.CalculateMemoryUsage(expectedBitArraySize);
        
        bitArraySize.Should().Be(expectedBitArraySize);
        hashFunctionCount.Should().Be(expectedHashFunctionCount);
        memoryUsage.Should().Be(expectedMemoryUsage);
    }
}