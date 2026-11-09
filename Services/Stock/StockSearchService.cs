// ===== Stock query matching =====
// Pseudocode:
// - Split the query into non-empty terms separated by commas or whitespace.
// - Keep a stock only when each term appears in at least one of its attributes,
//   comparing without regard to case.
// Sealed prevents accidental subclassing of the stateless implementation; callers use
// IStockSearchService so alternative behavior can be supplied through dependency injection.
public sealed class StockSearchService : IStockSearchService
{
	// Purpose: apply AND semantics across terms and OR semantics across a stock's attributes.
	public List<FlyStock> Search(
		IEnumerable<FlyStock> stocks,
		string query)
	{
		var terms = query
			.Split(
				new[] { ',', ' ', '\t', '\n' },
				StringSplitOptions.RemoveEmptyEntries)
			.Select(x => x.Trim())
			.ToList();

		return stocks
			.Where(stock =>
				terms.All(term => Matches(stock, term)))
			.ToList();
	}

	// Check one term against all precomputed attributes of a stock.
	private static bool Matches(
		FlyStock stock,
		string term)
	{
		return stock.Attributes.Any(
			x => x.Contains(
				term,
				StringComparison.OrdinalIgnoreCase));
	}
}