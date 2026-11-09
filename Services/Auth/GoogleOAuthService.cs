using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

// ===== Server-side Google OAuth implementation =====
// Pseudocode:
// - Exchange the browser-returned authorization code using the server-only client secret.
// - Keep access/refresh tokens in the circuit-scoped token store, never in configuration
//   sent to the browser or in persistent storage.
// - Before an API call, reuse a token that remains valid; otherwise serialize refresh
//   attempts and request a replacement using the refresh token.
// The service is sealed because the interface is its extension point. Async methods are
// required for Google token-endpoint I/O and cancellation.
public sealed class GoogleOAuthService(
	HttpClient httpClient,
	IOptions<GoogleOptions> options,
	IGoogleTokenStore tokenStore) : IGoogleOAuthService
{
	private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
	private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(1);
	private readonly SemaphoreSlim _refreshLock = new(1, 1);
	private readonly GoogleOptions _options = options.Value;

	// ===== Authorization-code exchange =====
	// Pseudocode:
	// - Require both configured OAuth credentials.
	// - Validate the calling site's popup origin and exchange the one-use code with Google.
	// - Require an access token, compute expiry, save access/refresh tokens in this circuit,
	//   and return only the access token required by the Picker.
	public async Task<string> ExchangeAuthorizationCodeAsync(
		string authorizationCode,
		string redirectUri,
		CancellationToken cancellationToken = default)
	{
		EnsureCredentialsConfigured();

		if (string.IsNullOrWhiteSpace(authorizationCode))
		{
			throw new ArgumentException("An authorization code is required.", nameof(authorizationCode));
		}

		var verifiedRedirectUri = ValidatePopupRedirectUri(redirectUri);
		using var response = await httpClient.PostAsync(
			TokenEndpoint,
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["code"] = authorizationCode,
				["client_id"] = _options.ClientId,
				["client_secret"] = _options.ClientSecret,
				["redirect_uri"] = verifiedRedirectUri,
				["grant_type"] = "authorization_code"
			}),
			cancellationToken);

		var tokenResponse = await ReadTokenResponseAsync(response, cancellationToken);
		var accessToken = RequireAccessToken(tokenResponse);
		var refreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
			? tokenStore.Current?.RefreshToken
			: tokenResponse.RefreshToken;

		tokenStore.Set(new GoogleOAuthTokens(
			accessToken,
			refreshToken,
			DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn)));

		return accessToken;
	}

	// ===== Access-token retrieval and refresh =====
	// Pseudocode:
	// - Fail clearly when this circuit has no signed-in token state.
	// - Return the current access token if it outlives the refresh safety window.
	// - Otherwise acquire a lock, re-check state, refresh once, and save the updated tokens.
	// The lock prevents concurrent requests in one circuit from racing to refresh the same
	// single-use/rotating refresh token.
	public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
	{
		var currentTokens = tokenStore.Current
			?? throw new InvalidOperationException("Sign in to Google before accessing spreadsheets.");

		if (currentTokens.ExpiresAt > DateTimeOffset.UtcNow.Add(RefreshWindow))
		{
			return currentTokens.AccessToken;
		}

		await _refreshLock.WaitAsync(cancellationToken);
		try
		{
			currentTokens = tokenStore.Current
				?? throw new InvalidOperationException("Sign in to Google before accessing spreadsheets.");

			if (currentTokens.ExpiresAt > DateTimeOffset.UtcNow.Add(RefreshWindow))
			{
				return currentTokens.AccessToken;
			}

			if (string.IsNullOrWhiteSpace(currentTokens.RefreshToken))
			{
				tokenStore.Clear();
				throw new InvalidOperationException(
					"Google sign-in expired. Sign in again to continue; no refresh token was issued.");
			}

			var tokenResponse = await RefreshTokensAsync(
				currentTokens.RefreshToken,
				cancellationToken);
			var accessToken = RequireAccessToken(tokenResponse);
			var refreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
				? currentTokens.RefreshToken
				: tokenResponse.RefreshToken;

			tokenStore.Set(new GoogleOAuthTokens(
				accessToken,
				refreshToken,
				DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn)));

			return accessToken;
		}
		finally
		{
			_refreshLock.Release();
		}
	}

	// ===== Refresh request and response validation =====
	// Pseudocode:
	// - Send the refresh grant with the configured server credentials.
	// - Deserialize a successful token response, otherwise report the HTTP status.
	// - Require a non-empty access token before storing or returning it.
	private async Task<GoogleTokenEndpointResponse> RefreshTokensAsync(
		string refreshToken,
		CancellationToken cancellationToken)
	{
		using var response = await httpClient.PostAsync(
			TokenEndpoint,
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["client_id"] = _options.ClientId,
				["client_secret"] = _options.ClientSecret,
				["refresh_token"] = refreshToken,
				["grant_type"] = "refresh_token"
			}),
			cancellationToken);

		return await ReadTokenResponseAsync(response, cancellationToken);
	}

	private static async Task<GoogleTokenEndpointResponse> ReadTokenResponseAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		if (!response.IsSuccessStatusCode)
		{
			throw new HttpRequestException(
				$"Google token request failed (HTTP {(int)response.StatusCode}).",
				null,
				response.StatusCode);
		}

		return await response.Content.ReadFromJsonAsync<GoogleTokenEndpointResponse>(
			cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("Google returned an empty token response.");
	}

	private static string RequireAccessToken(GoogleTokenEndpointResponse response)
	{
		if (string.IsNullOrWhiteSpace(response.AccessToken))
		{
			throw new InvalidOperationException("Google's token response did not include an access token.");
		}

		if (response.ExpiresIn <= 0)
		{
			throw new InvalidOperationException("Google's token response did not include a valid expiry.");
		}

		return response.AccessToken;
	}

	private static string ValidatePopupRedirectUri(string redirectUri)
	{
		if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) ||
			(uri.Scheme != Uri.UriSchemeHttps &&
			 !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
			uri.AbsolutePath != "/" ||
			!string.IsNullOrEmpty(uri.Query) ||
			!string.IsNullOrEmpty(uri.Fragment) ||
			!string.IsNullOrEmpty(uri.UserInfo))
		{
			throw new ArgumentException(
				"OAuth popup redirect URI must be an HTTPS origin (or a localhost HTTP origin).",
				nameof(redirectUri));
		}

		return uri.GetLeftPart(UriPartial.Authority);
	}

	private void EnsureCredentialsConfigured()
	{
		if (string.IsNullOrWhiteSpace(_options.ClientId) ||
			string.IsNullOrWhiteSpace(_options.ClientSecret))
		{
			throw new InvalidOperationException(
				"Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET in the .env file used by Docker Compose.");
		}
	}

	public async Task ClearSessionAsync(CancellationToken cancellationToken = default)
	{
		await _refreshLock.WaitAsync(cancellationToken);
		try
		{
			tokenStore.Clear();
		}
		finally
		{
			_refreshLock.Release();
		}
	}

	private sealed class GoogleTokenEndpointResponse
	{
		[JsonPropertyName("access_token")]
		public string? AccessToken { get; set; }

		[JsonPropertyName("refresh_token")]
		public string? RefreshToken { get; set; }

		[JsonPropertyName("expires_in")]
		public int ExpiresIn { get; set; }
	}
}
