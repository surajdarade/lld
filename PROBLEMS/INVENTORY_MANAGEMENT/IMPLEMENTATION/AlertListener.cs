public interface AlertListener
{
    void OnLowStock(string warehouseId, string productId, int remainingQuantity);
}
