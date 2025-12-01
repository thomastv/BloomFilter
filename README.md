# Unique.BloomFilter

A high-performance .NET library implementing Bloom filters with modern C# features and memory optimizations.

## Overview

This library provides efficient probabilistic data structures for fast set membership testing with configurable false positive rates. Built for .NET 9 with Span<T> optimizations and modern C# language features.

## Features

- **High Performance**: Span<T>-based implementation with smart memory allocation
- **Modern C#**: Built with .NET 9 and latest C# features
- **Flexible**: Configurable capacity and error rates
- **Memory Efficient**: Stack allocation for small filters, heap for larger ones
- **Thread Safe**: Concurrent variants available
- **Multiple Variants**: Basic and counting Bloom filters

## Quick Start

```csharp
using Unique.BloomFilter;

// Create a Bloom filter for 1000 items with 1% false positive rate
var buffer = new byte[2000];
var filter = BloomFilter.Create(capacity: 1000, falsePositiveRate: 0.01, buffer);

// Add items
filter.Add("hello");
filter.Add("world");
filter.Add(42);

// Check membership
bool mightContain = filter.MightContain("hello"); // true
bool definitelyNotContains = filter.MightContain("unknown"); // likely false
```

## Installation

```bash
dotnet add package Unique.BloomFilter
```

## Documentation

### Basic Bloom Filter

The `BloomFilter` provides the core functionality:

- `Add<T>(T item)` - Add an item to the filter
- `MightContain<T>(T item)` - Check if an item might be in the set
- `Clear()` - Reset the filter to empty state

### Counting Bloom Filter

The `CountingBloomFilter` supports removal operations:

- `Add<T>(T item)` - Add an item
- `Remove<T>(T item)` - Remove an item (if it was added)
- `MightContain<T>(T item)` - Check membership

### Performance Characteristics

- **Time Complexity**: O(k) for all operations where k is the number of hash functions
- **Space Complexity**: ~9.6 bits per element for 1% false positive rate
- **Memory Allocation**: Stack allocation for filters ≤ 1KB, heap allocation for larger filters

## Advanced Usage

### Custom Configuration

```csharp
// Manually configure parameters
int bitArraySize = 4096;
int hashFunctionCount = 7;
var buffer = new byte[512];
var filter = BloomFilter.Create(bitArraySize, hashFunctionCount, buffer);
```

### Thread-Safe Operations

```csharp
// Thread-safe operations coming in future release
// Current implementation provides thread-safe reads when using proper synchronization
var buffer = new byte[2000];
var filter = BloomFilter.Create(1000, 0.01, buffer);
// Use external synchronization for writes
```

## Mathematical Background

This implementation uses the standard Bloom filter mathematics:

- **Optimal bit array size**: `m = -n * ln(ε) / (ln(2))²`
- **Optimal hash functions**: `k = (m/n) * ln(2)`  
- **False positive probability**: `ε ≈ (1 - e^(-kn/m))^k`

Where:
- `n` = expected number of elements
- `m` = bit array size  
- `k` = number of hash functions
- `ε` = desired false positive rate

## Implementation Details

### Hash Function Strategy

Uses System.HashCode with double hashing pattern:
- Generates two independent hash values from the input
- Derives k hash functions using linear combination
- Ensures uniform distribution across the bit array

### Memory Optimization

- **Small filters** (≤ 1KB): Stack-allocated using `stackalloc`
- **Large filters** (> 1KB): Heap-allocated arrays
- **Bit operations**: Optimized using 64-bit words when possible

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## TODO

- [ ] JSON serialization support with custom JsonConverter
- [ ] Base64 encoding for compact representation  
- [ ] Schema versioning for backward compatibility
- [ ] Compressed representations using BitArray optimization
- [ ] Scalable Bloom filter with dynamic growth
- [ ] Advanced benchmarking and performance analysis

## Acknowledgments

Based on the seminal work by Burton Howard Bloom (1970) and optimized for modern .NET 9 performance characteristics.