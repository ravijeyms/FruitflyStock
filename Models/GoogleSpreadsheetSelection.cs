// ===== Google Picker selection payload =====
// Pseudocode:
// - Receive the spreadsheet ID and display name sent from the browser Picker.
// - Provide empty defaults so deserialization does not produce null strings.
// The DTO is sealed because it is a simple payload, not a base type or extension point.
// Its setters support JSON deserialization from the JS interop boundary.
public sealed class GoogleSpreadsheetSelection
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
}
