# RESOURCE(S)


https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/inventory-management


# PROBLEM STATEMENT


Design an inventory management system that tracks products across multiple warehouses. The system needs to handle adding and removing
inventory, transferring stock between locations and alerting when inventory runs low.

**Primary Capabilities:**

- Track product quantities independently across multiple warehouses.
- Receive stock into a specified warehouse.
- Remove stock from a specified warehouse.
- Find warehouses that can fulfill a requested quantity of a product.
- Transfer stock between warehouses.
- Notify registered listeners when inventory reaches a configured low-stock threshold.

**Error/Edge Case Handling:**

- Reject stock removal when the requested quantity exceeds available inventory.
- Reject transfers when the source warehouse lacks sufficient stock.
- Reject invalid quantities, including zero or negative quantities.
- Handle unknown warehouses and invalid product identifiers.
- Prevent concurrent operations from causing negative inventory or inconsistent transfers.
- Handle low-stock alert thresholds and avoid unintended duplicate alerts.

**Scope Boundaries:**

- Product catalog management (products exist externally).
- Order processing, payment, and serviceability.
- Persistence.
- Distributed coordination across multiple application instances.


# STEP 1: REQUIREMENTS


1. Track inventory for products across multiple warehouses

2. Add stock to a specific warehouse (receiving shipments)

3. Remove stock from a specific warehouse (fulfilling orders)

4. Check availability: given a product and quantity, return which warehouses can fulfill

5. Transfer stock between warehouses

6. Low-stock alerts

7. Reject operations that would result in negative inventory

8. System must be thread-safe to handle concurent operations

**Out of scope:**

- Product catalog management (products exists externally)
- Order processing / payment / serviceability
- Persistence


# STEP 2: ENTITIES


- InventoryManager
- Warehouse
- AlertConfig
- AlertListener
- InventoryItem

**Unnecessary to Current Set of Requirements**

- InventoryTransfer
- LowStockAlert
- InventoryKey


# STEP 3: CLASS DESIGN


```text
class InventoryManager:
    - warehouses: Dictionary<string, Warehouse>
    - alertConfig: AlertConfig
    - listeners: List<AlertListener>

    + AddWarehouse(warehouse: Warehouse) -> void
    + AddStock(warehouseId: string, productId: string, quantity: int) -> void
    + RemoveStock(warehouseId: string, productId: string, quantity: int) -> void
    + CheckAvailability(productId: string, quantity: int) -> List<Warehouse>
    + TransferStock(sourceWarehouseId: string, destinationWarehouseId: string, productId: string, quantity: int) -> void
    + RegisterAlertListener(listener: AlertListener) -> void
    + UnregisterAlertListener(listener: AlertListener) -> void
```


```text
class Warehouse:
    - id: string
    - inventory: Dictionary<string, InventoryItem>

    + Warehouse(id: string)
    + GetInventoryItem(productId: string) -> InventoryItem?
    + GetOrCreateInventoryItem(productId: string) -> InventoryItem
    + GetAvailableQuantity(productId: string) -> int
    + GetAllInventoryItems() -> List<InventoryItem>
```


```text
class InventoryItem:
    - productId: string
    - quantity: int
    - lock: object

    + InventoryItem(productId: string)
    + GetQuantity() -> int
    + AddStock(quantity: int) -> void
    + RemoveStock(quantity: int) -> void
    + CanFulfill(quantity: int) -> boolean
```


```text
class AlertConfig:
    - threshold: int

    + AlertConfig(threshold: int)
    + IsLowStock(quantity: int) -> boolean
    + ShouldNotify(previousQuantity: int, currentQuantity: int) -> boolean
```


```text
interface AlertListener:
    + OnLowStock(warehouseId: string, productId: string, remainingQuantity: int) -> void
```


# STEP 4: IMPLEMENTATION


