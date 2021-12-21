namespace Lib;

public static class ConstantValues
{
    public static readonly char[] AvailableColors = { 'r', 'g', 'b', 'o', 'y', 'p', 'c', 'w' };

    public static readonly int SeqLength = 5;
    public static readonly int MemorizeTime = 5;

    public static readonly string RightConnect = "Successfully connected to opponent!";


    public static readonly string Request =
        $"Type a sequence of {SeqLength} ball colors (use first letters of: Red, Green, Blue, Orange,Yellow, Purple, Cyan, White): ";

    public static readonly string RememberType = "Remember? Now type!";

    public static readonly string Rewrite =
        "You're typing something wrong.. Try again?";

    public static readonly string WaitSequence = "The opponent typing a sequence, strain your brain and get ready!";

    public static readonly string WaitResult =
        "Wait for the opponent's response";

    public static readonly string RightType =
        "The sequence is recreated correctly, the change of roles - the game continues!";

    public static readonly string Victory = "Your opponent made a mistake - You're the winner!";

    public static readonly string Defeat = "Oh no, you lost Т__Т";

    public static readonly string GameLoad = "The game was loaded from last saved state.";
}


