using System;
using System.Collections.Generic;
using System.Linq;

public class ElevatorController {
    private readonly List<Elevator> _elevators;

    public ElevatorController() {
        _elevators = new List<Elevator> {
            new Elevator(),
            new Elevator(),
            new Elevator()
        };
    }

    public bool RequestElevator(int floor, RequestType type) {
        if (floor < 0 || floor > 9) {
            return false;
        }
        if (type == RequestType.DESTINATION) {
            return false;
        }

        var request = new Request(floor, type);
        var best = SelectBestElevator(request);
        return best.AddRequest(request);
    }

    public void Step() {
        foreach (var elevator in _elevators) {
            elevator.Step();
        }
    }

    private Elevator SelectBestElevator(Request request) {
        var best = FindCommittedToFloor(request);
        if (best != null) {
            return best;
        }

        best = FindNearestIdle(request.GetFloor());
        if (best != null) {
            return best;
        }

        return FindNearest(request.GetFloor());
    }

    private Elevator? FindCommittedToFloor(Request request) {
        var floor = request.GetFloor();
        var direction = request.GetType() == RequestType.PICKUP_UP ? Direction.UP : Direction.DOWN;

        Elevator? nearest = null;
        var minDistance = int.MaxValue;

        foreach (var e in _elevators) {
            if (e.Direction != direction) {
                continue;
            }

            if ((direction == Direction.UP && e.CurrentFloor > floor) ||
                (direction == Direction.DOWN && e.CurrentFloor < floor)) {
                continue;
            }

            if (!e.HasRequestsAtOrBeyond(floor, direction)) {
                continue;
            }

            var distance = Math.Abs(e.CurrentFloor - floor);
            if (distance < minDistance) {
                minDistance = distance;
                nearest = e;
            }
        }

        return nearest;
    }

    private Elevator? FindNearestIdle(int floor) {
        Elevator? nearest = null;
        var minDistance = int.MaxValue;

        foreach (var e in _elevators) {
            if (e.Direction != Direction.Idle) {
                continue;
            }

            var distance = Math.Abs(e.CurrentFloor - floor);
            if (distance < minDistance) {
                minDistance = distance;
                nearest = e;
            }
        }

        return nearest;
    }

    private Elevator FindNearest(int floor) {
        var nearest = _elevators[0];
        var minDistance = Math.Abs(_elevators[0].CurrentFloor - floor);

        foreach (var e in _elevators) {
            var distance = Math.Abs(e.CurrentFloor - floor);
            if (distance < minDistance) {
                minDistance = distance;
                nearest = e;
            }
        }

        return nearest;
    }
}