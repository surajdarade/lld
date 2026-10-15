# RESOURCE(S)


[https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/parking-lot](https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/parking-lot)


# PROBLEM STATEMENT


Design an in-memory file system that supports creating files and directories, navigating paths and basic file operations.


**Primary Capabilities:**

- Support Motorcycle, Car and Large Vehicle.
- Automatically assign the first available compatible spot when a vehicle enters.
- Issue a ticket at entry.
- Accept a ticket ID at exit, validate it, calculate the fee based on parking duration and release the assigned spot.
- Apply the same hourly rate to every vehicle type.

**Error/Edge Case Handling:**

- No compatible spot is available.
- Vehicle, spot, or ticket identifiers are invalid.
- A ticket is unknown or has already been used.
- Exit time precedes entry time.
- Concurrent entry and exit operations attempt to modify related state.

**Scope Boundaries:**

- Payment processing, physical gate hardware, security cameras/monitoring, UI/display systems and reservations are out of scope.
- The design supports multi-threaded calls within one process. Persistence and distributed coordination are out of scope.


# STEP 1: REQUIREMENTS


1. The system supports three vehicle types: Motorcycle, Car, and Large Vehicle.

2. When a vehicle enters, the system automatically assigns an available compatible spot.
   - A vehicle can use a spot of the same size or larger.
   - Compatibility: Motorcycle -> Motorcycle, Car, or Large; Car -> Car or Large; Large Vehicle -> Large only.
   - The first available compatible spot in the configured order is selected.

3. The system issues a ticket at entry.
   - The ticket contains a unique ID, vehicle, assigned spot ID, UTC entry time, and lifecycle status.

4. When a vehicle exits, the caller supplies the ticket ID.
   - The system validates that the ticket exists and is active.
   - The fee is based on elapsed time, rounded up to whole hours.
   - A minimum of one billable hour applies, including a zero-duration stay.
   - The assigned spot is released only after the exit has been validated.

5. All vehicle types use the same hourly rate.

6. Entry is rejected when no compatible spot is available. A rejected entry does not create a ticket or change occupancy.

7. Exit is rejected when the ticket ID is invalid or has already been used. A rejected exit does not change ticket or spot state.

8. Exit is rejected if the current time precedes the ticket's entry time.

9. Time is supplied through an injectable clock and normalized to UTC.

10. Concurrent entry and exit calls from multiple threads in the same process must preserve the system invariants.


**Out of Scope:**

- Payment processing or payment collection.
- Physical gates, cameras, monitoring and UI/display systems.
- Reservations or pre-booking.
- Persistence and recovery after process failure.
- Multi-process/distributed coordination.
- Multiple pricing plans, vehicle-specific rates or rate changes over time.


# STEP 2: ENTITIES


- `ParkingLot`
  - Coordinates entry and exit workflows; owns the ordered spot collection, ticket registry, pricing configuration, clock dependency and synchronization boundary.
- `ParkingSpot`
  - Owns a stable spot ID, fixed size, compatibility check and occupancy state.
- `Vehicle`
  - Represents immutable vehicle identity and type.
- `ParkingTicket`
  - Represents one parking session: ticket ID, vehicle, assigned spot, UTC entry time and Active/Completed status.
- `ParkingFeeCalculator`
  - Calculates the fee from entry time, exit time, and hourly rate.
- `IClock`
  - Abstraction for the current timestamp.
- `SystemClock`
  - Production implementation of `IClock` using UTC system time.

**Unnecessary to Current Set of Requirements:**

- `ParkingFloor`: no floor-specific behavior is required.
- `ParkingSession`: the ticket already represents a parking session.
- `Payment` / `PaymentProcessor`: payment processing is out of scope.
- `Reservation`: pre-booking is out of scope.
- `Gate`, `Camera`, `Display`: hardware and UI concerns are out of scope.
- `Customer` / `ParkingAttendant`: no required behavior needs these entities.
- Separate vehicle subclasses: the vehicle type enum is sufficient.

**Relationships and State Ownership:**

