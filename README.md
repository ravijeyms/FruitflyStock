# FruitflyStock

FruitflyStock searches Drosophila stock data in Google Sheets. Before starting
the app, configure a Google Cloud project for OAuth, enable the APIs it uses,
and set up Google Picker credentials.

## 1. Create a Google Cloud project and configure OAuth

1. Sign in to the [Google Cloud Console](https://console.cloud.google.com/) with
   the Google account that will own the project.
2. Create a project (or select an existing one) for FruitflyStock. Keep the
   project handy; the app also needs its numeric project number for Picker.
3. Open **Google Auth Platform** and configure the app's branding and audience.
   For a personal Google account, choose **External** and leave the app in
   **Testing** while setting it up. Add every Google account that will test the
   app as a test user. For a Google Workspace deployment, choose the audience
   appropriate for your organization.
4. In **Data Access**, add the scopes the app requests:
   - `https://www.googleapis.com/auth/drive.file`
   - `https://www.googleapis.com/auth/spreadsheets.readonly`

   Users must grant these scopes when they sign in so the app can show
   spreadsheets in Picker and read their contents.
5. In **Clients**, create an OAuth client ID with application type **Web
   application**. Add the app's origin under **Authorized JavaScript origins**:
   - Local Docker setup: `http://localhost:8080`
   - Deployed app: `https://your-domain`

   Use the origin only: no path or trailing slash. This app uses Google's
   popup-based OAuth flow, so configure JavaScript origins rather than adding a
   redirect URI. Save the client ID and client secret for the environment setup
   below. Keep the client secret private.

   Testing mode only allows listed test users. Before opening an external app
   to general users, publish it and complete Google's OAuth verification if
   required for the configured scopes.

## 2. Enable the Google APIs

In the selected Cloud project, open **APIs & Services → Library** and enable:

- **Google Sheets API** — reads the selected spreadsheets.
- **Google Picker API** — displays the spreadsheet picker in the browser.

The app's Sheets requests use the OAuth access token; the API key configured in
the next step is for Picker.

## 3. Configure Google Picker

1. In **APIs & Services → Credentials**, create an **API key**.
2. Restrict the key:
   - Under **Application restrictions**, choose **Websites** and add the
     browser referrer for the app. For the local Docker setup, add
     `http://localhost:8080/*`. For deployment, add
     `https://your-domain/*`.
   - Under **API restrictions**, choose **Restrict key** and select **Google
     Picker API**.
3. Find the project's **Project number** on the project dashboard. This number
   is Picker's App ID; it is not the project name or project ID.

The API key is used in the browser, so it is not a secret. The website and API
restrictions are important. Never expose the OAuth client secret.

## 4. Add credentials to the app

From the repository root, copy the example environment file:

```sh
cp .env.example .env
```

Fill in the four values in `.env`:

```dotenv
GOOGLE_CLIENT_ID=your-web-oauth-client-id
GOOGLE_CLIENT_SECRET=your-web-oauth-client-secret
GOOGLE_API_KEY=your-restricted-picker-api-key
GOOGLE_APP_ID=your-numeric-google-cloud-project-number
```

Use the OAuth client ID and secret from step 1, the restricted API key from
step 3, and the numeric project number from the same Cloud project. Do not add
quotes around the values. The root `.env` file is ignored by Git; do not commit
it or share its client secret. It is also excluded from the Docker build
context.

## 5. Start the app

Install and start Docker Desktop, then run from the repository root:

```sh
docker compose up --build
```

Open [http://localhost:8080](http://localhost:8080). Docker Compose reads the
four `.env` values and provides them to the server as .NET configuration. All
four values are required.

For a deployed app, serve it over HTTPS and update both the OAuth
**Authorized JavaScript origins** and API-key website restrictions to match
the deployed domain. Keep the OAuth client secret in the server's environment,
not in browser code.

## 6. Use the app

1. Select **Sign in with Google** and approve the requested access.
2. Select **Choose spreadsheets**, pick one or more spreadsheets, and confirm
   the selection.
3. Each spreadsheet's first tab must have the stock columns in the expected
   order in columns A:K. Connected spreadsheets are combined for searching.
4. Enter a search term to find stocks. Use the selection controls to collect
   the desired results.

## Troubleshooting

- **OAuth origin error:** Make sure the exact app origin is listed under the
  OAuth client's **Authorized JavaScript origins**. The local origin includes
  port `8080`.
- **Google sign-in unavailable:** Confirm the client ID is set in `.env` and
  the signed-in Google account is listed as a test user if the consent screen
  is in Testing.
- **Picker fails to load:** Confirm that the Google Picker API is enabled, the
  API key's website restriction matches the browser URL, and its API
  restriction includes Google Picker API. Also check that `GOOGLE_APP_ID` is
  the numeric project number.
- **Spreadsheets cannot be read:** Confirm that the Google Sheets API is
  enabled and that sign-in was granted the requested scopes.
