# RESOURCE(S)


[https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/bookmyshow](https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/bookmyshow)


# PROBLEM STATEMENT


Design a movie ticket booking system similar to BookMyShow that allows users to browse movies, select theaters and showtimes, book tickets and manager reservations.


**Primary Capabilities:**

- Search movies by title and browse movies playing at a theater.
- View showtimes and available seats.
- Book multiple seats in a single reservation.
- Generate a confirmation ID for successful bookings.
- Cancel reservations and release their seats.

**Error/Edge Case Handling:**

- Reject bookings when any selected seat is unavailable.
- Ensure concurrent bookings for the same seat result in exactly one successful booking.
- Reject invalid seat selections and invalid confirmation IDs.
- Prevent cancellation from releasing seats more than once.
- Ensure failed bookings do not leave seats partially reserved.

**Scope Boundaries:**

- Theaters contain multiple screens, with a shared seat layout across all screens.
- Seat rows range from A to Z and seat numbers from 0 to 20.
- Payment processing is assumed to succeed.
- Rescheduling, variable seat layouts, seat types and UI rendering are excluded.


# STEP 1: REQUIREMENTS


1. Users can search for movies by title

2. Users can browse movies playing at a given theater

3. Theaters have multiple screens; all screens share the same seat layout (rows A-Z, seats 0-20)

4. Users can view available seats for a showtime and select specific ones

5. Users can book multiple seats in a single reservation; booking returns a confirmation ID

6. Concurrent booking of the same seat: exactly one succeeds

7. Users can cancel a reservation by confirmation ID, releasing the seats

Out of Scope:
- Payment processing (assume payment always succeeds)
- Variable seat layouts or seat types (all seats identical)
- Rescheduling (cancel and rebook instead)
- UI / rendering


# STEP 2: ENTITIES


- `Movie`
  - Stores a stable movie ID and title for search and discovery.
- `Theater`
  - Represents a theater and owns its screens.
- `Screen`
  - Represents a screen and creates the standard seat layout: rows A-Z and seats 0-20.
- `Seat`
  - Represents a physical seat identified by its row, number and ID. It does not own booking status.
- `Show`
  - Associates a movie with a theater screen and start time. Owns availability for that specific show.
- `Reservation`
  - Represents one confirmed booking with a confirmation ID, show, selected seat IDs and lifecycle status.
- `MovieTicketBookingSystem`
  - Coordinates movie discovery, show lookup, seat availability, booking and cancellation.

**Enums:**

- `SeatStatus`: `AVAILABLE`, `BOOKED`.
- `ReservationStatus`: `CONFIRMED`, `CANCELLED`.

**Unnecessary to Current Set of Requirements:**

- `User`: user accounts and identity management are not required.
- `Payment` / `PaymentProcessor`: payment processing is out of scope and is assumed to succeed.
- `SeatType` / pricing strategy: all seats are identical and pricing is not specified.
- `BookingHold`: temporary holds and payment timeouts are not required.
- `Review` / `Rating`: movie reviews are not required.
- `Notification`: booking notifications are not required.

**Relationships and State Ownership:**

- `Theater` owns its `Screen` objects; each screen creates the same standard physical seat layout.
- `Show` references a `Movie`, `Theater` and one of that theater's `Screen` objects.
- Each `Show` maintains an independent status for every seat on its screen. A physical seat can be booked for one show and available for another.
- `Reservation` references one show and stores all seat IDs booked in that show.
- `MovieTicketBookingSystem` is the sole coordinator of seat-state and reservation-state mutations.


# STEP 3: CLASS DESIGN


```text
class MovieTicketBookingSystem:
    - movies: Dictionary<string, Movie>
    - theaters: Dictionary<string, Theater>
    - shows: Dictionary<string, Show>
    - reservations: Dictionary<string, Reservation>
    - syncLock: object

    + SearchMovies(title) -> List<Movie>
    + GetMoviesAtTheater(theaterId) -> List<Movie>
    + GetShowsAtTheater(theaterId) -> List<Show>
    + GetAvailableSeats(showId) -> List<string>
    + BookSeats(showId, seatIds) -> string
    + CancelReservation(confirmationId) -> void
    - ValidateSeatSelection(show, seatIds) -> void
    - GetShowOrThrow(showId) -> Show
    - GetReservationOrThrow(confirmationId) -> Reservation
    - GenerateConfirmationId() -> string
```


