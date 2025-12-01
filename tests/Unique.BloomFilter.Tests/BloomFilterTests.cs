namespace Unique.BloomFilter.Tests;

/// <summary>
/// Unit tests for the BloomFilter ref struct.
/// Tests basic functionality, memory optimization, and probabilistic behavior.
/// </summary>
public class BloomFilterTests
{
    [Fact]
    public void Create_WithCapacityAndErrorRate_ShouldInitializeCorrectly()
    {
        // Arrange
        const int capacity = 1000;
        const double errorRate = 0.01;
        
        // Act
        var buffer = new byte[2000]; // Provide sufficient buffer
        var filter = BloomFilter.Create(capacity, errorRate, buffer);
        
        // Assert
        filter.HashFunctionCount.Should().BeGreaterThan(0);
        filter.BitArraySize.Should().BeGreaterThan(0);
        filter.MemoryUsage.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Create_WithManualParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        const int bitArraySize = 1000;
        const int hashFunctionCount = 7;
        
        // Act
        var buffer = new byte[200]; // Sufficient for 1000 bits
        var filter = BloomFilter.Create(bitArraySize, hashFunctionCount, buffer);
        
        // Assert
        filter.HashFunctionCount.Should().Be(hashFunctionCount);
        filter.BitArraySize.Should().Be(bitArraySize);
    }

    [Fact]
    public void Add_SingleItem_ShouldAllowRetrieval()
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        const string item = "test";
        
        // Act
        filter.Add(item);
        var result = filter.MightContain(item);
        
