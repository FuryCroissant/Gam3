namespace Lib;

public static class ConstantValues
{
    public static readonly char[] AvailableColors = { 'r', 'g', 'b', 'o', 'y', 'p','c', 'w' };

    public static readonly int SequenceLength = 5;
    public static readonly int MemorizeTime = 5;

    public static readonly string ConnectedMessage = "Successfully connected to opponent!";


    public static readonly string RequestMessage =
        $"Type a sequence of {SequenceLength} ball colors (use first letters of: Red, Green, Blue, Orange,Yellow, Purple, Cyan, White): ";

    public static readonly string TypeRememberedMessage = "Remember? Now type!";

    public static readonly string RewriteSequenceMessage =
        "You're typing something wrong.. Try again?";

    public static readonly string WaitForSequenceMessage = "The opponent typing a sequence, strain your brain and get ready!";

    public static readonly string WaitForResultMessage =
        "Wait for the opponent's response";

    public static readonly string TypedRightMessage =
        "The sequence is recreated correctly, the change of roles - the game continues!";

    public static readonly string VictoryMessage = "Your opponent made a mistake - You're the winner!";

    public static readonly string DefeatMessage = "Oh no, you lost Т__Т";

    public static readonly string GameLoadedMessage = "The game was loaded from last saved state.";
}


