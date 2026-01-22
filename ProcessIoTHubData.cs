using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace IotTelemetryFunction
{
    public class ProcessIoTHubData
    {
        private readonly ILogger _logger;

        public ProcessIoTHubData(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessIoTHubData>();
        }

        [Function("ProcessIoTHubData")]
        [CosmosDBOutput(
            databaseName: "IotDatabase",
            containerName: "TelemetryData",
            Connection = "CosmosDBConnectionString")]
        public List<TelemetryDocument> Run(
            [EventHubTrigger("messages/events", Connection = "IoTHubConnectionString")] string[] messages,
            FunctionContext context)
        {
            var flattenedDocuments = new List<TelemetryDocument>();

            foreach (var messageBody in messages)
            {
                try
                {
                    _logger.LogDebug($"Processing message: {messageBody}");

                    using var jsonDoc = JsonDocument.Parse(messageBody);
                    var root = jsonDoc.RootElement;

                    // The IoT Hub message body sent by your Edge module is an array of measurements
                    // Note: If your Edge module sends the array directly, it might not have the "Body" wrapper 
                    // unless it's coming through a specific routing. Let's handle both cases.
                    
                    JsonElement measurementsArray;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        measurementsArray = root;
                    }
                    else if (root.TryGetProperty("Body", out var bodyProp))
                    {
                        measurementsArray = bodyProp;
                    }
                    else
                    {
                        _logger.LogWarning("Message format not recognized. Expected array or object with 'Body' property.");
                        continue;
                    }

                    foreach (var element in measurementsArray.EnumerateArray())
                    {
                        var doc = new TelemetryDocument
                        {
                            id = Guid.NewGuid().ToString(),
                            NodeName = element.GetProperty("NodeName").GetString() ?? "Unknown",
                            MeasurementType = element.GetProperty("MeasurementType").GetString() ?? "Unknown",
                            Value = element.GetProperty("Value").GetDouble(),
                            Timestamp = element.GetProperty("Timestamp").GetDateTime(),
                            Source = element.GetProperty("Source").GetString() ?? "Unknown",
                            ProcessedAt = DateTime.UtcNow
                        };

                        flattenedDocuments.Add(doc);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error parsing IoT Hub message: {ex.Message}");
                }
            }

            _logger.LogInformation($"Successfully processed and flattened {flattenedDocuments.Count} measurements.");
            return flattenedDocuments;
        }
    }

    public class TelemetryDocument
    {
        public string id { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty; // Partition Key
        public string MeasurementType { get; set; } = string.Empty;
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}