```text
class Movie:
    - id: string
    - title: string

    + Movie(id, title)
    + Id: string
    + Title: string
```


```text
class Theater:
    - id: string
    - name: string
    - screens: Dictionary<string, Screen>

    + Theater(id, name, screens)
    + Id: string
    + Name: string
    + Screens: IReadOnlyCollection<Screen>
    + GetScreen(screenId) -> Screen
```


```text
class Screen:
    - id: string
    - seats: Dictionary<string, Seat>

    + Screen(id)
    + Id: string
    + Seats: IReadOnlyDictionary<string, Seat>
```5


```text
class Seat:
    - id: string
    - row: char
    - number: int

    + Id: string
    + Row: char
    + Number: int
```


```text
class Show:
    - id: string
    - movie: Movie
    - theater: Theater
    - screen: Screen
    - startTime: DateTimeOffset
    - seatStatuses: Dictionary<string, SeatStatus>

    + Id: string
    + Movie: Movie
    + Theater: Theater
    + Screen: Screen
    + StartTime: DateTimeOffset
    - GetAvailableSeatsUnsafe() -> List<string>
    - ValidateSeatsAvailable(seatIds) -> void
    - BookSeatsUnsafe(seatIds) -> void
    - ReleaseSeatsUnsafe(seatIds) -> void
```


```text
class Reservation:
    - confirmationId: string
    - show: Show
    - seatIds: IReadOnlyList<string>
    - status: ReservationStatus

    + ConfirmationId: string
    + Show: Show
    + SeatIds: IReadOnlyList<string>
    + Status: ReservationStatus
    - MarkCancelled() -> void
```


```text
enum SeatStatus:
    AVAILABLE
    BOOKED

enum ReservationStatus:
    CONFIRMED
    CANCELLED
```


**Design Decisions:**

1. `MovieTicketBookingSystem` coordinates booking and cancellation because each operation changes multiple related pieces of state.
2. `Screen` creates the fixed seat layout rather than accepting arbitrary layouts.
3. Seat status belongs to `Show`, not `Seat`, because availability differs by showtime.
4. A reservation stores all selected seats together and receives one confirmation ID.
5. One private system-wide lock is the simplest baseline for atomic booking and cancellation within one process.
6. Seat availability is validated for the complete selection before any seat state changes.
7. Cancellation validates the reservation and all associated seats before releasing seats and changing reservation status.
8. No user, payment, pricing, temporary hold or notification abstractions are introduced because they are not required.

**Principles Applied:**

- **Single Responsibility Principle:** `Screen` owns physical seat layout; `Show` owns show-specific seat state; the coordinator manages workflows.
- **Encapsulation:** seat status changes are internal and performed through the coordinator's synchronization boundary.
- **Invariant preservation:** booking either reserves every selected seat or none; cancellation releases seats at most once.
- **YAGNI:** payment, holds, variable seat types, rescheduling and UI concerns are omitted.

**Pattern Decision Log:**

| Pattern | Decision | Reason / trigger to reconsider |
|---|---|---|
| Strategy | Not used | No alternative pricing or booking policies are required. |
| State | Not used as a class hierarchy | Two reservation states are adequately represented by an enum. |
| Factory | Not used | Entity creation is simple and local. |
| Observer | Not used | Notifications and event subscribers are not required. |
| Singleton | Not used | The coordinator can be created and dependencies do not need global lifetime. |
| Repository | Not used | Persistence is not required by the stated scope. |
| Command | Not used | Booking requests are synchronous and do not need queuing or replay. |


# STEP 4: IMPLEMENTATION


**Atomic Booking:**

1. Acquire the global lock.
2. Validate the show, non-empty seat selection, duplicate IDs, seat membership and availability.
3. Change every selected seat to `BOOKED` only after all validation succeeds.
4. Register one reservation and return its confirmation ID.
5. If reservation registration fails, release the seats before propagating the error.


