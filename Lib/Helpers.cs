namespace Lib;

public static class Helpers
{
    public static void WriteToBuffer(string message, byte[] buffer)
    {
        for (int i = 0; i < message.Length; i++)
            buffer[i] = (byte)message[i];

        for (int i = message.Length; i < buffer.Length; i++)
            buffer[i] = default;
    }

    public static string ReadFromBuffer(byte[] buffer)
    {
        string message = string.Empty;

        for (int i = 0; buffer[i] != default; i++)
            message += (char)buffer[i];

        return message;
    }
}

