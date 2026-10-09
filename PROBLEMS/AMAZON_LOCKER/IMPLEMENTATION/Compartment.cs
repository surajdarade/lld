public class Compartment {
    public Size Size { get; }
    private bool _occupied;

    public Compartment(Size size) {
        Size = size;
        _occupied = false;
    }

    public bool IsOccupied() {
        return _occupied;
    }

    public void MarkOccupied() {
        _occupied = true;
    }

    public void MarkFree() {
        _occupied = false;
    }

    public void Open() {
        // Logic to open the compartment (e.g., unlock the door)
    }
}