using System;

public sealed class AlertConfig
{
    public int Threshold { get; }

    public AlertConfig(int threshold)
    {
        if (threshold < 0)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        Threshold = threshold;
    }

    public bool IsLowStock(int quantity) => quantity <= Threshold;

    public bool ShouldNotify(int previousQuantity, int currentQuantity) =>
        previousQuantity > Threshold && currentQuantity <= Threshold;
}
