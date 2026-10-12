using System;

public class Request {
    private readonly int floor;
    private readonly RequestType type;

    public Request(int floor, RequestType type) {
        this.floor = floor;
        this.type = type;
    }

    public int GetFloor() {
        return floor;
    }

    public RequestType GetType() {
        return type;
    }

    public override bool Equals(object obj) {
        if (obj == null || GetType() != obj.GetType()) {
            return false;
        }

        Request request = (Request)obj;
        return floor == request.floor && type == request.type;
    }

    public override int GetHashCode() {
        return HashCode.Combine(floor, type);
    }
}