- `ParkingLot` contains an ordered list of `ParkingSpot` objects and a dictionary of tickets keyed by ticket ID.
- A `ParkingTicket` references its vehicle and assigned spot internally; callers receive the stable `SpotId`, not the mutable spot object.
- A spot can be associated with multiple historical tickets over time, but at most one active ticket at a time.
- Completed tickets remain in the registry so repeated exits can be identified.
- `ParkingLot` is the sole coordinator of occupancy changes and ticket completion. Those mutations are internal and happen within the lot's lock.


# STEP 3: CLASS DESIGN


```text
class ParkingLot:
    - parkingSpots: List<ParkingSpot>
    - tickets: Dictionary<string, ParkingTicket>
    - clock: IClock
    - feeCalculator: ParkingFeeCalculator
    - hourlyRate: decimal
    - syncLock: object

    + ParkingLot(ParkingSpots, HourlyRate, Clock, FeeCalculator?)
    + EnterVehicle(Vehicle) -> ParkingTicket
    + ExitVehicle(TicketId) -> decimal
    - FindAvailableSpot(VehicleType) -> ParkingSpot?
    - GenerateTicketId() -> string
```


```text
class ParkingSpot:
    - id: string
    - size: SpotSize
    - isOccupied: boolean

    + ParkingSpot(Id, Size)
    + CanFit(VehicleType) -> boolean
    # Occupy(VehicleType) -> void
    # Release() -> void
```


```text
class Vehicle:
    - id: string
    - type: VehicleType

    + Vehicle(Id, Type)
    + Id: string
    + Type: VehicleType
```


```text
class ParkingTicket:
    - id: string
    - vehicle: Vehicle
    - spot: ParkingSpot
    - entryTimeUtc: DateTimeOffset
    - status: TicketStatus

    + Id: string
    + Vehicle: Vehicle
    + SpotId: string
    + EntryTimeUtc: DateTimeOffset
    + Status: TicketStatus
    # MarkCompleted() -> void
```


```text
class ParkingFeeCalculator:
    + CalculateFee(EntryTimeUtc, ExitTimeUtc, HourlyRate) -> decimal
```


```text
interface IClock:
    + UtcNow: DateTimeOffset

class SystemClock implements IClock:
    + UtcNow: DateTimeOffset
```


```text
enum VehicleType:
    MOTORCYCLE
    CAR
    LARGE_VEHICLE

enum SpotSize:
    MOTORCYCLE
    CAR
    LARGE

enum TicketStatus:
    ACTIVE
    COMPLETED
```


**Design Decisions:**

1. `ParkingLot` coordinates entry and exit because each workflow changes multiple related objects.
2. Spot allocation follows configured list order. The requirement says first available compatible spot, not smallest compatible spot.
3. `ParkingTicket` represents the parking session; a separate session class would duplicate state.
4. `ParkingFeeCalculator` isolates fee arithmetic from workflow orchestration.
5. `IClock` makes time injectable without introducing a larger time service.
6. One private lock is the simplest synchronization policy that makes each entry/exit workflow atomic within this process.
7. Completed tickets are retained to distinguish a reused ticket ID from an unknown ticket ID.
8. Occupancy and ticket lifecycle mutation are internal. Callers cannot mutate a spot through a ticket and bypass the synchronization boundary.

**Principles Applied:**

- **Single Responsibility Principle:** `ParkingSpot` owns fit/occupancy rules; `ParkingFeeCalculator` owns fee arithmetic; `ParkingLot` coordinates workflows.
- **Encapsulation:** occupancy and ticket completion methods are internal; the assigned spot reference is not public.
- **Dependency Inversion (limited, purposeful):** `ParkingLot` depends on `IClock`, making time a replaceable dependency.
- **Open/Closed Principle (not over-engineered):** current requirements need one rate and one allocation policy, so no policy hierarchy is introduced prematurely.
- **Invariant preservation:** ticket status and spot occupancy are changed only through the lot's coordinated workflow.
- **YAGNI:** floors, payments, reservations, hardware, and multiple pricing policies are omitted until required.

**Pattern Decision Log:**

