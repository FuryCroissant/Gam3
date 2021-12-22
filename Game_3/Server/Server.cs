using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using NJsonSchema.Generation;
using NJsonSchema.Validation;
using Lib;

namespace Server;

class Server
{
    static async Task Main(string[] args)
    {
        try
        {
            await StartServer();
        }
        catch (IOException)
        {
            Console.WriteLine("Disconnect.");
        }
        catch (JsonException)
        {
            Console.WriteLine("Opponent sent message in unexpected format. Game aborted.");
        }
        catch (Exception)
        {
            Console.WriteLine("Unknown error occured.");
            throw;
        }
        finally
        {
            Console.ReadLine();
        }
    }

    static async Task StartServer()
    {
        var settings = new JsonSchemaGeneratorSettings();
        var generator = new JsonSchemaGenerator(settings);
        var schema = generator.Generate(typeof(GameState));

        bool isGameLoaded = false;
        string? gameStateJson = null;
        GameState gameState = new();

        bool isAnswerCorrect = false;
        while (!isAnswerCorrect)
        {
            Console.WriteLine("Do you want to load game from last saved state (y/n)?");
            var answer = Console.ReadLine();

            switch (answer.ToLower())
            {
                case "y"://загрузка сохраненного
                    isAnswerCorrect = true;

                    using (StreamReader streamReader = new("GameState.json")) 
                        gameStateJson = streamReader.ReadToEnd();

                    JsonSchemaValidator validator = new JsonSchemaValidator();
                    var validationErrors = validator.Validate(gameStateJson, schema);
                    if (validationErrors.Count > 0)
                    {
                        Console.WriteLine("Schema validation failed while loading last saved state. Starting game from initial state...");
                        gameStateJson = null;
                        break;
                    }

                    gameState = JsonSerializer.Deserialize<GameState>(gameStateJson);

                    isGameLoaded = true;
                    Console.WriteLine(Phrases.GameLoad);
                    break;
                case "n"://без загрузки
                    isAnswerCorrect = true;
                    Console.WriteLine("Game will be started from the beginning, waiting for client...");
                    break;
                default:
                    Console.WriteLine("Error, please provide valid response.");
                    break;
            }
        }

        IPAddress localAddr = IPAddress.Parse("127.0.0.1");
        var server = new TcpListener(localAddr, port: 1337);
        server.Start();
        TcpClient client = await server.AcceptTcpClientAsync();
        server.Stop();
        Console.WriteLine(Phrases.RightConnect);
        NetworkStream stream = client.GetStream();

        byte[] buffer = new byte[1024];

        if (gameStateJson == null)//сохранение в начале раунда
            gameStateJson = JsonSerializer.Serialize<GameState>(gameState);

        Helpers.WriteToBuffer(gameStateJson, buffer);
        await stream.WriteAsync(buffer, 0, buffer.Length);

        Console.WriteLine("The game is on :)");

        bool SeqCreated = gameState.IsServerCreatingSequence;
        bool FirstTurn = true;
        bool continueGame = true;

        while (continueGame)
        {
            if (SeqCreated)
            {
                if (isGameLoaded && gameState.Sequence != null && FirstTurn)//игра была сохранена после передачи пол-ти
                {
                    FirstTurn = false;
                    goto MidTurn;
                }
                //загадать пос-ть
                Console.Write(Phrases.Request);
                string sequence = Console.ReadLine().ToLower();
                if (sequence == null || sequence.Length != Phrases.SeqLength ||
                    sequence.Any(color => !Phrases.Colors.Contains(char.ToLower(color))))
                {
                    Console.WriteLine(Phrases.Rewrite);
                    continue;
                }
                //отправка посл-ти, сохранение
                Message message = new() { Sequence = sequence };
                string messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);

                SaveGameState(new GameState(SeqCreated, sequence));

                MidTurn:
                //получение от клиента
                Console.WriteLine(Phrases.WaitResult);
                await stream.ReadAsync(buffer, 0, buffer.Length);
                messageJson = Helpers.ReadFromBuffer(buffer);
                Signal? opponentResult = JsonSerializer.Deserialize<Message>(messageJson)?.Signal;

                if (opponentResult == null)
                    throw new JsonException();

                if (opponentResult == Signal.Lost)//выигрыш сервера
                {
                    Console.WriteLine(Phrases.Victory);
                    continueGame = false;
                    continue;
                }

                if (opponentResult == Signal.GotItRight)//поражение
                {
                    Console.WriteLine(Phrases.RightType);
                    SeqCreated = false;
                }

                SaveGameState(new GameState(SeqCreated, null));//новое сохранение "в начале раунда"
            }
            else//загадывает клиент
            {
                string? sequence;

                if (isGameLoaded && gameState.Sequence != null && FirstTurn)//если уже было сохранено
                {
                    sequence = gameState.Sequence;
                    FirstTurn = false;
                    goto MidTurn;
                }
                //получаем посл-ть
                Console.WriteLine(Phrases.WaitSequence);

                await stream.ReadAsync(buffer, 0, buffer.Length);
                string messageJson = Helpers.ReadFromBuffer(buffer);
                sequence = JsonSerializer.Deserialize<Message>(messageJson)?.Sequence;

                if (sequence == null)
                    throw new JsonException();
                //сохраняем
                SaveGameState(new GameState(SeqCreated, sequence));

                MidTurn:

                Console.WriteLine($"Memorize this sequence ({Phrases.MemorizeTime} seconds!): {sequence}");
                Thread.Sleep(Phrases.MemorizeTime * 1000);

                Console.Clear();
                Console.Write(Phrases.RememberType);
                string? recreatedSequence = Console.ReadLine();
                Message message;

                if (recreatedSequence == null || recreatedSequence.ToLower() != sequence)
                {
                    Console.WriteLine(Phrases.Defeat);
                    message = new() { Signal = Signal.Lost };//проигрыш клиента
                    messageJson = JsonSerializer.Serialize(message);
                    Helpers.WriteToBuffer(messageJson, buffer);
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    continueGame = false;
                    continue;
                }

                Console.WriteLine(Phrases.RightType);
                message = new() { Signal = Signal.GotItRight };//выигрыш клиента
                messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);
                SeqCreated = true;

                SaveGameState(new GameState(SeqCreated, null));
            }
        }
    }

    private static void SaveGameState(GameState gameState)
    {
        using StreamWriter streamWriter = new("GameState.json");
        var gameStateJson = JsonSerializer.Serialize(gameState);
        streamWriter.Write(gameStateJson);
    }
}