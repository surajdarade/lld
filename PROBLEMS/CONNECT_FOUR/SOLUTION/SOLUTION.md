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