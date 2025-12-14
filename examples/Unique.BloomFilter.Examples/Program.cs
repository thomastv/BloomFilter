using Unique.BloomFilter;
using Unique.BloomFilter.Core;

Console.WriteLine("🌸 Unique.BloomFilter Library Examples");
Console.WriteLine("=====================================");
Console.WriteLine();

// Example 1: Basic Bloom Filter Usage
Console.WriteLine("📍 Example 1: Basic Bloom Filter");
Console.WriteLine("--------------------------------");

// Create a filter for 1000 items with 1% false positive rate
var buffer = new byte[2000];
var filter = BloomFilter.Create(capacity: 1000, falsePositiveRate: 0.01, buffer);

Console.WriteLine($"Created filter: {filter.BitArraySize} bits, {filter.HashFunctionCount} hash functions");
Console.WriteLine($"Memory usage: {filter.MemoryUsage} bytes");

// Add some items
var fruits = new[] { "apple", "banana", "cherry", "date", "elderberry", "fig", "grape" };
foreach (var fruit in fruits)
{
    filter.Add(fruit);
}

Console.WriteLine($"Added {fruits.Length} fruits to the filter");

// Test membership
Console.WriteLine("\n🔍 Testing membership:");
foreach (var fruit in fruits)
{
    var found = filter.MightContain(fruit);
    Console.WriteLine($"  {fruit}: {(found ? "✅ Found" : "❌ Not found")}");
}

// Test items not added
var testItems = new[] { "mango", "pineapple", "kiwi" };
Console.WriteLine("\n🔍 Testing items not added:");
foreach (var item in testItems)
{
    var found = filter.MightContain(item);
    Console.WriteLine($"  {item}: {(found ? "⚠️ False positive!" : "✅ Correctly not found")}");
}

Console.WriteLine($"\nEstimated item count: {filter.EstimateItemCount()}");
Console.WriteLine($"Current false positive rate: {filter.GetCurrentFalsePositiveRate():P2}");
Console.WriteLine();

// Example 2: Counting Bloom Filter with Removal
Console.WriteLine("📍 Example 2: Counting Bloom Filter (with removal)");
Console.WriteLine("------------------------------------------------");

var counterBuffer = new byte[10000];
var countingFilter = CountingBloomFilter.Create(capacity: 500, falsePositiveRate: 0.01, counterBuffer);

Console.WriteLine($"Created counting filter: {countingFilter.ArraySize} counters, {countingFilter.HashFunctionCount} hash functions");

// Add items
var colors = new[] { "red", "green", "blue", "yellow", "purple", "orange" };
foreach (var color in colors)
{
    var added = countingFilter.Add(color);
    Console.WriteLine($"Added '{color}': {(added ? "✅" : "❌")}");
}

Console.WriteLine($"\nEstimated item count: {countingFilter.EstimateItemCount()}");

// Add duplicates to test counting behavior
Console.WriteLine("\n📝 Adding duplicates to test counting:");
countingFilter.Add("red");
countingFilter.Add("red");
Console.WriteLine("Added 'red' two more times");

// Remove some items
Console.WriteLine("\n🗑️ Removing items:");
var removed = countingFilter.Remove("blue");
Console.WriteLine($"Removed 'blue': {(removed ? "✅" : "❌")}");

removed = countingFilter.Remove("red");
Console.WriteLine($"Removed 'red' (1st time): {(removed ? "✅" : "❌")}");

removed = countingFilter.Remove("red");
Console.WriteLine($"Removed 'red' (2nd time): {(removed ? "✅" : "❌")}");

removed = countingFilter.Remove("red");
Console.WriteLine($"Removed 'red' (3rd time): {(removed ? "✅" : "❌")}");

// Test what remains
Console.WriteLine("\n🔍 Testing remaining items:");
foreach (var color in colors)
{
    var found = countingFilter.MightContain(color);
    Console.WriteLine($"  {color}: {(found ? "✅ Found" : "❌ Not found")}");
}

// Get statistics
var (nonZeroCounters, maxValue, avgValue) = countingFilter.GetStatistics();
Console.WriteLine($"\n📊 Filter statistics:");
Console.WriteLine($"  Non-zero counters: {nonZeroCounters}");
Console.WriteLine($"  Max counter value: {maxValue}");
Console.WriteLine($"  Average counter value: {avgValue:F2}");
Console.WriteLine();

// Example 3: Mathematical Utilities
Console.WriteLine("📍 Example 3: Mathematical Utilities");
Console.WriteLine("------------------------------------");

Console.WriteLine("Computing optimal parameters for different scenarios:");

var scenarios = new[]
{
    (Capacity: 1000, FPR: 0.01),
    (Capacity: 10000, FPR: 0.001),
    (Capacity: 100000, FPR: 0.01)
};

foreach (var (capacity, fpr) in scenarios)
{
    var (bitArraySize, hashFunctions, memoryUsage) = BloomFilterMath.ComputeOptimalParameters(capacity, fpr);
    var bitsPerElement = (double)bitArraySize / capacity;
    var useStack = BloomFilterMath.ShouldUseStackAllocation(bitArraySize);
    
    Console.WriteLine($"\n  📋 Scenario: {capacity:N0} items, {fpr:P1} FPR");
    Console.WriteLine($"     Bit array size: {bitArraySize:N0} bits ({bitsPerElement:F1} bits/element)");
    Console.WriteLine($"     Hash functions: {hashFunctions}");
    Console.WriteLine($"     Memory usage: {memoryUsage:N0} bytes ({memoryUsage / 1024.0:F1} KB)");
    Console.WriteLine($"     Allocation: {(useStack ? "Stack" : "Heap")}");
}

Console.WriteLine();

// Example 4: Performance Characteristics
Console.WriteLine("📍 Example 4: Performance Test");
Console.WriteLine("------------------------------");

const int perfTestItems = 1000;
var perfBuffer = new byte[5000];
var perfFilter = BloomFilter.Create(perfTestItems, 0.01, perfBuffer);

// Time the add operations
var stopwatch = System.Diagnostics.Stopwatch.StartNew();

for (int i = 0; i < perfTestItems; i++)
{
    perfFilter.Add($"item_{i}");
}

stopwatch.Stop();
var addTime = stopwatch.Elapsed.TotalMilliseconds;

// Time the lookup operations
stopwatch.Restart();

int foundCount = 0;
for (int i = 0; i < perfTestItems; i++)
{
    if (perfFilter.MightContain($"item_{i}"))
    {
        foundCount++;
    }
}

stopwatch.Stop();
var lookupTime = stopwatch.Elapsed.TotalMilliseconds;

Console.WriteLine($"Performance results for {perfTestItems} items:");
Console.WriteLine($"  Add operations: {addTime:F2} ms ({addTime / perfTestItems:F4} ms per item)");
Console.WriteLine($"  Lookup operations: {lookupTime:F2} ms ({lookupTime / perfTestItems:F4} ms per item)");
Console.WriteLine($"  Items found: {foundCount} / {perfTestItems}");
Console.WriteLine($"  Final estimated count: {perfFilter.EstimateItemCount()}");

Console.WriteLine();
Console.WriteLine("🎉 Examples completed successfully!");
Console.WriteLine("📖 Check the README.md for more detailed documentation.");