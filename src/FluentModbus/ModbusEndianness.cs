namespace FluentModbus;

/// <summary>
/// Specifies the endianness of the data.
/// </summary>
public enum ModbusEndianness
{
    /// <summary>
    /// Little endian data layout, i.e. the least significant byte is trasmitted first.
    /// </summary>
    LittleEndian = 1,

    /// <summary>
    /// Big endian data layout, i.e. the most significant byte is trasmitted first.
    /// </summary>
    BigEndian = 2,
}
