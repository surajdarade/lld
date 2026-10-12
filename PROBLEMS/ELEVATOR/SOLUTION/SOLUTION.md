# RESOURCE(S)


https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/elevator


# PROBLEM STATEMENT


Design an Elevator Control System for a building. The syttem should handle multiple elevators, floor requests,
and move elevators efficiently to serve requests.


Primary Capabilities:

- How many elevators and floors? Fixed or Configurable?

- Are Hall Calls only UP or DOWN or do they chose Floor?

- Can passanger select multiple destination floors inside the Elevator?

Error/Edge Case Handling:

- What about invalid floor requests?

- What if someone requests the floor they're already on?

Scope Boundaries:

- Capacity, Weight Limits, Door Mechanics, Emergency Stops?

- Simulation (step/tick) or actual hardware control software?


# STEP 1: REQUIREMENTS


1. System manages 3 elevators serving 10 floors (0-9)

2. Users can request an elevator from any floor (hall call), System decides which elevator to dispatch

3. Once inside, user can select one or more destination floors

4. Simulation runs in discrete time steps (e.g. `step()` or `tick()` call advances time)

5. Elevator stop comes in two types:
   - Hall Calls: Request from a floor with direction (UP or DOWN)
   - Destination: Request from inside of an Elevator (no direction specified)

6. System handles multiple concurrent pickup requests across floors

7. Invalid requests should be rejected (return false)
   - Non-existent floor requests

8. Requests for the current floor are treated as a no-operation/already served (doors out of scope)


**Out of Scope:**

- Weight Capacity and Passanger Limits
- Door Open/Close Mechanics
- Emergency Stop Functionality
- Dynamic Floor/Elevator Configuration
- UI/Rendering Layer


# STEP 2: ENTITIES


- Elevator
- Request
- ElevatorController

**Unnecessary to Current Set of Requirements**
- Floor


# STEP 3: CLASS DESIGN


```text
class ElevatorController:
    - elevators: List<Elevator>

    + ElevatorController()
    + RequestElevator(floor, type) -> boolean
    + Step() -> void
```


```text
class Elevator:
   - currentFloor: int
   - direction: Direction
   - requests: Set<Request>

   + Elevator() 
   + AddRequest(request) -> boolean
   + Step() -> void
   + GetCurrentFloor() -> int
   + GetDirection() -> Direction
```


```text
enum Direction:
   UP
   DOWN
   IDLE
```


```text
class Request:
   - floor: int
   - type: RequestType

   + Request(floor, type)
   + GetFloor() -> int
   + GetType() -> RequestType
```


```text
enum RequestType:
   PICKUP_UP
   PICKUP_DOWN
   DESTINATION
```


# STEP 4: IMPLEMENTATION


```text
class EnevatorController:
   public void Step():
      for e in elevators
         e.Step()


   public boolean RequestElevator(floor, type: PICKUP_UP | PICKUP_DOWN):
      """
      Core Logic:
      1. Find the best Elevator to handle this request
      2. Send that request to the Elevator

      Error/Edge Case(s):
      1. Floor out of bounds -> throw error
      """

      if (floor < 0 || floor > 9)
         throw Error("Floor Does Not Exist")

      request = Request(floor, type)

      best = SelectBestElevator(request)

      return best.AddRequest(request)

   
   private Elevator SelectBestElevator(request):
      """
      Core Logic:
      1. Try to find the elevator moving toward the direction
      2. If none, try idle elevators (nearest)
      2. Pick the nearest

      Error/Edge Case(s):
      1. None
      """

      best = FindMovingToward(request)
      
      if (!best)
         return best

      best = FindNearestIdle(request)

      if (!best)
         return best

      return FindNearest(request.GetFloor())


   private Elevator FindMovingToward(request):
      """
      Core Logic:
      1. Scan elevators going that direction
      2. Keep track of closest
      3. Return closest

      Error/Edge Case(s):
      1. None
      """

      floor = request.GetFloor()

      direction = request.GetType() == RequestType.PICKUP_UP ? UP : DOWN

      nearest = null

      minDistance = INT_MAX

      for e in elevators
         if (e.GetDirection() != direction)
            continue

         if ((e.GetDirection() == Direction.UP && e.GetFloor() > floor)
          || (e.GetDirection() == Direction.DOWN && e.GetFloor() < floor))
            continue

         distance = abs(e.GetFloor() - floor)

         if (distance < minDistance)
            minDistance = distance

            nearest = e

      return nearest
```

