# FruitflyStock

## Configure and run with Docker Compose

Set these Google values in the root `.env` file:

```dotenv
GOOGLE_CLIENT_ID=your-oauth-client-id
GOOGLE_CLIENT_SECRET=your-oauth-client-secret
GOOGLE_API_KEY=your-google-picker-api-key
GOOGLE_APP_ID=your-google-cloud-project-number
```

`.env` is ignored by Git and excluded from the Docker build context. The
OAuth client secret is passed only to the server container; the Picker API
key is sent to the browser, so restrict it to your app's HTTP referrers and
the required Google APIs.

Build and start the app with `docker compose up --build`, then open
`http://localhost:8080`. Docker Compose passes the values as .NET configuration
environment variables (`Google__ClientId`, `Google__ClientSecret`,
`Google__ApiKey`, and `Google__AppId`). All four values are required.
