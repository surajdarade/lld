# RESOURCE(S)


https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/parking-lot


# PROBLEM STATEMENT


Design a parking lot system where different types of vehicles can park and the system manages spot assignment and calculates fees upon exit.


**Primary Capabilities:**

**Error/Edge Case Handling:**

**Scope Boundaries:**


# STEP 1: REQUIREMENTS


1. System supports three Vehicle types: Motorycle, Car, Large Vehicle

2. When a Vehicle enters, system automatically assigns an available compatible psot

3. System issues a TIcket at entry

4. When a Vehicle exits, user provides Ticket ID
   - System validates the Ticket
   - Calculates fee based on time spent (hourly, rounded up)
   - Frees the spot for next use

5. Pricing is hourly with same rate for all vehicles

6. System rejects entry if no compatible spot is available

7. System rejects exit if Ticket is invalid or already used


**Out of Scope:**

- Payment Processing
- Physical gate hardware
- Security camers or monitoring
- UI/Display Systems
- Reservations or pre-booking