| Pattern | Decision | Reason / trigger to reconsider |
|---|---|---|
| Strategy | Not used | Only one allocation policy and one pricing policy exist. Introduce it when alternatives become real requirements. |
| State | Not used as a class hierarchy | Two ticket states are adequately represented by an enum. Consider state objects if transitions gain substantially different behavior. |
| Factory | Not used | Creation is simple and local. Reconsider if creation gains policy or dependencies. |
| Observer | Not used | No notifications or subscribers are required. Reconsider for entry/exit events or downstream consumers. |
| Singleton | Not used | Global lifetime is unnecessary; dependencies are passed to the lot. |
| Decorator | Not used | No fee or spot behavior needs dynamic wrapping. |
| Chain of Responsibility | Not used | There is no configurable sequence of request handlers. |
| Composite | Not used | No hierarchy of lots/floors/spots is required. |
| Adapter | Not used | No incompatible external interface is integrated. |
| Command | Not used | Requests are handled synchronously; queued/replayable operations are not required. |
| Builder | Not used | Constructor configuration is small and stable. |
| Proxy | Not used | No access-control or remote-object boundary is required. |
| Flyweight | Not used | Spot and vehicle object count does not justify shared intrinsic state. |
| Iterator | Not used explicitly | Standard collection iteration is sufficient. |
| Mediator | Not used | `ParkingLot` coordinates the domain workflow; a separate mediator would add no useful boundary. |
| Memento | Not used | Undo/restore snapshots are not required. |
| Visitor | Not used | No stable object structure with multiple external operations exists. |
| Template Method | Not used | There are no related algorithms with invariant skeletons and variable steps. |


# STEP 4: IMPLEMENTATION


```text
class ParkingLot:
   public ParkingLot(parkingSpots, hourlyRate, clock, feeCalculator?):
      """
      Core Logic:
      1. Validate the supplied spot collection, hourly rate, and clock.
      2. Copy the spot collection so its ordering is stable and callers cannot change it later.
      3. Reject duplicate spot IDs.
      4. Store the clock and use the supplied fee calculator or create the default calculator.

      Error/Edge Case(s):
      1. Null spot collection or clock -> throw ArgumentNullException.
      2. Negative hourly rate -> throw ArgumentOutOfRangeException.
      3. Null spot in collection or duplicate spot ID -> throw ArgumentException.
      """

      validate constructor arguments
      parkingSpots = copy of supplied spots
      tickets = empty dictionary using ordinal ticket ID comparison
      clock = supplied clock
      feeCalculator = supplied calculator or new ParkingFeeCalculator
      hourlyRate = supplied hourly rate
      syncLock = new object


   public ParkingTicket EnterVehicle(vehicle):
      """
      Core Logic:
      1. Validate the vehicle.
      2. Lock the lot for the complete allocation-and-registration transaction.
      3. Find the first available compatible spot.
      4. Create a ticket with a unique ID and the current UTC time.
      5. Occupy the spot and register the ticket.
      6. Roll back occupancy if ticket registration fails.
      7. Return the created ticket.

      Error/Edge Case(s):
      1. Null vehicle -> throw ArgumentNullException.
      2. No compatible spot -> throw InvalidOperationException; no state changes.
      3. Ticket registration fails -> release the spot and rethrow.
      4. Concurrent entries cannot claim the same spot because the whole workflow uses the same lock.
      """

      if vehicle is null:
         throw ArgumentNullException

      lock(syncLock):
         spot = FindAvailableSpot(vehicle.Type)

         if spot is null:
            throw InvalidOperationException("No compatible spot is available")

         entryTimeUtc = clock.UtcNow converted to UTC
         ticket = ParkingTicket(GenerateTicketId(), vehicle, spot, entryTimeUtc)

         spot.Occupy(vehicle.Type)

         try:
            tickets.Add(ticket.Id, ticket)
         catch:
            spot.Release()
            rethrow

         return ticket


   public decimal ExitVehicle(ticketId):
      """
      Core Logic:
      1. Validate the ticket ID.
      2. Lock the lot for the complete exit transaction.
      3. Find the ticket and require it to be Active.
      4. Obtain the exit time and calculate the fee before mutating state.
      5. Verify the assigned spot is occupied.
      6. Complete the ticket and release its spot.
      7. Return the fee.

      Error/Edge Case(s):
      1. Null, empty, or whitespace ticket ID -> throw ArgumentException.
      2. Unknown ticket ID -> throw ArgumentException.
      3. Completed ticket -> throw InvalidOperationException.
      4. Exit time before entry time -> fee calculation throws before state changes.
      5. Assigned spot is unexpectedly unoccupied -> throw InvalidOperationException before state changes.
      6. Concurrent exits for one ticket allow at most one successful completion.
      """

      if ticketId is null or whitespace:
         throw ArgumentException

      lock(syncLock):
         if !tickets.Contains(ticketId):
            throw ArgumentException("Unknown ticket")

         ticket = tickets[ticketId]

         if ticket.Status != TicketStatus.ACTIVE:
            throw InvalidOperationException("Ticket has already been used")

         exitTimeUtc = clock.UtcNow converted to UTC
         fee = feeCalculator.CalculateFee(ticket.EntryTimeUtc, exitTimeUtc, hourlyRate)

         if not ticket.AssignedSpot.IsOccupied:
            throw InvalidOperationException("Ticket and spot state are inconsistent")

         ticket.MarkCompleted()
         ticket.AssignedSpot.Release()

         return fee


   private ParkingSpot FindAvailableSpot(vehicleType):
      """
      Core Logic:
      1. Scan spots in the configured order.
      2. Return the first spot that is available and can fit the vehicle.
      3. Return null if no spot qualifies.

      Error/Edge Case(s):
      1. Empty spot list -> return null.
      2. No compatible available spot -> return null.
      """

      for spot in parkingSpots:
         if spot.IsAvailable and spot.CanFit(vehicleType):
            return spot

      return null


   private string GenerateTicketId():
      """
      Core Logic:
      1. Generate a GUID-based ticket ID.
      2. Retry if the ID already exists in retained ticket history.
      3. Return an unused ID; the caller adds it while holding the lot lock.

      Error/Edge Case(s):
      1. Extremely unlikely GUID collision -> generate another ID.
      2. Dictionary.Add remains the final duplicate guard and never overwrites a historical ticket.
      """

      do:
         ticketId = new GUID without separators
      while tickets contains ticketId

      return ticketId
```


