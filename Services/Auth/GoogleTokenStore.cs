// ===== In-memory circuit token store =====
// Pseudocode:
// - Hold one immutable token snapshot for the current scoped service instance.
// - Replace the whole snapshot when OAuth exchanges or refreshes tokens.
// - Drop the snapshot when the user signs out.
// Sealed prevents inheritance from this minimal store; DI injects IGoogleTokenStore so
// a durable encrypted implementation can be substituted later without changing callers.
public sealed class GoogleTokenStore : IGoogleTokenStore
{
	public GoogleOAuthTokens? Current { get; private set; }

	public void Set(GoogleOAuthTokens tokens) => Current = tokens;

	public void Clear() => Current = null;
}
