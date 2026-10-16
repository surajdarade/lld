using System;
using System.Threading;

public sealed class InventoryItem
{
    private static long _nextLockOrder;
    private readonly object _sync = new();
    private readonly long _lockOrder = Interlocked.Increment(ref _nextLockOrder);
    private int _quantity;

    public string ProductId { get; }

    public InventoryItem(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        ProductId = productId;
    }

    public int GetQuantity()
    {
        lock (_sync) return _quantity;
    }

    public void AddStock(int quantity)
    {
        ValidateQuantity(quantity);
        lock (_sync)
        {
            if (_quantity > int.MaxValue - quantity)
                throw new OverflowException("Inventory quantity would overflow.");
            _quantity += quantity;
        }
    }

    public void RemoveStock(int quantity)
    {
        ValidateQuantity(quantity);
        lock (_sync)
        {
            if (_quantity < quantity)
                throw new InvalidOperationException("Insufficient inventory.");
            _quantity -= quantity;
        }
    }

    public bool CanFulfill(int quantity)
    {
        ValidateQuantity(quantity);
        lock (_sync) return _quantity >= quantity;
    }

    public (int Previous, int Current) AddStockAndGetChange(int quantity)
    {
        ValidateQuantity(quantity);
        lock (_sync)
        {
            if (_quantity > int.MaxValue - quantity)
                throw new OverflowException("Inventory quantity would overflow.");
            int previous = _quantity;
            _quantity += quantity;
            return (previous, _quantity);
        }
    }

    public (int Previous, int Current) RemoveStockAndGetChange(int quantity)
    {
        ValidateQuantity(quantity);
        lock (_sync)
        {
            if (_quantity < quantity)
                throw new InvalidOperationException("Insufficient inventory.");
            int previous = _quantity;
            _quantity -= quantity;
            return (previous, _quantity);
        }
    }

    public static (int SourceBefore, int SourceAfter, int DestinationBefore, int DestinationAfter) Transfer(
        InventoryItem source, InventoryItem destination, int quantity)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ValidateQuantity(quantity);
        if (ReferenceEquals(source, destination))
            throw new ArgumentException("Source and destination inventory items must differ.");

        InventoryItem first = source._lockOrder < destination._lockOrder ? source : destination;
        InventoryItem second = ReferenceEquals(first, source) ? destination : source;
        lock (first._sync)
        lock (second._sync)
        {
            if (source._quantity < quantity)
                throw new InvalidOperationException("Insufficient source inventory.");
            if (destination._quantity > int.MaxValue - quantity)
                throw new OverflowException("Destination inventory quantity would overflow.");

            int sourceBefore = source._quantity;
            int destinationBefore = destination._quantity;
            source._quantity -= quantity;
            destination._quantity += quantity;
            return (sourceBefore, source._quantity, destinationBefore, destination._quantity);
        }
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
    }
}
