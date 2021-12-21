using System.ComponentModel.DataAnnotations;

namespace Lib;

public record GameState
{
    public GameState() { }
    public GameState(bool isServerCreatingSequence, string? sequence)
    {
        IsServerCreatingSequence = isServerCreatingSequence;
        Sequence = sequence;
    }

    public bool IsServerCreatingSequence { get; init; } = true;

    [RegularExpression("^[rgbpyc]{5}$")]
    public string? Sequence { get; init; }
}
