using System;
using System.Collections.Generic;
using System.Linq;

public sealed class InventoryManager
{
    private readonly object _warehousesLock = new();
    private readonly object _listenersLock = new();
    private readonly Dictionary<string, Warehouse> _warehouses = new(StringComparer.Ordinal);
    private readonly List<AlertListener> _listeners = new();
    private readonly AlertConfig _alertConfig;

    public InventoryManager(AlertConfig alertConfig) =>
        _alertConfig = alertConfig ?? throw new ArgumentNullException(nameof(alertConfig));

    public void AddWarehouse(Warehouse warehouse)
    {
        ArgumentNullException.ThrowIfNull(warehouse);
        lock (_warehousesLock)
        {
            if (!_warehouses.TryAdd(warehouse.Id, warehouse))
                throw new InvalidOperationException($"Warehouse '{warehouse.Id}' already exists.");
        }
    }

    public void AddStock(string warehouseId, string productId, int quantity)
    {
        ValidateIds(warehouseId, productId);
        ValidateQuantity(quantity);
        InventoryItem item = GetWarehouse(warehouseId).GetOrCreateInventoryItem(productId);
        (int previous, int current) = item.AddStockAndGetChange(quantity);
        NotifyIfNeeded(warehouseId, productId, previous, current);
    }

    public void RemoveStock(string warehouseId, string productId, int quantity)
    {
        ValidateIds(warehouseId, productId);
        ValidateQuantity(quantity);
        InventoryItem item = GetWarehouse(warehouseId).GetInventoryItem(productId)
            ?? throw new InvalidOperationException($"No inventory for product '{productId}' in warehouse '{warehouseId}'.");
        (int previous, int current) = item.RemoveStockAndGetChange(quantity);
        NotifyIfNeeded(warehouseId, productId, previous, current);
    }

    public List<Warehouse> CheckAvailability(string productId, int quantity)
    {
        ValidateProductId(productId);
        ValidateQuantity(quantity);
        Warehouse[] snapshot;
        lock (_warehousesLock) snapshot = _warehouses.Values.ToArray();
        return snapshot.Where(w => w.GetAvailableQuantity(productId) >= quantity).ToList();
    }

    public void TransferStock(string sourceWarehouseId, string destinationWarehouseId, string productId, int quantity)
    {
        ValidateWarehouseId(sourceWarehouseId);
        ValidateWarehouseId(destinationWarehouseId);
        ValidateProductId(productId);
        ValidateQuantity(quantity);
        if (StringComparer.Ordinal.Equals(sourceWarehouseId, destinationWarehouseId))
            throw new ArgumentException("Source and destination warehouses must differ.");

        Warehouse source = GetWarehouse(sourceWarehouseId);
        Warehouse destination = GetWarehouse(destinationWarehouseId);
        InventoryItem sourceItem = source.GetInventoryItem(productId)
            ?? throw new InvalidOperationException($"No source inventory for product '{productId}'.");
        InventoryItem destinationItem = destination.GetOrCreateInventoryItem(productId);
        var result = InventoryItem.Transfer(sourceItem, destinationItem, quantity);
        NotifyIfNeeded(sourceWarehouseId, productId, result.SourceBefore, result.SourceAfter);
        NotifyIfNeeded(destinationWarehouseId, productId, result.DestinationBefore, result.DestinationAfter);
    }

    public void RegisterAlertListener(AlertListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_listenersLock)
            if (!_listeners.Contains(listener)) _listeners.Add(listener);
    }

    public void UnregisterAlertListener(AlertListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_listenersLock) _listeners.Remove(listener);
    }

    private Warehouse GetWarehouse(string warehouseId)
    {
        lock (_warehousesLock)
            return _warehouses.TryGetValue(warehouseId, out Warehouse? warehouse)
                ? warehouse
                : throw new KeyNotFoundException($"Warehouse '{warehouseId}' was not found.");
    }

    private void NotifyIfNeeded(string warehouseId, string productId, int previous, int current)
    {
        if (!_alertConfig.ShouldNotify(previous, current)) return;
        AlertListener[] snapshot;
        lock (_listenersLock) snapshot = _listeners.ToArray();
        foreach (AlertListener listener in snapshot)
        {
            try { listener.OnLowStock(warehouseId, productId, current); }
            catch { /* Notification failure does not undo committed inventory. */ }
        }
    }

    private static void ValidateIds(string warehouseId, string productId)
    {
        ValidateWarehouseId(warehouseId);
        ValidateProductId(productId);
    }

    private static void ValidateWarehouseId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Warehouse ID cannot be empty.");
    }

    private static void ValidateProductId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Product ID cannot be empty.");
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
    }
}
