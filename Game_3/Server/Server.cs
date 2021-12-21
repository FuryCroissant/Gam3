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
            Console.WriteLine("Opponent disconnected. Game aborted.");
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
                    Console.WriteLine(ConstantValues.GameLoad);
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
        Console.WriteLine(ConstantValues.RightConnect);
        NetworkStream stream = client.GetStream();

        byte[] buffer = new byte[1024];

        if (gameStateJson == null)//сохранение в начале раунда
            gameStateJson = JsonSerializer.Serialize<GameState>(gameState);

        Helpers.WriteToBuffer(gameStateJson, buffer);
        await stream.WriteAsync(buffer, 0, buffer.Length);

        Console.WriteLine("The game is on :)");

        bool isCreatingSequence = gameState.IsServerCreatingSequence;
        bool isFirstTurn = true;
        bool continueGame = true;

        while (continueGame)
        {
            if (isCreatingSequence)
            {
                if (isGameLoaded && gameState.Sequence != null && isFirstTurn)//игра была сохранена после передачи пол-ти
                {
                    isFirstTurn = false;
                    goto MidTurn;
                }
                //загадать пос-ть
                Console.Write(ConstantValues.Request);
                string sequence = Console.ReadLine().ToLower();
                if (sequence == null || sequence.Length != ConstantValues.SeqLength ||
                    sequence.Any(color => !ConstantValues.AvailableColors.Contains(char.ToLower(color))))
                {
                    Console.WriteLine(ConstantValues.Rewrite);
                    continue;
                }
                //отправка посл-ти, сохранение
                Message message = new() { Sequence = sequence };
                string messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);

                SaveGameState(new GameState(isCreatingSequence, sequence));

                MidTurn:
                //получение от клиента
                Console.WriteLine(ConstantValues.WaitResult);
                await stream.ReadAsync(buffer, 0, buffer.Length);
                messageJson = Helpers.ReadFromBuffer(buffer);
                Signal? opponentResult = JsonSerializer.Deserialize<Message>(messageJson)?.Signal;

                if (opponentResult == null)
                    throw new JsonException();

                if (opponentResult == Signal.Lost)//выигрыш сервера
                {
                    Console.WriteLine(ConstantValues.Victory);
                    continueGame = false;
                    continue;
                }

                if (opponentResult == Signal.GotItRight)//поражение
                {
                    Console.WriteLine(ConstantValues.RightType);
                    isCreatingSequence = false;
                }

                SaveGameState(new GameState(isCreatingSequence, null));//новое сохранение "в начале раунда"
            }
            else//загадывает клиент
            {
                string? sequence;

                if (isGameLoaded && gameState.Sequence != null && isFirstTurn)//если уже было сохранено
                {
                    sequence = gameState.Sequence;
                    isFirstTurn = false;
                    goto MidTurn;
                }
                //получаем посл-ть
                Console.WriteLine(ConstantValues.WaitSequence);

                await stream.ReadAsync(buffer, 0, buffer.Length);
                string messageJson = Helpers.ReadFromBuffer(buffer);
                sequence = JsonSerializer.Deserialize<Message>(messageJson)?.Sequence;

                if (sequence == null)
                    throw new JsonException();
                //сохраняем
                SaveGameState(new GameState(isCreatingSequence, sequence));

                MidTurn:

                Console.WriteLine($"Memorize this sequence ({ConstantValues.MemorizeTime} seconds!): {sequence}");
                Thread.Sleep(ConstantValues.MemorizeTime * 1000);

                Console.Clear();
                Console.Write(ConstantValues.RememberType);
                string? recreatedSequence = Console.ReadLine();
                Message message;

                if (recreatedSequence == null || recreatedSequence.ToLower() != sequence)
                {
                    Console.WriteLine(ConstantValues.Defeat);
                    message = new() { Signal = Signal.Lost };//проигрыш клиента
                    messageJson = JsonSerializer.Serialize(message);
                    Helpers.WriteToBuffer(messageJson, buffer);
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    continueGame = false;
                    continue;
                }

                Console.WriteLine(ConstantValues.RightType);
                message = new() { Signal = Signal.GotItRight };//выигрыш клиента
                messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);
                isCreatingSequence = true;

                SaveGameState(new GameState(isCreatingSequence, null));
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