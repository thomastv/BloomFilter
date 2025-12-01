namespace Unique.BloomFilter.Tests;

/// <summary>
/// Unit tests for the CountingBloomFilter ref struct.
/// Tests add/remove functionality, overflow protection, and counting-specific behavior.
/// </summary>
public class CountingBloomFilterTests
{
    [Fact]
    public void Create_WithCapacityAndErrorRate_ShouldInitializeCorrectly()
    {
        // Arrange
        const int capacity = 1000;
        const double errorRate = 0.01;
        
        // Act
        var buffer = new byte[20000]; // Large buffer for counters
        var filter = CountingBloomFilter.Create(capacity, errorRate, buffer);
        
        // Assert
        filter.HashFunctionCount.Should().BeGreaterThan(0);
        filter.ArraySize.Should().BeGreaterThan(0);
        filter.MemoryUsage.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Add_SingleItem_ShouldAllowRetrieval()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        const string item = "test";
        
        // Act
        var added = filter.Add(item);
        var result = filter.MightContain(item);
        
        // Assert
        added.Should().BeTrue("add operation should succeed");
        result.Should().BeTrue("added items should always return true");
    }

    [Fact]
    public void Add_MultipleItems_ShouldAllowRetrievalOfAll()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        var items = new[] { "hello", "world", "test", "bloom", "filter" };
        
        // Act
        foreach (var item in items)
        {
            filter.Add(item).Should().BeTrue();
        }
        