```text
class MovieTicketBookingSystem:
    public BookSeats(showId, seatIds):
      """
      Core Logic:
      1. Lock the shared state for the complete booking operation.
      2. Resolve the show and validate the full seat selection.
      3. Book every seat, create one reservation and return its confirmation ID.

      Error/Edge Case(s):
      1. Unknown show, empty selection, duplicate or invalid seat IDs -> reject.
      2. If any seat is unavailable, no seat state changes.
      3. If reservation registration fails, release the seats before rethrowing.
      4. Concurrent requests for the same seat are serialized by the lock.
      """

      lock(syncLock):
        show = GetShowOrThrow(showId)
        ValidateSeatSelection(show, seatIds)
        show.ValidateSeatsAvailable(seatIds)

        confirmationId = GenerateConfirmationId()
        show.BookSeatsUnsafe(seatIds)

        try:
          reservation = Reservation(confirmationId, show, copy(seatIds))
          reservations.Add(confirmationId, reservation)
        catch error:
          show.ReleaseSeatsUnsafe(seatIds)
          rethrow error

        return confirmationId


    public CancelReservation(confirmationId):
      """
      Core Logic:
      1. Lock the shared state.
      2. Find the reservation and ensure it is confirmed.
      3. Validate that all its seats are still booked.
      4. Release the seats and mark the reservation cancelled.

      Error/Edge Case(s):
      1. Unknown confirmation ID -> reject.
      2. Already-cancelled reservation -> reject without changing state.
      3. Validate all seats before releasing any of them.
      """

      lock(syncLock):
        reservation = GetReservationOrThrow(confirmationId)
        if reservation.Status != CONFIRMED:
          throw InvalidOperationException

        reservation.Show.ValidateSeatsBooked(reservation.SeatIds)
        reservation.Show.ReleaseSeatsUnsafe(reservation.SeatIds)
        reservation.MarkCancelled()
```


```text
class Show:
    private ValidateSeatsAvailable(seatIds):
      """
      Core Logic:
      1. Ensure every seat exists in this show's screen.
      2. Ensure every selected seat is AVAILABLE before mutation.

      Error/Edge Case(s):
      1. A seat not belonging to the screen is rejected.
      2. Any BOOKED seat rejects the whole booking.
      """

      for seatId in seatIds:
        if seatId not in seatStatuses:
          throw ArgumentException
        if seatStatuses[seatId] != SeatStatus.AVAILABLE:
          throw InvalidOperationException


    private ReleaseSeatsUnsafe(seatIds):
      """
      Core Logic:
      1. Validate that all requested seats are BOOKED.
      2. Change all of them to AVAILABLE.

      Error/Edge Case(s):
      1. Unknown or non-booked seats are rejected before any mutation.
      2. Caller must hold the coordinator's global lock.
      """

      for seatId in seatIds:
        if seatId not in seatStatuses or seatStatuses[seatId] != BOOKED:
          throw InvalidOperationException

      for seatId in seatIds:
        seatStatuses[seatId] = SeatStatus.AVAILABLE
```


**Concurrency and Correctness Review:**

- Two requests for the same seat: the first completes under the lock; the second sees `BOOKED` and fails.
- A multi-seat request with one unavailable seat: validation fails before any seat changes.
- Concurrent cancellation of one reservation: only one request sees `CONFIRMED`; the other is rejected.
- Seat availability reads use the same lock to avoid observing intermediate booking changes.
- The global lock coordinates threads in one process only. Multiple application instances require a shared transactional store or equivalent cross-instance coordination.


# STEP 5: EXTENSIBILITY


**1. How would you support different seat types and pricing strategies?**

- Introduce `SeatType` and a pricing component to calculate ticket prices.
- Update `Seat` to represent its type and use the pricing component during booking.
- Keep pricing separate from reservation management so pricing rules can evolve independently.


**2. How would you temporarily reserve seats while users complete payment?**

- Introduce a `SeatHold` with an expiration time.
- Mark seats as temporarily held before payment begins.
- Confirm the reservation after successful payment or release the seats when the hold expires.
- Ensure concurrent requests cannot hold or book the same seat.

**3. How would you persist bookings and recover after a restart?**

- Store movies, theaters, shows, seat availability and reservations in a database.
- Use database transactions to update seat states and reservation records together.
- Restore the booking state from the database after a restart.


**#4. How would you improve concurrency if the global lock became a bottleneck?**

- Replace the global lock with a lock per `Show`.
- Requests for different shows can proceed concurrently.
- Requests for the same show still coordinate their seat updates to prevent double booking.
- Keep cancellation and booking synchronized using the same show-level locking strategy.


**5. How would you handle concurrent bookings across multiple application instances?**

- An in-memory lock is insufficient because each instance has its own memory.
- Use database transactions and appropriate uniqueness constraints or conditional updates to protect seat availability.
- Commit seat updates and reservation creation atomically so only one competing booking succeeds.