// ===== Common term calculation =====
// Pseudocode:
// - Flatten each stock's precomputed attributes.
// - Ignore blank terms and group remaining terms without regard to case.
// - Keep each group's original spelling, order by frequency then alphabetically,
//   take the requested count, and return the terms.
// Sealed prevents inheritance from this stateless implementation; callers depend on
// ICommonTermsService, so customization belongs in another registered implementation.
public sealed class CommonTermsService : ICommonTermsService
{
	// Purpose: build deterministic common-term suggestions from the currently linked data.
	public List<string> GetCommonTerms(IEnumerable<FlyStock> stocks, int count = 25)
	{
		return stocks
			.SelectMany(stock => stock.Attributes)
			.Where(term => !string.IsNullOrWhiteSpace(term))
			.GroupBy(
				term => term,
				StringComparer.OrdinalIgnoreCase)
			.Select(group => new
			{
				Term = group.First(),
				Count = group.Count()
			})
			.OrderByDescending(x => x.Count)
			.ThenBy(x => x.Term)
			.Take(count)
			.Select(x => x.Term)
			.ToList();
	}
}