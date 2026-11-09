using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

// ===== Google Sheets HTTP implementation =====
// Pseudocode:
// - Obtain the current circuit's access token through IGoogleOAuthService.
// - Request spreadsheet metadata and values with that bearer token.
// - Identify the first worksheet and request columns A:K.
// - Skip a recognized header, map non-empty rows into FlyStock, and build searchable attributes.
// - Dispose HTTP responses and JSON documents as soon as the mapped rows are ready.
// Sealed keeps this concrete implementation closed to inheritance; callers use the
// IGoogleSheetsService contract. Async/await keeps remote I/O non-blocking and propagates
// cancellation through requests and JSON reads.
public sealed class GoogleSheetsService(
	HttpClient httpClient,
	IGoogleOAuthService googleOAuthService) : IGoogleSheetsService
{
	private const string SheetsApiUrl = "https://sheets.googleapis.com/v4/spreadsheets";

	// Purpose: download and convert one selected sheet into the app's stock model.
	public async Task<List<FlyStock>> GetStocksAsync(
		string spreadsheetId,
		string spreadsheetName,
		CancellationToken cancellationToken = default)
	{
		var accessToken = await googleOAuthService.GetAccessTokenAsync(cancellationToken);
		var spreadsheetUrl = $"{SheetsApiUrl}/{Uri.EscapeDataString(spreadsheetId)}";
		using var metadataResponse = await SendAsync(
			$"{spreadsheetUrl}?fields=sheets.properties.title",
			spreadsheetName,
			"open",
			accessToken,
			cancellationToken);
		var metadata = await metadataResponse.Content.ReadFromJsonAsync<SpreadsheetMetadata>(
			cancellationToken: cancellationToken);
		var firstSheetName = metadata?.Sheets?.FirstOrDefault()?.Properties?.Title;

		if (string.IsNullOrWhiteSpace(firstSheetName))
		{
			throw new InvalidOperationException(
				$"\"{spreadsheetName}\" does not contain a worksheet.");
		}

		var range = Uri.EscapeDataString($"{firstSheetName}!A:K");
		using var valuesResponse = await SendAsync(
			$"{spreadsheetUrl}/values/{range}",
			spreadsheetName,
			"read data from",
			accessToken,
			cancellationToken);
		using var valuesDocument = await JsonDocument.ParseAsync(
			await valuesResponse.Content.ReadAsStreamAsync(cancellationToken),
			cancellationToken: cancellationToken);

		if (!valuesDocument.RootElement.TryGetProperty("values", out var rows) ||
			rows.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		var firstRow = rows.EnumerateArray().FirstOrDefault();
		var rowIndex = IsHeaderRow(firstRow) ? 1 : 0;
		var stocks = new List<FlyStock>();
		var currentIndex = 0;

		foreach (var row in rows.EnumerateArray())
		{
			if (currentIndex++ < rowIndex || row.ValueKind != JsonValueKind.Array)
			{
				continue;
			}

			var stock = new FlyStock
			{
				Label = Cell(row, 0),
				ShortIdentifier = Cell(row, 1),
				Genotype = Cell(row, 2),
				X = Cell(row, 3),
				Y = Cell(row, 4),
				C2a = Cell(row, 5),
				C2b = Cell(row, 6),
				C3a = Cell(row, 7),
				C3b = Cell(row, 8),
				ExtraInfo = Cell(row, 9),
				Mod = Cell(row, 10)
			};

			if (StockColumns.All.Any(column =>
				!string.IsNullOrWhiteSpace(column.GetValue(stock))))
			{
				stock.BuildAttributes();
				stocks.Add(stock);
			}
		}

		return stocks;
	}

	// ===== Authenticated request and failure handling =====
	// Pseudocode:
	// - Create a GET request with the supplied bearer token.
	// - Send it using the injected, factory-managed HttpClient.
	// - Return successful responses to the caller; turn HTTP failures into an exception
	//   that names the operation, spreadsheet, and status code.
	// The method is async because network I/O is asynchronous; request and response
	// lifetimes are scoped so their resources are released on success or failure.
	private async Task<HttpResponseMessage> SendAsync(
		string requestUrl,
		string spreadsheetName,
		string action,
		string accessToken,
		CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

		var response = await httpClient.SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			var statusCode = response.StatusCode;
			response.Dispose();
			throw new HttpRequestException(
				$"Unable to {action} \"{spreadsheetName}\" (HTTP {(int)statusCode}).",
				null,
				statusCode);
		}

		return response;
	}

	// ===== Row parsing and header detection =====
	// Pseudocode:
	// - Return an empty string when a row omits the requested column.
	// - Convert JSON strings directly and other scalar values using their textual form.
	// - Consider the first row a header only when it contains both required headings.
	private static string Cell(JsonElement row, int index)
	{
		if (index >= row.GetArrayLength())
		{
			return "";
		}

		var cell = row[index];
		return cell.ValueKind switch
		{
			JsonValueKind.Null or JsonValueKind.Undefined => "",
			JsonValueKind.String => cell.GetString() ?? "",
			_ => cell.ToString()
		};
	}

	private static bool IsHeaderRow(JsonElement row)
	{
		if (row.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		var headers = row.EnumerateArray()
			.Select(cell => cell.ToString().Trim());
		var headerSet = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
		return headerSet.Contains("label") && headerSet.Contains("genotype");
	}

	// ===== Metadata deserialization shapes =====
	// Pseudocode:
	// - Map the API's nested sheets -> properties -> title JSON shape into typed objects.
	// These private sealed classes mirror only the response fields needed by this service;
	// sealing makes clear they are local data-transfer shapes, not extension points.
	private sealed class SpreadsheetMetadata
	{
		[JsonPropertyName("sheets")]
		public List<SpreadsheetInfo>? Sheets { get; set; }
	}

	private sealed class SpreadsheetInfo
	{
		[JsonPropertyName("properties")]
		public SheetProperties? Properties { get; set; }
	}

	private sealed class SheetProperties
	{
		[JsonPropertyName("title")]
		public string? Title { get; set; }
	}
}