        // Assert
        result.Should().BeTrue("added items should always return true");
    }

    [Fact]
    public void Add_MultipleItems_ShouldAllowRetrievalOfAll()
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        var items = new[] { "hello", "world", "test", "bloom", "filter" };
        
        // Act
        foreach (var item in items)
        {
            filter.Add(item);
        }
        
        // Assert
        foreach (var item in items)
        {
            filter.MightContain(item).Should().BeTrue($"added item '{item}' should be found");
        }
    }

    [Fact]
    public void MightContain_ItemNotAdded_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        filter.Add("existing");
        
        // Act
        var result = filter.MightContain("notadded");
        
        // Assert - Note: this could be a false positive, but very unlikely with good parameters
        // We test with a simple case that should definitely return false
        result.Should().BeFalse("item not added should likely return false");
    }

    [Fact]
    public void MightContain_EmptyFilter_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        var result = filter.MightContain("anything");
        
        // Assert
        result.Should().BeFalse("empty filter should always return false");
    }

    [Fact]
    public void Clear_AfterAddingItems_ShouldMakeFilterEmpty()
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        filter.Add("test1");
        filter.Add("test2");
        
        // Act
        filter.Clear();
        
        // Assert
        filter.MightContain("test1").Should().BeFalse();
        filter.MightContain("test2").Should().BeFalse();
        filter.EstimateItemCount().Should().Be(0);
    }

    [Theory]
    [InlineData("string")]
    [InlineData(42)]
    [InlineData(3.14)]
    [InlineData(true)]
    public void Add_DifferentTypes_ShouldWork<T>(T item) where T : notnull
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        filter.Add(item);
        var result = filter.MightContain(item);
        
        // Assert
        result.Should().BeTrue($"added item of type {typeof(T).Name} should be found");
    }

    [Fact]
    public void Add_SameItemMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        const string item = "duplicate";
        
        // Act
        filter.Add(item);
        filter.Add(item);
        filter.Add(item);
        var result = filter.MightContain(item);
        
        // Assert
        result.Should().BeTrue();
        // Item count estimation should account for duplicates
        filter.EstimateItemCount().Should().BeLessOrEqualTo(10); // Should be much less than 3
    }

    [Fact]
    public void EstimateItemCount_EmptyFilter_ShouldReturnZero()
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        var count = filter.EstimateItemCount();
        
        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void EstimateItemCount_WithKnownItems_ShouldBeReasonablyAccurate()
    {
        // Arrange
        var buffer = new byte[2000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        var items = Enumerable.Range(0, 50).Select(i => $"item_{i}").ToList();
        
        // Act
        foreach (var item in items)
        {
            filter.Add(item);
        }
        var estimatedCount = filter.EstimateItemCount();
        
        // Assert
        // Estimation should be reasonably close to actual count (within 50% for this test)
        estimatedCount.Should().BeInRange(25, 75, "estimated count should be reasonably accurate");
    }

    [Fact]
    public void GetCurrentFalsePositiveRate_EmptyFilter_ShouldReturnZero()
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        var fpr = filter.GetCurrentFalsePositiveRate();
        
        // Assert
        fpr.Should().Be(0.0);
    }

    [Fact]
    public void GetCurrentFalsePositiveRate_WithItems_ShouldReturnReasonableValue()
    {
        // Arrange
        var buffer = new byte[2000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        
        // Add some items
        for (int i = 0; i < 100; i++)
        {
            filter.Add($"item_{i}");
        }
        
        // Act
        var fpr = filter.GetCurrentFalsePositiveRate();
        
        // Assert
        fpr.Should().BeInRange(0.0, 1.0);
        fpr.Should().BeLessOrEqualTo(0.05); // Should be reasonably low for this load
    }

    [Fact]
    public void GetState_ShouldReturnReadOnlySpan()
    {
        // Arrange
        var buffer = new byte[1000];
        var filter = BloomFilter.Create(1000, 0.01, buffer);
        filter.Add("test");
        
        // Act
        var state = filter.GetState();
        
        // Assert
        state.Length.Should().BeGreaterThan(0);
        state.Length.Should().Be(filter.MemoryUsage);
        
        // State should contain some non-zero bytes after adding an item
        var hasNonZeroByte = false;
        foreach (var b in state)
        {
            if (b != 0)
            {
                hasNonZeroByte = true;
                break;
            }
        }
        hasNonZeroByte.Should().BeTrue("filter state should have non-zero bytes after adding items");
    }

    [Fact]
    public void BloomFilter_LargeScale_ShouldMaintainPerformanceCharacteristics()
    {
        // Arrange
        const int capacity = 10000;
        const double targetFpr = 0.01;
        var buffer = new byte[20000]; // Large buffer
        var filter = BloomFilter.Create(capacity, targetFpr, buffer);
        
        var itemsToAdd = Enumerable.Range(0, capacity / 10).Select(i => $"large_test_{i}").ToList();
        var itemsNotAdded = Enumerable.Range(capacity, capacity / 10).Select(i => $"not_added_{i}").ToList();
        
        // Act - Add items
        foreach (var item in itemsToAdd)
        {
            filter.Add(item);
        }
        
        // Assert - All added items should be found
        foreach (var item in itemsToAdd)
        {
            filter.MightContain(item).Should().BeTrue($"added item {item} should be found");
        }
        
        // Check false positive rate with items not added
        var falsePositives = 0;
        foreach (var item in itemsNotAdded)
        {
            if (filter.MightContain(item))
            {
                falsePositives++;
            }
        }
        
        var actualFpr = (double)falsePositives / itemsNotAdded.Count;
        actualFpr.Should().BeLessOrEqualTo(targetFpr * 3, // Allow some margin due to randomness
            $"false positive rate should be reasonably close to target {targetFpr}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_InvalidCapacity_ShouldThrow(int invalidCapacity)
    {
        // Act & Assert
        Action act = () => BloomFilter.Create(invalidCapacity, 0.01);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.0)]
    [InlineData(1.1)]
    public void Create_InvalidErrorRate_ShouldThrow(double invalidErrorRate)
    {
        // Act & Assert  
        Action act = () => BloomFilter.Create(1000, invalidErrorRate);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}