```text
class InventoryManager:
    public void AddWarehouse(Warehouse warehouse):
        """
        Core Logic:
        1. Validate warehouse is not null.
        2. Acquire the warehouse collection lock.
        3. Reject duplicate warehouse IDs.
        4. Register the warehouse.
        5. Release the lock.

        Error/Edge Case(s):
        1. Null warehouse -> reject.
        2. Duplicate warehouse ID -> reject.
        3. Concurrent registration -> prevent duplicate entries.
        """

        if (warehouse == null)
            throw ArgumentNullException

        lock (warehousesLock)
            if (warehouses.ContainsKey(warehouse.Id))
                throw InvalidOperationException

            warehouses[warehouse.Id] = warehouse

    public void AddStock(string warehouseId, string productId, int quantity):
        """
        Core Logic:
        1. Validate warehouseId, productId, and quantity.
        2. Locate the requested warehouse.
        3. Get or create the InventoryItem for productId.
        4. Atomically add quantity and capture the previous/current quantities.
        5. Evaluate the resulting quantity against AlertConfig.
        6. Dispatch applicable low-stock notifications outside inventory locks.

        Error/Edge Case(s):
        1. Unknown warehouse -> reject.
        2. Empty product ID -> reject.
        3. Quantity <= 0 -> reject.
        4. Quantity overflow -> reject without modifying stock.
        """

        ValidateIds(warehouseId, productId)
        ValidateQuantity(quantity)

        warehouse = GetWarehouse(warehouseId)
        item = warehouse.GetOrCreateInventoryItem(productId)
        (previousQuantity, currentQuantity) = item.AddStock(quantity)

        if (alertConfig.ShouldNotify(previousQuantity, currentQuantity))
            NotifyListeners(warehouseId, productId, currentQuantity)

    public void RemoveStock(string warehouseId, string productId, int quantity):
        """
        Core Logic:
        1. Validate warehouseId, productId, and quantity.
        2. Locate the requested warehouse and inventory item.
        3. Atomically verify sufficient stock, remove quantity, and capture previous/current quantities.
        4. Evaluate the resulting quantity against AlertConfig.
        5. Dispatch applicable low-stock notifications outside inventory locks.

        Error/Edge Case(s):
        1. Unknown warehouse -> reject.
        2. Missing inventory item -> reject.
        3. Quantity <= 0 -> reject.
        4. Insufficient stock -> reject without modifying stock.
        """

        ValidateIds(warehouseId, productId)
        ValidateQuantity(quantity)

        warehouse = GetWarehouse(warehouseId)
        item = warehouse.GetInventoryItem(productId)
        if (item == null)
            throw InvalidOperationException

        (previousQuantity, currentQuantity) = item.RemoveStock(quantity)

        if (alertConfig.ShouldNotify(previousQuantity, currentQuantity))
            NotifyListeners(warehouseId, productId, currentQuantity)

    public List<Warehouse> CheckAvailability(string productId, int quantity):
        """
        Core Logic:
        1. Validate productId and quantity.
        2. Take a snapshot of the registered warehouses.
        3. Read each warehouse's available quantity consistently.
        4. Include warehouses whose stock >= quantity.
        5. Return the matching warehouses.

        Error/Edge Case(s):
        1. Empty product ID -> reject.
        2. Quantity <= 0 -> reject.
        3. No matching warehouses -> return an empty list.
        4. Stock may change after the availability check; this operation does not reserve stock.
        """

        ValidateProductId(productId)
        ValidateQuantity(quantity)

        lock (warehousesLock)
            warehouseSnapshot = warehouses.Values.ToList()

        result = new List<Warehouse>()
        for each warehouse in warehouseSnapshot
            if (warehouse.GetAvailableQuantity(productId) >= quantity)
                result.Add(warehouse)

        return result

    public void TransferStock(string sourceWarehouseId, string destinationWarehouseId, string productId, int quantity):
        """
        Core Logic:
        1. Validate IDs and quantity; reject identical source/destination warehouses.
        2. Locate both warehouses and their inventory items.
        3. Acquire both item locks in a stable, unique order.
        4. Verify sufficient source stock and destination capacity before modifying either quantity.
        5. Deduct from source and add to destination while both locks are held.
        6. Release both locks.
        7. Evaluate low-stock threshold crossings and notify listeners outside the locks.

        Error/Edge Case(s):
        1. Unknown warehouse -> reject.
        2. Same source and destination -> reject.
        3. Quantity <= 0 -> reject.
        4. Missing source inventory or insufficient stock -> reject without changing either quantity.
        5. Destination quantity overflow -> reject without changing either quantity.
        6. Concurrent transfers -> stable lock ordering prevents deadlocks; both updates are atomic.
        """

        ValidateIds(sourceWarehouseId, destinationWarehouseId, productId)
        ValidateQuantity(quantity)
        if (sourceWarehouseId == destinationWarehouseId)
            throw ArgumentException

        sourceWarehouse = GetWarehouse(sourceWarehouseId)
        destinationWarehouse = GetWarehouse(destinationWarehouseId)
        sourceItem = sourceWarehouse.GetInventoryItem(productId)
        if (sourceItem == null)
            throw InvalidOperationException

        destinationItem = destinationWarehouse.GetOrCreateInventoryItem(productId)

        sourcePrevious = 0
        sourceCurrent = 0
        destinationPrevious = 0
        destinationCurrent = 0

        InventoryItem.WithLocks(sourceItem, destinationItem, () =>
            sourcePrevious = sourceItem.GetQuantityUnsafe()
            destinationPrevious = destinationItem.GetQuantityUnsafe()

            if (sourcePrevious < quantity)
                throw InvalidOperationException

            if (destinationPrevious > INT_MAX - quantity)
                throw OverflowException

            sourceCurrent = sourcePrevious - quantity
            destinationCurrent = destinationPrevious + quantity

            sourceItem.SetQuantityUnsafe(sourceCurrent)
            destinationItem.SetQuantityUnsafe(destinationCurrent)
        )

        if (alertConfig.ShouldNotify(sourcePrevious, sourceCurrent))
            NotifyListeners(sourceWarehouseId, productId, sourceCurrent)

        if (alertConfig.ShouldNotify(destinationPrevious, destinationCurrent))
            NotifyListeners(destinationWarehouseId, productId, destinationCurrent)

    public void RegisterAlertListener(AlertListener listener):
        """
        Core Logic:
        1. Validate listener.
        2. Acquire the listener collection lock.
        3. Add the listener only if it is not already registered.
        4. Release the lock.

        Error/Edge Case(s):
        1. Null listener -> reject.
        2. Duplicate registration -> do not add duplicate entries.
        3. Concurrent registration -> protect the listener collection.
        """

        if (listener == null)
            throw ArgumentNullException

        lock (listenersLock)
            if (!listeners.Contains(listener))
                listeners.Add(listener)

    public void UnregisterAlertListener(AlertListener listener):
        """
        Core Logic:
        1. Validate listener.
        2. Acquire the listener collection lock.
        3. Remove the listener if registered.
        4. Release the lock.

        Error/Edge Case(s):
        1. Null listener -> reject.
        2. Listener not registered -> no-op.
        3. Concurrent modification -> protect the listener collection.
        """

        if (listener == null)
            throw ArgumentNullException

        lock (listenersLock)
            listeners.Remove(listener)

    private void NotifyListeners(string warehouseId, string productId, int remainingQuantity):
        listenersSnapshot = SnapshotListeners()

        for each listener in listenersSnapshot
            try
                listener.OnLowStock(warehouseId, productId, remainingQuantity)
            catch error
                // A listener failure does not roll back stock or block other listeners.
```


