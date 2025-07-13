using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

public class BenchmarkEntity
{
    public long Id { get; set; }
    public string Name { get; set; }
    public int Value { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public double Score { get; set; }
    public string Tags { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

class Program
{
    static void Main()
    {
        // Create a sample entity similar to benchmark data
        var entity = new BenchmarkEntity
        {
            Id = 1000,
            Name = "Entity 1000",
            Value = 1000,
            Description = "Description for entity 1000 with some additional text to make it more realistic",
            IsActive = true,
            Score = 1500.0,
            Tags = string.Join(",", Enumerable.Range(1, 5).Select(t => $"tag{t}")),
            CreatedAt = DateTime.UtcNow
        };

        Console.WriteLine("=== BenchmarkEntity Size Analysis ===\n");

        // Calculate individual field sizes
        Console.WriteLine("Field sizes:");
        Console.WriteLine($"  Id (long): 8 bytes");
        Console.WriteLine($"  Name string length: {entity.Name.Length} chars = ~{entity.Name.Length * 2} bytes");
        Console.WriteLine($"  Value (int): 4 bytes");
        Console.WriteLine($"  Description string length: {entity.Description.Length} chars = ~{entity.Description.Length * 2} bytes");
        Console.WriteLine($"  IsActive (bool): 1 byte");
        Console.WriteLine($"  Score (double): 8 bytes");
        Console.WriteLine($"  Tags string length: {entity.Tags.Length} chars = ~{entity.Tags.Length * 2} bytes");
        Console.WriteLine($"  CreatedAt (DateTime): 8 bytes");

        // Calculate total in-memory size (approximate)
        int baseSize = 8 + 4 + 1 + 8 + 8; // primitive types
        int stringSize = (entity.Name.Length + entity.Description.Length + entity.Tags.Length) * 2;
        int objectOverhead = 24; // .NET object header (approximate)
        int referenceSize = 8 * 3; // 3 string references on 64-bit
        
        int totalMemorySize = objectOverhead + baseSize + stringSize + referenceSize;
        
        Console.WriteLine($"\nApproximate in-memory size: {totalMemorySize} bytes");

        // Calculate JSON serialized size (what's actually stored in SQLite)
        string json = JsonConvert.SerializeObject(entity);
        int jsonSize = Encoding.UTF8.GetByteCount(json);
        
        Console.WriteLine($"\nJSON serialized:");
        Console.WriteLine($"  JSON string: {json}");
        Console.WriteLine($"  JSON size: {jsonSize} bytes");

        // Show size for different entity numbers
        Console.WriteLine("\nJSON size variations by entity number:");
        for (int i = 1; i <= 10000; i *= 10)
        {
            var testEntity = new BenchmarkEntity
            {
                Id = i,
                Name = $"Entity {i}",
                Value = i,
                Description = $"Description for entity {i} with some additional text to make it more realistic",
                IsActive = i % 2 == 0,
                Score = i * 1.5,
                Tags = string.Join(",", Enumerable.Range(1, 5).Select(t => $"tag{t}")),
                CreatedAt = DateTime.UtcNow
            };
            
            string testJson = JsonConvert.SerializeObject(testEntity);
            Console.WriteLine($"  Entity #{i}: {Encoding.UTF8.GetByteCount(testJson)} bytes");
        }
    }
}