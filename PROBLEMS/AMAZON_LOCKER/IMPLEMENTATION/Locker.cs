using System;
using System.Collections.Generic;

public class Locker{
    private readonly Compartment[] _compartments;
    private readonly Dictionary<string, AccessToken> _accessTokenMapping;
    private readonly Random _random;

    public Locker(Compartment[] compartments) {
        _compartments = compartments;
        _accessTokenMapping = new Dictionary<string, AccessToken>();
        _random = new Random();
    }

    public string DepositPackage(Size size) {
        var compartment = GetAvailableCompartment(size);
        if (compartment == null)
        {
            throw new InvalidOperationException($"No available compartment of size {size}");
        }

        compartment.Open();
        compartment.MarkOccupied();
        var accessToken = GenerateAccessToken(compartment);
        _accessTokenMapping[accessToken.Code] = accessToken;

        return accessToken.Code;
    }

    public void Pickup(string tokenCode) {
        if (string.IsNullOrWhiteSpace(tokenCode)) {
            throw new InvalidOperationException("Invalid access token code");
        }

        if (!_accessTokenMapping.TryGetValue(tokenCode, out var accessToken)) {
            throw new InvalidOperationException("Invalid access token code");
        }

        if (accessToken.IsExpired()) {
            throw new InvalidOperationException("Access token has expired");
        }

        var compartment = accessToken.GetCompartment();
        compartment.Open();
        ClearDeposit(accessToken);
    }

    public void OpenExpiredCompartments() {
        foreach (var accessToken in _accessTokenMapping.Values) {
            if (accessToken.IsExpired()) {
                var compartment = accessToken.GetCompartment();
                compartment.Open();
            }
        }
    }

    private Compartment? GetAvailableCompartment(Size size) {
        foreach (var c in _compartments) {
            if (c.Size == size && !c.IsOccupied()) {
                return c;
            }
        }
        return null;
    }

    private AccessToken GenerateAccessToken(Compartment compartment) {
        var code = _random.Next(0, 1_000_000).ToString("D6");
        var expiration = DateTime.UtcNow.AddDays(7);
        return new AccessToken(code, expiration, compartment);
    }

    private void ClearDeposit(AccessToken accessToken) {
        var compartment = accessToken.GetCompartment();
        compartment.MarkFree();
        _accessTokenMapping.Remove(accessToken.Code);
    }
}