```text
class ParkingSpot:
    public ParkingSpot(id, size):
      """
      Core Logic:
      1. Validate the spot ID.
      2. Store the fixed spot size.
      3. Initialize the spot as available.

      Error/Edge Case(s):
      1. Null, empty, or whitespace ID -> throw ArgumentException.
      2. Unsupported SpotSize value -> throw ArgumentOutOfRangeException.
      """

      validate id and size
      this.id = id
      this.size = size
      isOccupied = false


   public boolean IsAvailable:
      """
      Core Logic:
      1. Return the inverse of the internal occupancy flag.

      Error/Edge Case(s):
      1. None; the lot reads this property while holding its synchronization lock.
      """

      return not isOccupied


   public boolean CanFit(vehicleType):
      """
      Core Logic:
      1. Motorcycle fits any defined spot size.
      2. Car fits Car or Large spots.
      3. Large Vehicle fits only Large spots.
      4. Return false for unknown vehicle types.

      Error/Edge Case(s):
      1. Unsupported enum value -> return false.
      2. This checks size compatibility only; availability is checked separately.
      """

      if vehicleType == VehicleType.MOTORCYCLE:
         return true

      if vehicleType == VehicleType.CAR:
         return size == SpotSize.CAR or size == SpotSize.LARGE

      if vehicleType == VehicleType.LARGE_VEHICLE:
         return size == SpotSize.LARGE

      return false


   internal void Occupy(vehicleType):
      """
      Core Logic:
      1. Verify the spot is available.
      2. Verify the vehicle fits.
      3. Mark the spot occupied.

      Error/Edge Case(s):
      1. Spot already occupied -> throw InvalidOperationException.
      2. Vehicle does not fit -> throw InvalidOperationException.
      3. Internal mutation is coordinated by ParkingLot under its lock.
      """

      if isOccupied:
         throw InvalidOperationException

      if not CanFit(vehicleType):
         throw InvalidOperationException

      isOccupied = true


   internal void Release():
      """
      Core Logic:
      1. Verify the spot is occupied.
      2. Mark the spot available.

      Error/Edge Case(s):
      1. Spot already available -> throw InvalidOperationException.
      2. Internal mutation is coordinated by ParkingLot under its lock.
      """

      if not isOccupied:
         throw InvalidOperationException

      isOccupied = false
```


