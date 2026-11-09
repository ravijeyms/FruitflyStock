// ===== Google SDK configuration and module lifecycle =====
// Pseudocode:
// - Keep the Google OAuth/Picker URLs and requested scopes together.
// - Cache one shared SDK-loading promise so repeated attempts do not inject duplicates.
// - Remember the access token only in this page lifetime for Picker; server-side OAuth
//   exchanges and refresh-token storage remain in .NET. Public Picker settings are
//   received from GoogleOptions rather than duplicated in JavaScript configuration.
const gisScriptUrl = "https://accounts.google.com/gsi/client";
const pickerScriptUrl = "https://apis.google.com/js/api.js";
const sheetsScope =
    "https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/spreadsheets.readonly";

let disposeHandlers;
let googleServicesPromise;
let googleServicesReady = false;

// ===== Blazor bridge setup =====
// Pseudocode:
// - Find the Blazor-owned sign-in and picker buttons.
// - Define helpers to check configuration and load Google scripts only once.
// - Register click handlers and report missing configuration or SDK load failures to .NET.
// This module remains JavaScript because OAuth popup and Google Picker are browser SDKs.
export function initialize(
    dotNetReference,
    clientId,
    apiKey,
    appId,
    initialOAuthState
) {
    const signInButton = document.getElementById("googleSignIn");
    const pickerButton = document.getElementById("chooseSheets");
    let accessToken;
    let oauthState = initialOAuthState;
    let authInProgress = false;

    function isConfigured() {
        return Boolean(clientId);
    }

    function isPickerConfigured() {
        return Boolean(apiKey && appId);
    }

    // ===== Script loading and authorization-code request =====
    // Pseudocode:
    // - Reuse an already loaded script, wait for an in-progress script, or inject it.
    // - Load Identity Services and the Google API client concurrently, then load Picker.
    // - Ask Google Identity Services for a one-time authorization code.
    // - Send the code to .NET, where the client secret is used to exchange it securely.
    // Promises/async are required because script events, Google's SDK callbacks, and OAuth
    // responses complete later than the initiating click.
    function loadScript(id, url) {
        const existingScript = document.getElementById(id);
        if (existingScript) {
            if (existingScript.dataset.loaded === "true") {
                return Promise.resolve();
            }

            return new Promise((resolve, reject) => {
                existingScript.addEventListener("load", resolve, { once: true });
                existingScript.addEventListener(
                    "error",
                    () => reject(new Error("Unable to load Google services.")),
                    { once: true }
                );
            });
        }

        return new Promise((resolve, reject) => {
            const script = document.createElement("script");
            script.id = id;
            script.src = url;
            script.async = true;
            script.onload = () => {
                script.dataset.loaded = "true";
                resolve();
            };
            script.onerror = () => reject(new Error("Unable to load Google services."));
            document.head.appendChild(script);
        });
    }

    async function loadGoogleServices() {
        if (!googleServicesPromise) {
            googleServicesPromise = (async () => {
                await Promise.all([
                    loadScript("google-identity-services", gisScriptUrl),
                    loadScript("google-api-client", pickerScriptUrl)
                ]);

                await new Promise((resolve, reject) => {
                    window.gapi.load("picker", {
                        callback: resolve,
                        onerror: () => reject(new Error("Unable to load Google Picker."))
                    });
                });
            })().catch(error => {
                googleServicesPromise = undefined;
                throw error;
            });
        }

        await googleServicesPromise;
    }

    function requestAuthorizationCode() {
        return new Promise((resolve, reject) => {
            const codeClient = window.google.accounts.oauth2.initCodeClient({
                client_id: clientId,
                scope: sheetsScope,
                ux_mode: "popup",
                state: oauthState,
                callback: response => {
                    if (response.error) {
                        reject(new Error(response.error_description || response.error));
                        return;
                    }
                    resolve({ code: response.code, state: response.state });
                },
                error_callback: error => {
                    reject(new Error(error.message || "Google sign-in was cancelled."));
                }
            });

            codeClient.requestCode();
        });
    }

    // ===== Picker creation and response forwarding =====
    // Pseudocode:
    // - Configure Picker for spreadsheets and multi-select with the current access token.
    // - Ignore cancellation and report Picker errors.
    // - On a successful pick, send only each document's ID/name to the Blazor component;
    //   the C# service performs the actual Sheets API data fetch.
    function showPicker() {
        const picker = new window.google.picker.PickerBuilder()
            .addView(
                new window.google.picker.DocsView(
                    window.google.picker.ViewId.SPREADSHEETS
                ).setMimeTypes(
                    "application/vnd.google-apps.spreadsheet"
                )
            )
            .enableFeature(window.google.picker.Feature.MULTISELECT_ENABLED)
            .setOAuthToken(accessToken)
            .setDeveloperKey(apiKey)
            .setAppId(appId)
            .setOrigin(window.location.protocol + "//" + window.location.host)
            .setCallback(handlePickerResponseSafely)
            .build();

        picker.setVisible(true);
    }

    async function handlePickerResponse(data) {
        if (data.action === window.google.picker.Action.CANCEL) {
            return;
        }

        if (data.action === window.google.picker.Action.ERROR) {
            console.error("Google Picker error response:", data);
            await updateStatus(
                data.error?.message ||
                "Google Picker could not load the selected spreadsheets. Check the browser console and Google Cloud API/key restrictions."
            );
            return;
        }

        if (data.action !== window.google.picker.Action.PICKED) {
            return;
        }

        await dotNetReference.invokeMethodAsync(
            "OnSpreadsheetsPicked",
            (data.docs ?? []).map(document => ({
                id: document.id,
                name: document.name
            }))
        );
    }

    // ===== Sign-in, sign-out, and Picker actions =====
    // Pseudocode:
    // - If already signed in, notify .NET to clear the circuit token and linked data.
    // - Otherwise validate configuration and SDK readiness, request a one-use code,
    //   and let .NET exchange it and return a Picker access token.
    // - Open Picker only with a token; report operational errors through the status callback.
    // These handlers are async because token acquisition and Blazor JS interop are asynchronous.
    async function handleSignIn() {
        if (accessToken) {
            await dotNetReference.invokeMethodAsync("OnGoogleSignedOut");
            accessToken = undefined;
            return;
        }

        if (!isConfigured()) {
            await updateStatus(
                "Google sign-in needs GOOGLE_CLIENT_ID configured in the app's .env file."
            );
            return;
        }

        if (!googleServicesReady) {
            loadGoogleServices()
                .then(async () => {
                    googleServicesReady = true;
                    await updateStatus("Google services loaded. Sign in to continue.");
                })
                .catch(async error => {
                    console.error(error);
                    await updateStatus(`Unable to load Google services: ${error.message}`);
                });
            await updateStatus("Google services are still loading. Try again shortly.");
            return;
        }

        if (authInProgress) {
            return;
        }

        authInProgress = true;
        try {
            const authorization = await requestAuthorizationCode();
            const session = await dotNetReference.invokeMethodAsync(
                "OnGoogleAuthorizationCode",
                authorization.code,
                authorization.state
            );
            accessToken = session.accessToken;
            oauthState = session.state;
        } catch (error) {
            console.error(error);
            await updateStatus(`Google sign-in failed: ${error.message}`);
        } finally {
            authInProgress = false;
        }
    }

    async function handleChooseSheets() {
        if (!accessToken) {
            return;
        }

        if (!isPickerConfigured()) {
            await updateStatus(
                "Google Picker requires GOOGLE_API_KEY and GOOGLE_APP_ID in the app's .env file."
            );
            return;
        }

        if (!googleServicesReady) {
            loadGoogleServices()
                .then(async () => {
                    googleServicesReady = true;
                    await updateStatus("Google services loaded. Choose spreadsheets to continue.");
                })
                .catch(async error => {
                    console.error(error);
                    await updateStatus(`Unable to load Google services: ${error.message}`);
                });
            await updateStatus("Google services are still loading. Try again shortly.");
            return;
        }

        try {
            accessToken = await dotNetReference.invokeMethodAsync("GetPickerAccessToken");
            showPicker();
        } catch (error) {
            console.error(error);
            await updateStatus(`Unable to open Google Picker: ${error.message}`);
        }
    }

    // ===== Error forwarding and event cleanup =====
    // Pseudocode:
    // - Convert rejected Picker/button promises into logged errors plus visible status updates.
    // - Attach one click handler to each Blazor button.
    // - On disposal, remove exactly those handlers to prevent stale callbacks.
    async function updateStatus(message) {
        await dotNetReference.invokeMethodAsync("OnGoogleStatusChanged", message);
    }

    const handlePickerResponseSafely = data => {
        handlePickerResponse(data).catch(async error => {
            console.error(error);
            await updateStatus(`Unable to connect selected spreadsheets: ${error.message}`);
        });
    };

    const handleSignInSafely = () => {
        handleSignIn().catch(error => {
            console.error(error);
        });
    };

    const handleChooseSheetsSafely = () => {
        handleChooseSheets().catch(error => {
            console.error(error);
        });
    };

    signInButton.addEventListener("click", handleSignInSafely);
    pickerButton.addEventListener("click", handleChooseSheetsSafely);
    disposeHandlers = () => {
        signInButton.removeEventListener("click", handleSignInSafely);
        pickerButton.removeEventListener("click", handleChooseSheetsSafely);
    };

    if (!isConfigured()) {
        updateStatus(
            "Set GOOGLE_CLIENT_ID in the app's .env file to enable sign-in."
        ).catch(error => console.error(error));
    } else if (!isPickerConfigured()) {
        updateStatus(
            "Google sign-in is configured, but Picker needs GOOGLE_API_KEY and GOOGLE_APP_ID in the app's .env file."
        ).catch(error => console.error(error));
    } else {
        loadGoogleServices()
            .then(() => {
                googleServicesReady = true;
            })
            .catch(error => {
                console.error(error);
                return updateStatus(`Unable to load Google services: ${error.message}`);
            })
            .catch(error => console.error(error));
    }
}

export function dispose() {
    disposeHandlers?.();
    disposeHandlers = undefined;
}
