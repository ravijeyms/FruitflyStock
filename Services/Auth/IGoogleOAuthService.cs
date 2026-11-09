// ===== Google OAuth service contract =====
// Pseudocode:
// - Expose the public client ID for starting the browser authorization flow.
// - Exchange a one-use authorization code for tokens and retain them in the current
//   Blazor circuit's token store.
// - Return a valid access token to server-side API clients, refreshing it when needed.
// - Clear the current circuit's token state on sign-out.
public interface IGoogleOAuthService
{
	Task<string> ExchangeAuthorizationCodeAsync(
		string authorizationCode,
		string redirectUri,
		CancellationToken cancellationToken = default);

	Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

	Task ClearSessionAsync(CancellationToken cancellationToken = default);
}
