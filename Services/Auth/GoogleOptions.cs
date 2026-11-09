// ===== Google configuration binding =====
// Pseudocode:
// - Bind OAuth credentials and Picker's public configuration from the Google section.
// - Keep ClientSecret server-only; only ClientId, ApiKey, and AppId may cross to the browser.
// Configure the API key with HTTP-referrer and API restrictions in Google Cloud.
public sealed class GoogleOptions
{
	public string ClientId { get; set; } = "";

	public string ClientSecret { get; set; } = "";

	public string ApiKey { get; set; } = "";

	public string AppId { get; set; } = "";
}
