using Microsoft.AspNetCore.SignalR;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace BookKeepingWeb
{
    public class ChatHub : Hub
    {
        private readonly string _logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "chatlog.txt");

        public ChatHub()
        {
            // Ensure the file exists
            if (!File.Exists(_logFilePath))
            {
                File.Create(_logFilePath).Dispose(); // Create and immediately close the file
            }
        }

        public async Task SendMessage(string user, string message)
        {
            var logEntry = new { User = user, Message = message };
            var logLine = JsonSerializer.Serialize(logEntry);

            await File.AppendAllTextAsync(_logFilePath, logLine + "\n");

            var logLines = await File.ReadAllLinesAsync(_logFilePath);
            if (logLines.Length > 100)
            {
                await File.WriteAllLinesAsync(_logFilePath, logLines[^100..]); // Keep the last 50 lines
            }

            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public override async Task OnConnectedAsync()
        {
            if (File.Exists(_logFilePath))
            {
                var logLines = await File.ReadAllLinesAsync(_logFilePath);
                foreach (var line in logLines)
                {
                    try
                    {
                        // Deserialize to a strongly-typed object
                        var logEntry = JsonSerializer.Deserialize<ChatLogEntry>(line);
                        string user = logEntry?.User ?? "Unknown";
                        string message = logEntry?.Message ?? "";

                        // Send the message to the connected client
                        await Clients.Caller.SendAsync("ReceiveMessage", user, message);
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Failed to deserialize log entry: {ex.Message}");
                    }
                }
            }

            await base.OnConnectedAsync();
        }

        // Define a model for the log entry
        public class ChatLogEntry
        {
            public string User { get; set; }
            public string Message { get; set; }
        }
    }
}
