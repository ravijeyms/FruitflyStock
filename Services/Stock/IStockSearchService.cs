// ===== Stock search contract =====
// Pseudocode:
// - Accept available stocks and a query string.
// - Return only stocks matching every query term.
// The interface keeps search rules replaceable and independent from Blazor components.
public interface IStockSearchService
{
	List<FlyStock> Search(IEnumerable<FlyStock> stocks, string query);
}
