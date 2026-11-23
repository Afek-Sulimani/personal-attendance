export class ApiRequestError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    // Preserve the HTTP status so pages can distinguish expired login from other API failures.
    super(message);
    this.name = "ApiRequestError";
  }
}

type ApiRequestOptions = {
  method?: "GET" | "POST" | "PUT";
  body?: unknown;
  antiforgery?: boolean;
  signal?: AbortSignal;
};

async function readResponseBody(response: Response): Promise<unknown> {
  // Treat a 204 response as empty rather than trying to parse a body that cannot exist.
  if (response.status === 204) {
    return undefined;
  }

  // Read once as text so both JSON and plain-text proxy errors can be handled.
  const body = await response.text();
  if (!body) {
    return undefined;
  }

  // Prefer structured JSON for API contracts but keep unstructured server errors readable.
  try {
    return JSON.parse(body) as unknown;
  } catch {
    return body;
  }
}

function getErrorMessage(body: unknown, fallback: string): string {
  // Use the API's standard error field when present; otherwise show the caller's safe fallback.
  if (typeof body === "object" && body !== null && "error" in body &&
      typeof body.error === "string") {
    return body.error;
  }

  return fallback;
}

async function getAntiforgeryToken(signal?: AbortSignal): Promise<string> {
  // Fetch a request token from the same origin before a state-changing request.
  const response = await fetch("/api/auth/antiforgery", {
    method: "GET",
    credentials: "same-origin",
    headers: { Accept: "application/json" },
    signal,
  });
  // Parse and validate the response instead of sending an empty or malformed CSRF header.
  const body = await readResponseBody(response);

  if (!response.ok || typeof body !== "object" || body === null ||
      !("requestToken" in body) || typeof body.requestToken !== "string") {
    throw new ApiRequestError(
      getErrorMessage(body, "Could not prepare a secure request."),
      response.status,
    );
  }

  return body.requestToken;
}

export async function apiRequest<TResponse>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<TResponse> {
  // Keep all API requests same-origin so the browser sends the application's authentication cookie.
  const headers = new Headers({ Accept: "application/json" });
  const method = options.method ?? "GET";

  // Mark JSON only when a request actually contains a body.
  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  // Attach the verified token only to calls that mutate server state.
  if (options.antiforgery) {
    headers.set("X-CSRF-TOKEN", await getAntiforgeryToken(options.signal));
  }

  // Send the request with the caller's method, payload, cancellation signal, and session cookie.
  const response = await fetch(path, {
    method,
    credentials: "same-origin",
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  });
  // Parse once so both success data and the API's structured error can be returned consistently.
  const body = await readResponseBody(response);

  // Convert non-2xx responses to a typed error so pages can route 401s back to sign-in.
  if (!response.ok) {
    throw new ApiRequestError(
      getErrorMessage(body, "The request could not be completed."),
      response.status,
    );
  }

  return body as TResponse;
}
