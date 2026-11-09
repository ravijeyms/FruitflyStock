// ===== Circuit token-store contract =====
// Pseudocode:
// - Expose the token set currently associated with this scoped Blazor circuit.
// - Replace it atomically after authorization or refresh.
// - Clear it when the user signs out.
// This store is intentionally scoped/in-memory, not durable; a new circuit requires sign-in.
public interface IGoogleTokenStore
{
	GoogleOAuthTokens? Current { get; }

	void Set(GoogleOAuthTokens tokens);

	void Clear();
}

public sealed record GoogleOAuthTokens(
	string AccessToken,
	string? RefreshToken,
	DateTimeOffset ExpiresAt);