```text
class Elevator:
   public void Step():
      """
      Core Logic:
      1. If idle with requests, pick direction towards the nearest request
      2. Check if we should stop of the current floor (matches a hall call or a destination)
      3. If no requests ahead of us (and other requests pending), reverse
      4. Move on floor in current direction

      Error/Edge Case(s):
      1. No requests -> set IDLE, return
      2. Stop move cant happen in same tick, reverse and move
      """

      # Case 1: Nothing to serve
      if (requests.IsEmpty())
         direction = Direction.IDLE

         return

      # Case 2: Pick direction if IDLE
      if (direction == IDLE)
         nearest = FindNearestRequest()

         direction = nearest.GetFloor() > floor ? Direction.UP : Direction.DOWN

      # Case 3: Stop at Current Floor?
      type = direction == UP ? RequestType.PICKUP_UP : RequestType.PICKUP_DOWN

      hallCallRequest = Request(floor, type)

      destinationRequest = Request(floor, RequestType.DESTINATION)

      if (requests.Contains(hallCallRequest) || requests.Contains(destinationRequest))
         requests.Remove(hallCallRequest)

         requests.Remove(destinationRequest)

         stop()

         return

      # Case 4: Reverse if nothing ahead
      if (!HasRequestsAhead(direction))
         direction = direction == Direction.UP ? Direction.DOWN : Direction.UP

         return

      # Case 5: Move
      if (direction == Direction.UP)
         floor++
      else if(direction == Direction.DOWN)
         floor--


   private Request FindNearestRequest():
      """
      Core Logic:
      1. Scan all requests, find closest by floor distance

      Error/Edge Case(s):
      1. Ties broken arbritarily
      """

      nearest = null

      minDistance = INT_MAX

      for request in requests
         distance = abs(req.GetFloor() - floor)
         
         if (distance < minDistance)
            minDistance = distance

            nearest = request

      return nearest

   
   private boolean HasRequestsAhead(direction):
      """
      Core Logic:
      1. Check if ANY request exists ahead in the given direction
      2. Regardless of type-we travel toward all requests, only stop for matching ones

      Error/Edge Case(s):
      1. None, naturally handles floor boundaries (nothing above 9 and nothing below 0)
      """

      for request in requests
         if (direction == Direction.UP && request.GetFloor() > currentFloor)
            return true

         if (direction == Direction.DOWN && request.GetFloor() < currentFloor)
            return true

         return false
```


# STEP 5: EXTENSIBILITY


1. What if multiple hall calls come in at the same time? What if a hall call comes in while step is running?

- Use lock, acquire and release the lock

```text
RequestElevator(floor, type):
   // BEFORE ADDING THE REQUEST TO AN ELEVATOR
   lock.Acquire()
   // ADD LOGIC
   lock.Release()

Step():
   // BEFORE REMOVING REQEUST FROM ELEVATOR
   lock.Acquire()
   // REMOVE LOGIC
   lock.Release()
```

Downside with this approach is, Hall Call has to wait while the Step() is running and Vice Versa.


- Use concurrent thread-safe queue

```text
public boolean AddRequest(request):
   pendingRequests.Enqueue(request) // thread-safe queue

public void Step():
   while (!pendingRequests.IsEmpty())
      activeRequests.Add(pendingRequests.dequeue())

   // ALL LOGIC USES activeRequests
```


2. How would you add a priority floors or an express elevator?

- Use `isExpress` property in elevator
- Use array of priorityFloors in express elevator

```text
class Elevator:
   - isExpress: boolean
   - priorityFloors: [3, 7]
```


3. How would add the ability to cancel a floor request?

- Provide a method to remove request

```text
public boolean RemoveRequest(reqeust):
   lock.Acquire()

   requests.Remove(request)

   lock.Release()
```