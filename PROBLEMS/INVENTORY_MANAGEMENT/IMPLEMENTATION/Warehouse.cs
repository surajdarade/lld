using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Warehouse
{
    private readonly object _inventoryLock = new();
    private readonly Dictionary<string, InventoryItem> _inventory = new(StringComparer.Ordinal);

    public string Id { get; }

    public Warehouse(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Warehouse ID cannot be empty.", nameof(id));
        Id = id;
    }

    public InventoryItem? GetInventoryItem(string productId)
    {
        ValidateProductId(productId);
        lock (_inventoryLock)
            return _inventory.TryGetValue(productId, out InventoryItem? item) ? item : null;
    }

    public InventoryItem GetOrCreateInventoryItem(string productId)
    {
        ValidateProductId(productId);
        lock (_inventoryLock)
        {
            if (_inventory.TryGetValue(productId, out InventoryItem? item))
                return item;
            item = new InventoryItem(productId);
            _inventory.Add(productId, item);
            return item;
        }
    }

    public int GetAvailableQuantity(string productId) => GetInventoryItem(productId)?.GetQuantity() ?? 0;

    public List<InventoryItem> GetAllInventoryItems()
    {
        lock (_inventoryLock) return _inventory.Values.ToList();
    }

    private static void ValidateProductId(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
    }
}