```text
class Warehouse:
    public InventoryItem? GetInventoryItem(string productId):
        """
        Core Logic:
        1. Validate productId.
        2. Acquire the inventory map lock.
        3. Return the matching item or null.
        4. Release the lock.

        Error/Edge Case(s):
        1. Empty product ID -> reject.
        2. Item does not exist -> return null.
        3. Concurrent lookup and creation -> synchronize access to the inventory map.
        """

        ValidateProductId(productId)

        lock (inventoryLock)
            if (inventory.ContainsKey(productId))
                return inventory[productId]

        return null

    public InventoryItem GetOrCreateInventoryItem(string productId):
        """
        Core Logic:
        1. Validate productId.
        2. Acquire the inventory map lock.
        3. Return the existing item or create and register a new InventoryItem.
        4. Release the lock.

        Error/Edge Case(s):
        1. Empty product ID -> reject.
        2. Concurrent creation -> only one item per product ID.
        """

        ValidateProductId(productId)

        lock (inventoryLock)
            if (inventory.ContainsKey(productId))
                return inventory[productId]

            item = new InventoryItem(productId)
            inventory[productId] = item
            return item

    public int GetAvailableQuantity(string productId):
        """
        Core Logic:
        1. Look up the inventory item.
        2. Return its current thread-safe quantity.
        3. If the item does not exist, return 0.

        Error/Edge Case(s):
        1. Empty product ID -> reject.
        2. Concurrent stock changes -> quantity reads must be safe.
        """

        item = GetInventoryItem(productId)
        if (item == null)
            return 0

        return item.GetQuantity()

    public List<InventoryItem> GetAllInventoryItems():
        """
        Core Logic:
        1. Acquire the inventory map lock.
        2. Create a snapshot of the current inventory items.
        3. Release the lock.
        4. Return the snapshot.

        Error/Edge Case(s):
        1. Concurrent additions -> snapshot must not expose a mutable internal collection.
        """

        lock (inventoryLock)
            return inventory.Values.ToList()
```


