public class Game {
    private readonly Board _board;
    private readonly Player _player1;
    private readonly Player _player2;
    private Player _currentPlayer;
    private GameState _state;
    private Player? _winner;

    public Game(Player player1, Player player2) {
        _board = new Board();
        _player1 = player1;
        _player2 = player2;
        _currentPlayer = player1;
        _state = GameState.InProgress;
    }

    public bool MakeMove(Player player, int column) {
        if (_state != GameState.InProgress) {
            return false;
        }

        if (player != _currentPlayer) {
            return false;
        }

        var row = _board.PlaceDisc(column, player.Color);
        if (row == -1) {
            return false;
        }

        if (_board.CheckWin(row, column, player.Color)) {
            _state = GameState.Won;
            _winner = player;
        }
        else if (_board.IsFull()){
            _state = GameState.Draw;
        }
        else {
            _currentPlayer = _currentPlayer == _player1 ? _player2 : _player1;
        }
        return true;
    }

    public Player CurrentPlayer => _currentPlayer;

    public GameState State => _state;

    public Player? Winner => _winner;

    public Board Board => _board;
}