namespace ParkingLotSystem;

public sealed class ParkingFeeCalculator
{
    private const long TicksPerHour = TimeSpan.TicksPerHour;

    public decimal CalculateFee(
        DateTimeOffset entryTimeUtc,
        DateTimeOffset exitTimeUtc,
        decimal hourlyRate)
    {
        if (entryTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Entry time must be UTC.", nameof(entryTimeUtc));
        }

        if (exitTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Exit time must be UTC.", nameof(exitTimeUtc));
        }

        if (exitTimeUtc < entryTimeUtc)
        {
            throw new ArgumentException("Exit time cannot precede entry time.", nameof(exitTimeUtc));
        }

        if (hourlyRate < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hourlyRate),
                "Hourly rate cannot be negative.");
        }

        long elapsedTicks = (exitTimeUtc - entryTimeUtc).Ticks;
        decimal elapsedHours = (decimal)elapsedTicks / TicksPerHour;
        decimal roundedHours = decimal.Ceiling(elapsedHours);
        decimal billableHours = Math.Max(1m, roundedHours);

        return billableHours * hourlyRate;
    }
}
