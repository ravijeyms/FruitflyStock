// ===== Common-term service contract =====
// Pseudocode:
// - Accept the stocks currently available to the caller.
// - Return up to the requested number of frequently occurring terms.
// The interface separates term-generation policy from UI components and allows a
// different implementation to be substituted in tests or future application layers.
public interface ICommonTermsService
{
	List<string> GetCommonTerms(IEnumerable<FlyStock> stocks, int count = 25);
}
