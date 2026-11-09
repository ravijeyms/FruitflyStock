// ===== Google Sheets data contract =====
// Pseudocode:
// - Accept a selected spreadsheet and its display name.
// - Return mapped FlyStock rows, or surface fetch/parse failures to the caller.
// The implementation obtains the current circuit's access token through IGoogleOAuthService.
// The method is async because it performs remote HTTP requests and supports cancellation.
public interface IGoogleSheetsService
{
	Task<List<FlyStock>> GetStocksAsync(
		string spreadsheetId,
		string spreadsheetName,
		CancellationToken cancellationToken = default);
}
