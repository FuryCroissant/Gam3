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

        Console.WriteLine(Phrases.RightConnect);

        NetworkStream stream = client.GetStream();

        byte[] buffer = new byte[1024];

        await stream.ReadAsync(buffer, 0, buffer.Length);
        var gameStateJson = Helpers.ReadFromBuffer(buffer);
        var gameState = JsonSerializer.Deserialize<GameState>(gameStateJson);

        bool isGameLoaded = !gameState.IsServerCreatingSequence || gameState.Sequence != null;

        if (isGameLoaded)
            Console.WriteLine(Phrases.GameLoad);

        Console.WriteLine("The game is on :)");

        bool SeqCreated = !gameState.IsServerCreatingSequence;
        bool FirstTurn = true;
        bool continueGame = true;

        while (continueGame)
        {
            if (SeqCreated)
            {
                if (isGameLoaded && gameState.Sequence != null && FirstTurn)
                {
                    FirstTurn = false;
                    goto MidTurn;
                }

                Console.Write(Phrases.Request);
                string? seq = Console.ReadLine().ToLower();
                if (seq == null || seq.Length != Phrases.SeqLength ||
                    seq.Any(color => !Phrases.Colors.Contains(char.ToLower(color))))
                {
                    Console.WriteLine(Phrases.Rewrite);
                    continue;
                }

                Message message = new() { Sequence = seq };
                string messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);

            MidTurn:

                Console.WriteLine(Phrases.WaitResult);
                await stream.ReadAsync(buffer, 0, buffer.Length);
                messageJson = Helpers.ReadFromBuffer(buffer);
                Signal? opponentResult = JsonSerializer.Deserialize<Message>(messageJson)?.Signal;

                if (opponentResult == null)
                    throw new JsonException();

                if (opponentResult == Signal.Lost)
                {
                    Console.WriteLine(Phrases.Victory);
                    continueGame = false;
                    continue;
                }

                if (opponentResult == Signal.GotItRight)
                {
                    Console.WriteLine(Phrases.RightType);
                    SeqCreated = false;
                }
            }
            else
            {
                string? seq;

                if (isGameLoaded && gameState.Sequence != null && FirstTurn)
                {
                    seq = gameState.Sequence;
                    FirstTurn = false;
                    goto MidTurn;
                }

                Console.WriteLine(Phrases.WaitSequence);

                await stream.ReadAsync(buffer, 0, buffer.Length);
                string messageJson = Helpers.ReadFromBuffer(buffer);
                seq = JsonSerializer.Deserialize<Message>(messageJson)?.Sequence;

                if (seq == null)
                    throw new JsonException();

                MidTurn:

                Console.WriteLine($"Memorize this seq ({Phrases.MemorizeTime} seconds!): {seq}");
                Thread.Sleep(Phrases.MemorizeTime * 1000);

                Console.Clear();
                Console.Write(Phrases.RememberType);
                string? recreatedSequence = Console.ReadLine();
                Message message;

                if (recreatedSequence == null || recreatedSequence.ToLower() != seq)
                {
                    Console.WriteLine(Phrases.Defeat);
                    message = new() { Signal = Signal.Lost };
                    messageJson = JsonSerializer.Serialize(message);
                    Helpers.WriteToBuffer(messageJson, buffer);
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    continueGame = false;
                    continue;
                }

                Console.WriteLine(Phrases.RightType);
                message = new() { Signal = Signal.GotItRight };
                messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);
                SeqCreated = true;
            }
        }
    }
}