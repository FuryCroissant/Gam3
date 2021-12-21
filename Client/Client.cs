using System.Net.Sockets;
using System.Text.Json;
using Lib;

namespace Client;

class Client
{
    static async Task Main(string[] args)
    {
        try
        {
            while (true)
            {
                try
                {
                    await StartClient();
                }
                catch (SocketException)
                {
                    Console.WriteLine("Connection to server failed. Reconnecting in 5 seconds...");
                    Thread.Sleep(5000);
                    continue;
                }

                break;
            }
        }
        catch (IOException)
        {
            Console.WriteLine("Opponent disconnected. Game aborted.");
        }
        catch (JsonException)
        {
            Console.WriteLine("Opponent sent message in unexpected format. Game aborted.");
        }
        catch (Exception)
        {
            Console.WriteLine("Unknown error occured. Sorry, call administrator or something :(");
            throw;
        }
        finally
        {
            Console.ReadLine();
        }
    }

    static async Task StartClient()
    {
        string localAddr = "127.0.0.1";
        var client = new TcpClient();
        while (!client.Connected)
        {
            await client.ConnectAsync(localAddr, port: 1337);
            Thread.Sleep(100);
        }

        Console.WriteLine(ConstantValues.ConnectedMessage);

        NetworkStream stream = client.GetStream();

        byte[] buffer = new byte[1024];

        await stream.ReadAsync(buffer, 0, buffer.Length);
        var gameStateJson = Helpers.ReadFromBuffer(buffer);
        var gameState = JsonSerializer.Deserialize<GameState>(gameStateJson);

        // TODO check default working
        bool isGameLoaded = !gameState.IsServerCreatingSequence || gameState.Sequence != null;

        if (isGameLoaded)
            Console.WriteLine(ConstantValues.GameLoadedMessage);

        Console.WriteLine("The game is on :)");

        bool isCreatingSequence = !gameState.IsServerCreatingSequence;
        bool isFirstTurn = true;
        bool continueGame = true;

        while (continueGame)
        {
            if (isCreatingSequence)
            {
                if (isGameLoaded && gameState.Sequence != null && isFirstTurn)
                {
                    isFirstTurn = false;
                    goto MidTurn;
                }

                Console.Write(ConstantValues.RequestMessage);
                string? sequence = Console.ReadLine().ToLower();
                if (sequence == null || sequence.Length != ConstantValues.SequenceLength ||
                    sequence.Any(color => !ConstantValues.AvailableColors.Contains(char.ToLower(color))))
                {
                    Console.WriteLine(ConstantValues.RewriteSequenceMessage);
                    continue;
                }

                Message message = new() { Sequence = sequence };
                string messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);

            MidTurn:

                Console.WriteLine(ConstantValues.WaitForResultMessage);
                await stream.ReadAsync(buffer, 0, buffer.Length);
                messageJson = Helpers.ReadFromBuffer(buffer);
                Signal? opponentResult = JsonSerializer.Deserialize<Message>(messageJson)?.Signal;

                if (opponentResult == null)
                    throw new JsonException();

                if (opponentResult == Signal.Lost)
                {
                    Console.WriteLine(ConstantValues.VictoryMessage);
                    continueGame = false;
                    continue;
                }

                if (opponentResult == Signal.GotItRight)
                {
                    Console.WriteLine(ConstantValues.TypedRightMessage);
                    isCreatingSequence = false;
                }
            }
            else
            {
                string? sequence;

                if (isGameLoaded && gameState.Sequence != null && isFirstTurn)
                {
                    sequence = gameState.Sequence;
                    isFirstTurn = false;
                    goto MidTurn;
                }

                Console.WriteLine(ConstantValues.WaitForSequenceMessage);

                await stream.ReadAsync(buffer, 0, buffer.Length);
                string messageJson = Helpers.ReadFromBuffer(buffer);
                sequence = JsonSerializer.Deserialize<Message>(messageJson)?.Sequence;

                if (sequence == null)
                    throw new JsonException();

                MidTurn:

                Console.WriteLine($"Memorize this sequence ({ConstantValues.MemorizeTime} seconds!): {sequence}");
                Thread.Sleep(ConstantValues.MemorizeTime * 1000);

                Console.Clear();
                Console.Write(ConstantValues.TypeRememberedMessage);
                string? recreatedSequence = Console.ReadLine();
                Message message;

                if (recreatedSequence == null || recreatedSequence.ToLower() != sequence)
                {
                    Console.WriteLine(ConstantValues.DefeatMessage);
                    message = new() { Signal = Signal.Lost };
                    messageJson = JsonSerializer.Serialize(message);
                    Helpers.WriteToBuffer(messageJson, buffer);
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    continueGame = false;
                    continue;
                }

                Console.WriteLine(ConstantValues.TypedRightMessage);
                message = new() { Signal = Signal.GotItRight };
                messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);
                isCreatingSequence = true;
            }
        }
    }
}