```text
class ParkingTicket:
   internal ParkingTicket(id, vehicle, assignedSpot, entryTimeUtc):
      """
      Core Logic:
      1. Validate the ticket ID and associated objects.
      2. Require the entry timestamp to be UTC.
      3. Store the assigned spot internally and expose only its stable ID.
      4. Initialize the ticket as Active.

      Error/Edge Case(s):
      1. Invalid ID or null dependency -> throw ArgumentException/ArgumentNullException.
      2. Non-UTC entry timestamp -> throw ArgumentException.
      """

      validate id, vehicle, assignedSpot, and UTC entry time
      store fields
      status = Active


   public string Id:
      """
      Core Logic:
      1. Return the immutable ticket identifier.

      Error/Edge Case(s):
      1. None.
      """

      return id


   public Vehicle Vehicle:
      """
      Core Logic:
      1. Return the immutable vehicle object associated with this ticket.

      Error/Edge Case(s):
      1. None.
      """

      return vehicle


   public string SpotId:
      """
      Core Logic:
      1. Return the assigned spot's stable ID without exposing the mutable spot object.

      Error/Edge Case(s):
      1. None.
      """

      return assignedSpot.Id


   public DateTimeOffset EntryTimeUtc:
      """
      Core Logic:
      1. Return the immutable UTC entry instant.

      Error/Edge Case(s):
      1. None.
      """

      return entryTimeUtc


   public TicketStatus Status:
      """
      Core Logic:
      1. Read the lifecycle status.

      Error/Edge Case(s):
      1. None; lifecycle mutation is internal and coordinated by ParkingLot.
      """

      return status


   internal ParkingSpot AssignedSpot:
      """
      Core Logic:
      1. Give the owning ParkingLot access to the associated spot for coordinated exit handling.
      2. Do not expose this reference through the public ticket API.

      Error/Edge Case(s):
      1. None; access is internal to this implementation.
      """

      return assignedSpot


   internal void MarkCompleted():
      """
      Core Logic:
      1. Transition the ticket from Active to Completed.
      2. Reject any second completion attempt.

      Error/Edge Case(s):
      1. Ticket already Completed -> throw InvalidOperationException.
      """

      if status != TicketStatus.ACTIVE:
         throw InvalidOperationException

      status = Completed
```


```text
class ParkingFeeCalculator:
   + CalculateFee(entryTimeUtc, exitTimeUtc, hourlyRate) -> decimal


   public decimal CalculateFee(entryTimeUtc, exitTimeUtc, hourlyRate):
      """
      Core Logic:
      1. Require UTC timestamps.
      2. Reject an exit instant earlier than the entry instant.
      3. Reject a negative hourly rate.
      4. Compute elapsed time using ticks.
      5. Round partial hours up and enforce a one-hour minimum.
      6. Multiply billable hours by the common hourly rate.

      Error/Edge Case(s):
      1. Non-UTC timestamp -> throw ArgumentException.
      2. Exit before entry -> throw ArgumentException.
      3. Negative hourly rate -> throw ArgumentOutOfRangeException.
      4. Zero duration -> charge one hour.
      5. Exactly one hour -> charge one hour.
      6. Any positive fraction beyond one hour -> charge two hours.
      7. Zero rate is valid and yields a zero fee.
      """

      validate UTC timestamps
      if exitTimeUtc < entryTimeUtc:
         throw ArgumentException

      if hourlyRate < 0:
         throw ArgumentOutOfRangeException

      elapsedTicks = (exitTimeUtc - entryTimeUtc).Ticks
      roundedHours = ceiling(elapsedTicks / TicksPerHour)
      billableHours = max(1, roundedHours)

      return billableHours * hourlyRate
```


# STEP 5: EXTENSIBILITY


## 1. What if multiple entry/exit calls happen concurrently, or an exit occurs while an entry is running?


**Chosen approach: one private lock per ParkingLot.**

