# iot-telemetry-function

Azure Functions (isolated worker) that ingests IoT Hub Event Hub messages, flattens measurement arrays, and stores each measurement as a separate document in Cosmos DB.

## What it does
- Listens to IoT Hub via the built-in Event Hub-compatible endpoint (`messages/events`).
- Accepts messages either as an array payload or as an object with a `Body` array property.
- Flattens each measurement into an individual document and writes to Cosmos DB (`IotDatabase` / `TelemetryData`).
- Uses `NodeName` as the logical partition key; ensure the Cosmos container is created with `/NodeName`.

## Prerequisites
- .NET 8 SDK or later (project targets `net10.0` isolated worker).
- Azure Functions Core Tools (for local runs).
- Access to an IoT Hub connection string with listen rights for the Event Hub-compatible endpoint.
- Access to a Cosmos DB SQL API account and a container named `TelemetryData` in database `IotDatabase` with partition key `/NodeName`.

## Configure local settings
Create or edit `local.settings.json` (not checked in) with your own values:

```json
{
	"IsEncrypted": false,
	"Values": {
		"AzureWebJobsStorage": "<storage-connection-string>",
		"FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
		"IoTHubConnectionString": "<iot-hub-event-hub-compatible-connection-string>",
		"CosmosDBConnectionString": "<cosmos-db-connection-string>"
	}
}
```

## Message format expected from IoT Hub
The function supports either of the following shapes:

1) Array payload (recommended):

```json
[
	{
		"NodeName": "node-1",
		"MeasurementType": "temperature",
		"Value": 21.5,
		"Timestamp": "2024-05-20T12:34:56Z",
		"Source": "edge-module"
	}
]
```

2) Object with a `Body` array:

```json
{
	"Body": [
		{
			"NodeName": "node-1",
			"MeasurementType": "temperature",
			"Value": 21.5,
			"Timestamp": "2024-05-20T12:34:56Z",
			"Source": "edge-module"
		}
	]
}
```

Each element is transformed to a document with fields: `id` (GUID), `NodeName`, `MeasurementType`, `Value`, `Timestamp`, `Source`, and `Processed` (UTC).

## Running locally
1) Restore and build: `dotnet build`.
2) Start the Functions host from the repo root: `func host start` (or run the VS Code task `func: 4`).
3) Send test Event Hub messages in one of the supported formats.
4) Verify Cosmos DB receives documents in `IotDatabase` / `TelemetryData`.

## Deployment
- Build and publish for Release: `dotnet publish --configuration Release` (task: `publish (functions)`).
- Deploy the published output under `bin/Release/net10.0/publish` to your Function App.
- Configure application settings in Azure to match the values in `local.settings.json` (never deploy secrets from the local file).

## Observability
- Application Insights is enabled via `AddApplicationInsightsTelemetryWorkerService()` with sampling; live metrics filters are on. Review host.json for logging settings.
- Each invocation logs debug messages per received payload and an informational count of flattened measurements.

## Notes and recommendations
- Keep IoT messages small; Cosmos DB items have a 2 MB limit.
- Use high-cardinality partition keys (here `/NodeName`) to avoid hot partitions.
- Handle 429 (throttling) gracefully in downstream consumers; the SDK retries by default, but monitor RU consumption and adjust throughput as needed.
