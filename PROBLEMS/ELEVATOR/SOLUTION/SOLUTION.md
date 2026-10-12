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


