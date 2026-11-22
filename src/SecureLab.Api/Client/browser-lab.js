
function renderSafeText(element, value) {
  element.textContent = value ?? "";
}

let csrfToken = null;

async function getCsrfToken() {
  const response = await fetch("/api/antiforgery/token", {
    method: "GET",
    credentials: "same-origin"
  });

  if (!response.ok) {
    throw new Error(`CSRF token request failed: ${response.status}`);
  }

  const data = await response.json();

  if (!data.token) {
    throw new Error("CSRF token is missing");
  }

  csrfToken = data.token;
  return csrfToken;
}

async function csrfFetch(url, options = {}) {
  if (!csrfToken) {
    await getCsrfToken();
  }

  const headers = new Headers(options.headers);
  headers.set("X-CSRF-TOKEN", csrfToken);

  return fetch(url, {
    ...options,
    credentials: "same-origin",
    headers
  });
}

let current;
const id = () => encodeURIComponent(document.querySelector("#incident-id").value.trim());
const status = document.querySelector("#status");

async function load() {
  const response = await fetch(`/api/incidents/${id()}`);
  status.textContent = `GET: ${response.status}`;
  if (!response.ok) return;

  current = await response.json();
  document.querySelector("#title").textContent = current.title;

  renderSafeText(
    document.querySelector("#description"),
    current.description
  );

  document.querySelector("#comments").replaceChildren();

  for (const comment of current.comments ?? []) {
    const paragraph = document.createElement("p");
    renderSafeText(paragraph, comment.text);
    document.querySelector("#comments").append(paragraph);
  }

  document.querySelector("#edit").elements.description.value = current.description;
}

document.querySelector("#load").addEventListener("click", load);

document.querySelector("#edit").addEventListener("submit", async event => {
  event.preventDefault();

  try {
    const response = await csrfFetch("/api/incidents", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        title: event.currentTarget.elements.title.value,
        description: event.currentTarget.elements.description.value,
        severity: "Low",
        occurredAtUtc: new Date().toISOString()
      })
    });

    status.textContent = `POST: ${response.status}`;

    if (response.ok) {
      const created = await response.json();
      document.querySelector("#incident-id").value = created.id;
      await load();
    }
  } catch (error) {
    status.textContent = `POST error: ${error.message}`;
  }
});