```text
class InventoryItem:
    public int GetQuantity():
        lock (sync)
            return quantity

    public (int previousQuantity, int currentQuantity) AddStock(int quantityToAdd):
        """
        Core Logic:
        1. Reject quantityToAdd <= 0.
        2. Acquire the inventory item's lock.
        3. Check for integer overflow before mutation.
        4. Capture previous quantity, add stock, and capture current quantity.
        5. Release the lock and return both quantities.

        Error/Edge Case(s):
        1. Quantity <= 0 -> reject.
        2. Integer overflow -> reject without changing quantity.
        3. Concurrent additions -> serialize mutations.
        """

        if (quantityToAdd <= 0)
            throw ArgumentOutOfRangeException

        lock (sync)
            previousQuantity = quantity
            if (quantity > INT_MAX - quantityToAdd)
                throw OverflowException

            quantity = quantity + quantityToAdd
            currentQuantity = quantity

        return (previousQuantity, currentQuantity)

    public (int previousQuantity, int currentQuantity) RemoveStock(int quantityToRemove):
        """
        Core Logic:
        1. Reject quantityToRemove <= 0.
        2. Acquire the inventory item's lock.
        3. Verify sufficient stock before mutation.
        4. Capture previous quantity, remove stock, and capture current quantity.
        5. Release the lock and return both quantities.

        Error/Edge Case(s):
        1. Quantity <= 0 -> reject.
        2. Insufficient stock -> reject without modifying quantity.
        3. Concurrent removals -> serialize validation and mutation.
        """

        if (quantityToRemove <= 0)
            throw ArgumentOutOfRangeException

        lock (sync)
            if (quantity < quantityToRemove)
                throw InvalidOperationException

            previousQuantity = quantity
            quantity = quantity - quantityToRemove
            currentQuantity = quantity

        return (previousQuantity, currentQuantity)

    public boolean CanFulfill(int requestedQuantity):
        """
        Core Logic:
        1. Validate requested quantity.
        2. Read quantity safely.
        3. Return true if quantity >= requested quantity; otherwise false.

        Error/Edge Case(s):
        1. Quantity <= 0 -> reject.
        2. Concurrent stock changes -> read a consistent quantity.
        """

        if (requestedQuantity <= 0)
            throw ArgumentOutOfRangeException

        lock (sync)
            return quantity >= requestedQuantity

    private static void WithLocks(InventoryItem first, InventoryItem second, Action action):
        // Each item has a unique LockOrderId assigned at construction.
        if (first == second)
            lock (first.sync)
                action()
            return

        lower = first.LockOrderId < second.LockOrderId ? first : second
        upper = (lower == first) ? second : first

        lock (lower.sync)
        lock (upper.sync)
            action()

    private int GetQuantityUnsafe():
        // Caller must already hold this item's lock.
        return quantity

    private void SetQuantityUnsafe(int newQuantity):
        // Caller must already hold this item's lock; caller validates invariants first.
        quantity = newQuantity
```


```text
class AlertConfig:
    public boolean IsLowStock(int quantity):
        """
        Core Logic:
        1. Compare quantity with Threshold.
        2. Return true when quantity <= Threshold.
        3. Otherwise return false.

        Error/Edge Case(s):
        1. Negative threshold -> reject during configuration construction.
        2. Negative inventory quantity -> prevented by InventoryItem.
        """

        return quantity <= threshold

    public boolean ShouldNotify(int previousQuantity, int currentQuantity):
        """
        Core Logic:
        1. Check whether previousQuantity > Threshold.
        2. Check whether currentQuantity <= Threshold.
        3. Return true only when both conditions hold.

        Error/Edge Case(s):
        1. Repeated mutations below threshold -> no duplicate crossing alert.
        2. The quantity transition must be captured atomically with the stock mutation.
        """

        return previousQuantity > threshold && currentQuantity <= threshold
```


