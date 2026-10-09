public class Board {
    private const int Rows = 6;
    private const int ColsConst = 7;
    private readonly DiscColor?[,] _grid = new DiscColor?[Rows, ColsConst];

    public int RowsCount => Rows;
    public int Cols => ColsConst;

    public bool CanPlace(int column) {
        if (column < 0 || column >= ColsConst) {
            return false;
        }
        return _grid[0, column] == null;
    }

    public int PlaceDisc(int column, DiscColor color) {
        if (!CanPlace(column)) {
            return -1;
        }

        for (var row = Rows - 1; row >= 0; row--) {
            if (_grid[row, column] == null)
            {
                _grid[row, column] = color;
                return row;
            }
        }

        return -1;
    }

    public bool CheckWin(int row, int column, DiscColor color) {
        if (!InBounds(row, column) || _grid[row, column] != color) {
            return false;
        }

        int[][] directions = {
            new[] { 0, 1 },
            new[] { 1, 0 },
            new[] { 1, 1 },
            new[] { -1, 1 }
        };

        foreach (var dir in directions) {
            var count = 1;
            count += CountInDirection(row, column, dir[0], dir[1], color);
            count += CountInDirection(row, column, -dir[0], -dir[1], color);
            if (count >= 4) {
                return true;
            }
        }

        return false;
    }

    public bool IsFull() {
        for (var c = 0; c < ColsConst; c++) {
            if (_grid[0, c] == null) {
                return false;
            }
        }
        return true;
    }

    public DiscColor? GetCell(int row, int column) {
        if (!InBounds(row, column)) {
            return null;
        }
        return _grid[row, column];
    }

    private int CountInDirection(int row, int column, int dr, int dc, DiscColor color) {
        var count = 0;
        var r = row + dr;
        var c = column + dc;

        while (InBounds(r, c) && _grid[r, c] == color) {
            count++;
            r += dr;
            c += dc;
        }

        return count;
    }

    private bool InBounds(int row, int column) {
        return row >= 0 && row < Rows && column >= 0 && column < ColsConst;
    }
}