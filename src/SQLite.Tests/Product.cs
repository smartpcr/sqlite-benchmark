using SQLite.Lib.Abstractions;

namespace SQLite.Tests
{
    internal class Product : IEntity
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }
}
