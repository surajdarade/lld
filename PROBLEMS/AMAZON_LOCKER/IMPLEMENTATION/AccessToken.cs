using System;

public class AccessToken {
    public string Code { get; }
    public DateTime Expiration { get; }
    public Compartment Compartment { get; }

    public AccessToken(string code, DateTime expiration, Compartment compartment) {
        Code = code;
        Expiration = expiration;
        Compartment = compartment;
    }

    public bool IsExpired() {
        return DateTime.UtcNow >= Expiration;
    }

    public Compartment GetCompartment() {
        return Compartment;
    }
}