        // Assert
        foreach (var item in items)
        {
            filter.MightContain(item).Should().BeTrue($"added item '{item}' should be found");
        }
    }

    [Fact]
    public void Remove_AddedItem_ShouldSucceedAndRemoveItem()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        const string item = "removeme";
        
        // Act
        filter.Add(item).Should().BeTrue();
        filter.MightContain(item).Should().BeTrue("item should be found after adding");
        
        var removed = filter.Remove(item);
        var stillThere = filter.MightContain(item);
        
        // Assert
        removed.Should().BeTrue("remove operation should succeed");
        stillThere.Should().BeFalse("item should not be found after removal");
    }

    [Fact]
    public void Remove_ItemNotAdded_ShouldFail()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        var removed = filter.Remove("notadded");
        
        // Assert
        removed.Should().BeFalse("removing non-existent item should fail");
    }

    [Fact]
    public void Add_Remove_Add_ShouldWorkCorrectly()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        const string item = "cycle";
        
        // Act & Assert
        filter.Add(item).Should().BeTrue();
        filter.MightContain(item).Should().BeTrue();
        
        filter.Remove(item).Should().BeTrue();
        filter.MightContain(item).Should().BeFalse();
        
        filter.Add(item).Should().BeTrue();
        filter.MightContain(item).Should().BeTrue();
    }

    [Fact]
    public void Add_SameItemMultipleTimes_ShouldIncrementCounters()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        const string item = "duplicate";
        
        // Act
        filter.Add(item).Should().BeTrue();
        filter.Add(item).Should().BeTrue();
        filter.Add(item).Should().BeTrue();
        
        // Assert
        filter.MightContain(item).Should().BeTrue();
        
        // Should require multiple removals to fully remove
        filter.Remove(item).Should().BeTrue();
        filter.MightContain(item).Should().BeTrue("should still be present after one removal");
        
        filter.Remove(item).Should().BeTrue();
        filter.MightContain(item).Should().BeTrue("should still be present after two removals");
        
        filter.Remove(item).Should().BeTrue();
        filter.MightContain(item).Should().BeFalse("should be gone after three removals");
    }

    [Fact]
    public void Remove_MoreTimesThanAdded_ShouldFail()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        const string item = "overremove";
        
        // Act
        filter.Add(item).Should().BeTrue();
        filter.Remove(item).Should().BeTrue();
        
        // Try to remove again
        var secondRemove = filter.Remove(item);
        
        // Assert
        secondRemove.Should().BeFalse("removing more times than added should fail");
    }

    [Fact]
    public void Clear_AfterAddingItems_ShouldMakeFilterEmpty()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
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
    public void Add_Remove_DifferentTypes_ShouldWork<T>(T item) where T : notnull
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        filter.Add(item).Should().BeTrue();
        filter.MightContain(item).Should().BeTrue();
        
        filter.Remove(item).Should().BeTrue();
        filter.MightContain(item).Should().BeFalse();
    }

    [Fact]
    public void EstimateItemCount_EmptyFilter_ShouldReturnZero()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        var count = filter.EstimateItemCount();
        
        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void EstimateItemCount_WithKnownItems_ShouldBeReasonablyAccurate()
    {
        // Arrange
        var buffer = new byte[20000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        var itemCount = 20;
        
        // Act
        for (int i = 0; i < itemCount; i++)
        {
            filter.Add($"item_{i}");
        }
        var estimatedCount = filter.EstimateItemCount();
        
        // Assert
        // Estimation should be reasonably close (within 100% for this small test)
        estimatedCount.Should().BeInRange(itemCount / 2, itemCount * 2);
    }

    [Fact]
    public void GetStatistics_EmptyFilter_ShouldReturnZeros()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        var (nonZeroCounters, maxValue, avgValue) = filter.GetStatistics();
        
        // Assert
        nonZeroCounters.Should().Be(0);
        maxValue.Should().Be(0);
        avgValue.Should().Be(0.0);
    }

    [Fact]
    public void GetStatistics_WithItems_ShouldReturnReasonableValues()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        filter.Add("test1");
        filter.Add("test2");
        filter.Add("test1"); // Add duplicate to increase counters
        
        var (nonZeroCounters, maxValue, avgValue) = filter.GetStatistics();
        
        // Assert
        nonZeroCounters.Should().BeGreaterThan(0);
        maxValue.Should().BeGreaterThan(0);
        maxValue.Should().BeLessOrEqualTo(2); // Should be 1 or 2 based on overlaps
        avgValue.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void GetCounters_ShouldReturnReadOnlySpan()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        filter.Add("test");
        
        // Act
        var counters = filter.GetCounters();
        
        // Assert
        counters.Length.Should().Be(filter.ArraySize);
        
        // Should contain some non-zero values after adding
        var hasNonZero = false;
        foreach (var counter in counters)
        {
            if (counter > 0)
            {
                hasNonZero = true;
                break;
            }
        }
        hasNonZero.Should().BeTrue("counters should have non-zero values after adding items");
    }

    [Fact]
    public void IsApproachingOverflow_EmptyFilter_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        
        // Act
        var approaching = filter.IsApproachingOverflow();
        
        // Assert
        approaching.Should().BeFalse();
    }

    [Fact]
    public void IsApproachingOverflow_WithNormalLoad_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new byte[10000];
        var filter = CountingBloomFilter.Create(1000, 0.01, buffer);
        
        // Add reasonable number of items
        for (int i = 0; i < 50; i++)
        {
            filter.Add($"item_{i}");
        }
        
        // Act
        var approaching = filter.IsApproachingOverflow();
        
        // Assert
        approaching.Should().BeFalse("normal load should not approach overflow");
    }

    [Fact]
    public void Add_OverflowScenario_ShouldFailGracefully()
    {
        // Arrange - Create very small filter to force overflow quickly
        var buffer = new byte[10]; // Very small buffer
        var filter = CountingBloomFilter.Create(10, 10, buffer); // Small array with many hash functions
        
        // Act - Keep adding until we hit overflow
        bool overflowDetected = false;
        for (int i = 0; i < 1000 && !overflowDetected; i++)
        {
            var result = filter.Add($"overflow_test_{i}");
            if (!result)
            {
                overflowDetected = true;
            }
        }
        
        // Assert
        overflowDetected.Should().BeTrue("should eventually detect overflow with small filter and many items");
    }

    [Fact]
    public void CountingBloomFilter_MultipleItemsWithOverlap_ShouldHandleCorrectly()
    {
        // Arrange
        var buffer = new byte[5000];
        var filter = CountingBloomFilter.Create(100, 0.01, buffer); // Smaller filter to increase hash collisions
        
        var items = new[] { "apple", "banana", "cherry", "date", "elderberry" };
        
        // Act - Add all items
        foreach (var item in items)
        {
            filter.Add(item).Should().BeTrue();
        }
        
        // Verify all are found
        foreach (var item in items)
        {
            filter.MightContain(item).Should().BeTrue();
        }
        
        // Remove some items
        filter.Remove("banana").Should().BeTrue();
        filter.Remove("date").Should().BeTrue();
        
        // Assert - removed items should be gone, others should remain
        filter.MightContain("banana").Should().BeFalse();
        filter.MightContain("date").Should().BeFalse();
        filter.MightContain("apple").Should().BeTrue();
        filter.MightContain("cherry").Should().BeTrue();
        filter.MightContain("elderberry").Should().BeTrue();
    }
}