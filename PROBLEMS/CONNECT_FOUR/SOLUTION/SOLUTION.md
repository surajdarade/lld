# Resource(s):


https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/connect-four


# Problem Statement:


Build a two-player Connect Four Game. Players take turns dropping discs into a 7-column, 6-row board.
The first to align four of their own discs vertically or horizontally or diagonally wins.


Primary Capabilities:

Error/Edge Case Handling:

Scope Boundaries:


# STEP 1: REQUIREMENTS


1. Two players take turns dropping discs into a 7-column, 6-row board

2. A disc falls to the lowest available row in the chosen column

3. The game ends when:
   - A player gets four discs in a row (vertical, horizontal, or diagonal). They win.
   - The board is full. It's a draw.

4. Invalid moves should be rejected clearly:
   - Dropping in a full column.
   - Moving out of turn.
   - Moving after the game is over.

Out of scope: 

- UI support
- Concurrent games
- Move history
- Undo
- Board size configuration


# STEP 2: ENTITIES


- Game
- Board
- Player

Unnecessary to Current Set of Requirements
- Disc


# STEP 3: CLASS DESIGN


```text
class Game:
    - board: Board
    - player1: Player
    - player2: Player
    - currentPlayer: Player
    - state: GameState        // IN_PROGRESS, WON, DRAW
    - winner: Player?         // null if no winner yet or draw

    + Game(player1, player2)
    + MakeMove(player, column) -> boolean
    + GetCurrentPlayer() -> Player
    + GetGameState() -> GameState
    + GetWinner() -> Player?
    + GetBoard() -> Board
```


```text
enum GameState:
    IN_PROGRESS
    WON
    DRAW
```


```text
class Board:
    - rows: int
    - columns: int
    - grid: DiscColor?[rows][cols]

    + Board()
    + CanPlace(column) -> boolean
    + PlaceDisc(column, color) -> int // return row the disc lands in or -1
    + IsFull() -> boolean
    + CheckWin(row, column, color) -> boolean
    + GetRows() -> int
    + GetCols() -> int
    + GetCell() -> DiscColor?
```


```text
enum DiscColor:
    RED
    BLUE
```


```text
class Player:
    - color: DiscColor
    - name: string

    + Player(name, color)
    + GetName() -> string
    + GetColor() -> DiscColor
```


# STEP 4: IMPLEMENTATION


```text
class Game:
    public boolean MakeMove(player, column):
        """
        Core Logic:
        1. Place Disc
        2. Check Win
        3. If not, check for a Draw
        4. Switch Turns

        Error/Edge Case(s):
        1. Game already over
        2. Wrong player turn

        - this is actually board
        - column index out of bounds
        - column is full
        """

        if (state != GameState.IN_PROGRESS)
            return false

        if (player != currentPlayer)
            return false

        row = board.PlaceDisc(column, player.GetColor())
        
        if (row == -1)
            return false

        if (board.CheckWin(row, column, player.GetColor())
            state = GameState.WON
            winner = player

        else if (board.IsFull())
            return state = GameState.DRAW

        else 
            currentPlayer = (player == player1) ? player2 : player1

        return true
```


```text
class Board:
    public int PlaceDisc(column, color):
        """
        Core Logic:
        1. Find the lowest empty row for that column
        2. Place Disc
        3. Return the row it landed in

        Error/Edge Case(s):
        1. Column index out of bounds
        2. Column is full
        """

        if (column < 0 || column >= board.GetCols()) 
            return -1

        if(!board.CanPlace(column))
            return -1

        for row = board.GetRows() - 1 to 0
            if (grid[row][column] == null)
                grid[row][column] = color

                return row

        return -1

    
    public boolean CheckWin(row, column, color):
        """
        Core Logic:
        1. Check for four in a row in all for directions
        2. Return true if found, false otherwise

        Error/Edge Case(s):
        1. Row or column out of bounds -> return false
        2. Cell at (row, column) doesn't match color -> return false
        """

        if (r < 0 || r >= board.GetRows() || c < 0 || c >= board.GetCols())
            return false

        if (board.GetCell(row, column) != color)
            return false

        directions = [
            [0, 1], // horizontal
            [1, 0], // vertical 
            [1, 1], // diagonal
            [-1, 1] // other diagonal
        ]

        for dr, dc in directions
            count = 1

            count += CountInDirection(row, column, dr, dc, color) // move in one direction
            
            count += CountInDirection(row, column, -dr, -dc, color) // move in one direction 

            if (count >= 4)
                return true

            return false

    
    public int CountInDirection(row, column, dr, dc, color):
        count = 0

        r = row + dr

        c = column + dc

        while (r >= 0 && r < board.GetRows() && c >= 0 && c < board.GetCols() && board.GetCell(r, c) == color)
            count++

            r += dr

            c += dc

        return count
```


# STEP 5: EXTENSIBILITY


1. How would you support different board sizes?

Construct the board with desired rows and columns


2. How would you add undo or move history?

Undo belongs in `Game` because `Game` controls the lifecycle, turn order and when state changes
I'd keep a `moveHistory` stack. Each time a move succeeds, I push a small `Move` record containing player, row and column
Undo would pop the last move, clear that cell in the board, revent `currentPlayer` and recalculate game state if needed
The board does'nt need any new logic besides maybe an internal `ClearCell` method


```text
class Move:
    - player: Player
    - row: int
    - column: int

    + Move(player, row, column)
```


```text
class Game:
    moveHistory: Stack<Move>

    public boolean MakeMove(player, column):
        // PREVIOUS LOGIC

        row = board.PlaceDisc(column, player.GetColor())

        moveHistory.Push(Move(player, row, column))

        // PREVIOUS LOGIC


    public bool Undo():
        if (moveHistory.IsEmpty())
            return false

        last = moveHistory.Pop()

        // REVERT MOVE
        board.ClearCell(last.row, last.column)

        // REVERT TURN ORDER
        currentPlayer = last.player

        // RECOMPUTE GAME STATE, SIMPLEST VERSION
        state = GameState.IN_PROGRESS

        winner = null

        return true
```

3. How would you add a computer opponent?

I'd keep the game reuls exactly where they are. `Game` and `Board` doesn't need to change
I'd introduce a small bot component that looks at the current board and returns a column
From `Game's` persepctive, a bot move is just another call to `MakeMove(currentPlayer, column)`


```text
game = Game(humanPlayer, botPlayer)

while (game.GetGameState() == Gametate.IN_PROGRESS)
    currentPlayer = game.GetCurrentPlayer()

    column = // input from UI

    game.MakeMove(currentPlayer, column)
```


```text
class BotEngine:
    + ChooseMove(game, bot) -> int
```


```text
game = Game(humanPlayer, botPlayer)

bot = BotEngine()

while (game.GetGameState() == GameState.IN_PROGRESS)_
    currentPlayer = game.GetCurrentPlayer()

    if (currentPlayer == humanPlayer)
        column = // input from UI

    else 
        column = bot.ChooseMove(game, currentPlayer)

    game.MakeMove(currentPlayer, column)
```


Optional:

Might introduce `Player` interface and concrete implementations, `HumanPlayer` or `BotPlayer`, etc