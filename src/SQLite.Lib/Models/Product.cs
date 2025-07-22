// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace SQLite.Lib.Models
{
    using SQLite.Lib.Entities;

    internal class Product : BaseEntity<long>
    {
        public string Name { get; set; }

        public decimal Price { get; set; }
    }
}
