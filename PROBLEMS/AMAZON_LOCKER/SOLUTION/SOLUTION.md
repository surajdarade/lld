# Resource(s):

https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/amazon-locker


Problem Statement:

Design a Locker System like Amazon Locker where delivery drivers can deposit
packages and customers can pick them up using a code.


Primary Capabilities:

- Are there different sized compartments?
- How does the customer get their code? Do we need to send an SMS or Email?

Error/Edge Case Handling:

- Can one customer have multiple packages in the system at once? Are access tokens uniqye per package?
- Low long the codes last? What happens to a package if it's never picked up?
- What if all compartments of a given size are full when a driver tries to deposit?

Scope Boundaries:

- What's in scope for this system? Are we modelling the whole delivery flow, or just the piece
from when the driver arrives at the locker until the customer picks up? 


# STEP 1: REQUIREMENTS


1. Carrier deposits a package by specifying size (small, medium, large)
   - System assigns an available compartment of matching size
   - Opens compartment and returns access token, or error if no space

2. Upon successful deposity, an access token is generated and returned
   - One access token per package

3. User retrieves package by entering access token
   - System validates code and opens compartment
   - Throws specific error if code is invalid or expired

4. Access tokens expire after 7 days
   - Expired codes are rejected if used for pickup
   - Package remains in compartment until staff removes it

5. Staff can open all expired compartments with expired tokens
   - System opens all compartments with expired tokens
   - Staff physically removes packages and returns them to sender

Out of Scope:

- How the package gets to the locker (delivery logistics)
- How the access token reaches the customer (sms/email notification)


# STEP 2: ENTITIES


- Compartment
- Locker
- AccessToken

Unnecessary to Current Set of Requirements
- Carrier/Driver
- User
- Package


# STEP 3: CLASS DESIGN


class Locker:
    - compartments: Compartment[]
    - accessTokenMapping: Dictionary<string, AccessToken>

    + Locker(Compartments)
    + DepositPackage(Size) -> string | error
    + PickUp(AccessToken) -> void | error
    + OpenExpiredCompartments() -> void


class AccessToken:
    - code: string
    - expiration: TimeStamp/DateTime
    - compartment: Compartment

    + AccessToken(Code, Expiration, Compartment)
    + IsExpired() -> boolean
    + GetCompartment() -> Compartment
    + GetCode() -> string


class Compartment:
    - size: Size
    - occupied: boolean

    + Compartment(Size)
    + GetSize() -> Size
    + IsOccupied() -> boolean
    + MarkOccupied() -> void
    + MarkFree() -> void
    + Open() -> void


enum Size:
    SMALL 
    MEDIUM 
    LARGE 


# STEP 4: IMPLEMENTATION


class Locker:
   public string DepositPackage(Size):
      """
      Core Logic:
      1. Find Compartment of Right Size
      2. Open the Compartment
      3. Mark Compartment as Occupied 
      4. Generate AccessToken 
      5. Store the AccessToken 
      6. Return the AccesToken Code

      Error/Edge Case(s):
      1. No Compartment of Right Size -> Throw Error
      """

      compartment = GetAvailableCompartment(Size)

      if (compartment == null)
         throw Error("No Available Compartment of {Size} Size")

      compartment.Open()

      compartment.MarkOccupied()

      accessToken = GenerateAccessToken(Compartment)

      code = accessToken.GetCode()

      accessTokenMapping[code] = accessToken

      return code


   private Compartment GetAvailableCompartment(Size):
      """
      Core Logic:
      1. Scan All Compartments
      2. For each, see if right size and free
      3. Return first matching

      Error/Edge Case(s):
      1. None available -> return null
      """

      for c in compartments
         if (c.GetSize() == Size && !c.IsOccupied())
            return c 

      return null

   
   public void PickUp(Code):
      """
      Core Logic:
      1. Look up the code to get AccessToken 
      2. Get the Compartment
      3. Open the Compartment 
      4. Mark the Compartment as Free 
      5. Remove the AccessToken

      Error/Edge Case(s):
      1. Validation of the Code, not empty -> throw
      2. Code is not in mapping -> throw
      3. AccessToken is exipred -> throw
      """

      if (Code.IsEmpty())
         throw Error("Invalid Code")

      accessToken = accessTokenMapping[Code]

      if (accessToken == null)
         throw Error("Invalid Code")

      if (accessToken.IsExpired())
         throw Error("Access Code Expired")

      compartment = accessToken.GetCompartment()

      compartment.Open()

      compartment.MarkFree()

      accessTokenMapping.Remove(Code)

   public void OpenExpiredCompartments():
      """
      Core Logic:
      1. Scan through all access tokens in mapping 
      2. Find the expired tokens 
      3. Get compartment of each 
      4. Open those compartments 
      --- Remove the Packages [Out of Scope]
      5. Mark the compartment available again 
      6. Remove AceessToken from Map?? Decision: No

      Error/Edge Case(s):
      1. None that aren't implicitly handled
      """

      for code, accessToken in accessTokenMapping
         if accessToken.IsExpired()
            compartment = accessToken.GetCompartment()

            compartment.Open()

            // Assume the employee removed them and doors auto-close

            compartment.MarkFree()

            // Don't remove from accessTokenMapping so they still get correct error 

         // Loop to clear out the accessToken from the map that are 3month+ old


# STEP 5: EXTENSIBILITY


1. What if we want to allow a smaller package to use a large compartment as a fallback when all 
   exact-size compartments are full?

private GetAvailableCompartment(RequestedSize):
   sizesInOrder = [SMALL, MEDIUM, LARGE]

   startIndex = sizesInOrder.IndexOf(RequestedSize)

   for i from startIndex to sizesInOrder.Length
      size = sizesInOrder[i]

      for c in compartments 
         if (c.GetSize() == size && !c.IsOccupied())
            return c 

   return null


2. How would you handle compartments that are broken or under maintenance?

enum CompartmentStatus:
   AVAILABLE 
   OCCUPIED 
   OUT_OF_SERVICE


class Compartment:
   - size: Size
   - status: CompartmentStatus

   + IsAvailable() -> boolean
   + MarkOccupied() -> void
   + MarkAvailable() -> void 
   + MarkOutOfService() -> void
   OR 
   + UpdateStatus(CompartmentStatus.STATUS) -> void


3. How would you ensure packages are actually deposited before generating access tokens?

Two-Phased Commit 

class Locker:
   + ReserveCompartment(Size) -> reservationId
   + ConfirmDeposit(Reservationld) —> tokenCode
   + CancelReservation(Reservationld) -> void


class Compartment:
   - size: Size
   - status: CompartmentStatus // AVAILABLE, RESERVED, OCCUPIED, OUT_OF_SERVICE


enum CompartmentStatus:
   AVAILABLE
   RESERVED
   OCCUPIED
   OUT_OF_SERVICE


public GUID ReserveCompartment(Size):
   compartment = GetAvailableCompartment(Size)

   if (!compartment)
      throw Error("No Available Compartment")

   compartment.MarkReserved()

   compartment.Open()

   reservationId = GetReservationId()

   reservationMapping[reservationId] = compartment

   return reservationId


public string ConfirmDeposit(GUID ReservationId):
   compartment = reservationMapping[reservationId]

   if (!compartment)
      throw Error("Invalid Reservation")

   compartment.MarkOccupied()

   accessToken = GenerateAccessToken()

   accessTokenMapping[accessToken.GetCode()] = accessToken

   reservationMapping.Remove(reservationId)

   return accessToken.GetCode()