- `EnterVehicle` holds the lock while selecting a spot, marking it occupied, creating/registering the ticket and completing rollback if registration fails.
- `ExitVehicle` holds the same lock while validating the ticket, calculating the fee, marking the ticket completed, and releasing the spot.
- No public API exposes the mutable spot reference or occupancy flag, so callers cannot bypass this synchronization boundary.

**Trade-off:**

The design favors a simple correctness argument over maximum parallelism. Two operations on the same lot wait for one another even when they concern different spots. This is appropriate for the stated in-memory interview scope. If throughput becomes a measured problem, the synchronization model can be refined only after defining lock ordering and proving cross-object invariants.

**Why not a concurrent queue?**

A concurrent queue makes enqueue/dequeue thread-safe, but it does not make the full allocation-and-occupancy workflow atomic. It would also introduce pending versus active request semantics that the current requirements do not need.


## 2. What invariants must always hold?


1. A spot is assigned to at most one active ticket.
2. An Active ticket's assigned spot is occupied.
3. A Completed ticket cannot be exited again.
4. A rejected entry does not occupy a spot or create a ticket.
5. A rejected exit does not complete a ticket or release a spot.
6. Ticket IDs are unique within the lifetime of this in-memory lot.
7. All state transitions across the ticket registry and spots occur under the same lot lock.


## 3. Race trace: two vehicles compete for one spot


1. Thread A calls `EnterVehicle`.
2. Thread A acquires `_syncLock` and selects the only compatible spot.
3. Thread B calls `EnterVehicle` and waits for `_syncLock`.
4. Thread A marks the spot occupied and registers its ticket, then releases the lock.
5. Thread B acquires the lock and finds no compatible available spot.
6. Thread B is rejected; the spot has only one active ticket.

The lock serializes the complete transaction, not merely individual collection operations.


## 4. Liveness and lock discipline


- The critical sections contain only in-memory work and a clock read; they perform no network calls or blocking I/O.
- The lock is acquired through the language's `lock` construct, which releases it when the scope exits, including exceptional paths.
- Do not call arbitrary user callbacks while holding the lock.
- The design does not guarantee fairness between waiting threads; fairness is not required by the current specification.


## 5. Visibility and lifecycle


- Occupancy is internal and accessed by `ParkingLot` while it holds the lock.
- Ticket completion is internal. `Status` is read atomically to avoid a torn/stale integer state observation.
- Ticket history is retained for the process lifetime to reject duplicate exits. A production system would need an explicit retention/archival policy.
- Process termination loses all in-memory tickets and occupancy state; durable recovery is out of scope.


## 6. Concurrent verification / stress-test concept


No test project is included in this deliverable, but a future test harness should verify:

1. Many threads entering against a limited set of spots never cause more active tickets than available compatible spots.
2. Concurrent attempts to exit the same ticket produce exactly one successful exit.
3. A failed entry leaves all spot occupancy unchanged.
4. A failed exit leaves ticket status and spot occupancy unchanged.
5. Concurrent entry and exit operations preserve the invariants after every completed operation.
6. Fee boundary cases: zero duration, exactly one hour, just over one hour, and exit-before-entry.
7. Compatibility for every vehicle/spot-size pair.
8. Deterministic allocation follows the configured spot order.


## 7. Distributed boundary


The lock protects only one `ParkingLot` object inside one process. It does not coordinate multiple processes or application instances. A distributed deployment would need a durable source of truth and an atomic claim/release operation (for example, a database transaction with an appropriate concurrency constraint). A process-local lock must not be presented as a distributed lock.


## 8. Say this out loud in an interview


- “I put the lock around the complete entry and exit workflows because the invariant spans the ticket registry and spot occupancy.”
- “A concurrent collection alone would not make spot selection plus occupancy assignment atomic.”
- “I retain completed tickets so a repeated exit is distinguishable from an unknown ticket.”
- “I inject the clock because pricing is time-dependent and deterministic time is important for verification.”
- “I chose one lock for correctness and clarity. I would only introduce finer-grained locking after measuring contention and defining a safe lock order.”
- “This is thread-safe within one process; it does not solve coordination between multiple application instances.”