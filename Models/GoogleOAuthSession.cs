// ===== OAuth result returned to the browser bridge =====
// Pseudocode:
// - Return the short-lived access token needed by Google Picker.
// - Return a newly generated state value for the next authorization attempt.
// The refresh token stays in the server-side, circuit-scoped token store.
public sealed record GoogleOAuthSession(string AccessToken, string State);
