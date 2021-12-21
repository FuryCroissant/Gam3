namespace Lib;
    
public record Message
{
    public string? Sequence { get; init; }
    public Signal? Signal { get; init; } = null;
}