```text
interface AlertListener:
    public void OnLowStock(string warehouseId, string productId, int remainingQuantity):
        // Implement notification behavior in the concrete listener.
        // Listener exceptions are caught by InventoryManager so one failure does not block others.
```


# STEP 5: EXTENSIBILITY


**1. How do you prevent overselling when orders are in progress?**

- I'd introduce an inventory reservation mechanism.
- Each `InventoryItem` would track `onHandQuantity` and `reservedQuantity`. Available stock would be `onHandQuantity - reservedQuantity`.
- Reserving stock must be atomic and thread-safe. If sufficient stock is unavailable, the reservation fails.
- When an order is confirmed, reserved stock is deducted from on-hand inventory. If the order is cancelled or expires, the reservation is released.
- This requires additional reservation state and lifecycle management, which are outside the current requirements.

```text
class InventoryItem:
    - onHandQuantity: int
    - reservedQuantity: int

    + TryReserveStock(quantity: int) -> boolean
    + ConfirmReservation(quantity: int) -> void
    + ReleaseReservation(quantity: int) -> void
    + GetAvailableQuantity() -> int
```


**2. How would you handle inventory that's being shipped between warehouses?**

- I'd introduce an `InventoryTransfer` entity to track the transfer lifecycle.
- When stock is dispatched, it is deducted from the source warehouse and marked as in transit.
- When the shipment arrives, the received quantity is added to the destination warehouse.
- In-transit stock must not be counted as available at either warehouse.
- I'd use transfer states and idempotent operations to prevent duplicate dispatch or receipt events from changing inventory more than once.

```text
enum TransferStatus:
    PENDING
    IN_TRANSIT
    COMPLETED
    FAILED
    CANCELLED
```

```text
class InventoryTransfer:
    - transferId: string
    - productId: string
    - sourceWarehouseId: string
    - destinationWarehouseId: string
    - quantity: int
    - status: TransferStatus

    + Dispatch() -> void
    + Complete() -> void
    + Cancel() -> void
```


**3. How would you prevent duplicate low-stock alerts?**

- I'd trigger alerts when stock crosses from above the threshold to at or below it, rather than whenever stock changes while already below the threshold.
- The before-and-after quantities must be evaluated consistently with the inventory mutation.
- If stock rises above the threshold and later crosses below it again, a new alert can be sent.
- Listener failures should not roll back a successful inventory operation.

```text
class AlertConfig:
    - threshold: int

    + IsLowStock(quantity: int) -> boolean
    + ShouldNotify(previousQuantity: int, currentQuantity: int) -> boolean
```


**4. How would you support different low-stock thresholds across warehouses?**

- I'd configure the threshold for each warehouse-product combination.
- The same product may have different thresholds depending on warehouse demand and replenishment requirements.
- `InventoryManager` would resolve the appropriate `AlertConfig` using `warehouseId` and `productId`.
- The existing `AlertConfig` entity can be reused; only configuration ownership and lookup need to change.

```text
class AlertConfig:
    - warehouseId: string
    - productId: string
    - threshold: int

    + IsLowStock(quantity: int) -> boolean
```


**5. How would you make inventory transfers reliable if the system becomes distributed?**

- The current in-memory locks protect operations only within a single process.
- For a distributed system, I'd use a durable inventory store with transactions and concurrency control.
- I'd record transfer state durably and use reliable messaging, such as an outbox, to communicate transfer events.
- Destination receipt must be idempotent, so processing the same event twice cannot add stock twice.
- For transfers spanning separate transactional boundaries, I'd use a durable workflow or saga with reconciliation and compensating actions where needed.

```text
class InventoryTransfer:
    - transferId: string
    - status: TransferStatus

    + Dispatch() -> void
    + Complete() -> void
    + Fail() -> void
    + Reconcile() -> void
```