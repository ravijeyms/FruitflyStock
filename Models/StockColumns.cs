// ===== Shared stock-column definitions =====
// Pseudocode:
// - Define each displayed column once as a label plus a typed FlyStock value selector.
// - Reuse the ordered list for headers, cells, import mapping, and stable stock keys.
// The typed selector avoids repeating property-name strings or converting between
// differently named JS and C# column descriptions.
public static class StockColumns
{
	// A small immutable definition used to display a column and extract its value.
	public sealed record Column(string Label, Func<FlyStock, string> GetValue);

	// The order here is also the expected A:K spreadsheet layout.
	public static readonly Column[] All =
	[
		new("Label", stock => stock.Label),
		new("Identifier", stock => stock.ShortIdentifier),
		new("Genotype", stock => stock.Genotype),
		new("X", stock => stock.X),
		new("Y", stock => stock.Y),
		new("C2a", stock => stock.C2a),
		new("C2b", stock => stock.C2b),
		new("C3a", stock => stock.C3a),
		new("C3b", stock => stock.C3b),
		new("Extra Info", stock => stock.ExtraInfo),
		new("Mod", stock => stock.Mod)
	];
}