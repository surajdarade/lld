namespace LoggingService;

public interface ISink
{
    // Writes one complete logical record. The sink must not split a record
    // into uncoordinated writes that can interleave with another record.
    void Write(string data);
}
