"use strict";

        const aliceIncident =
            "20000000-0000-0000-0000-000000000001";

        const bobIncident =
            "20000000-0000-0000-0000-000000000002";

        const authResult = document.getElementById("authResult");
        const incidentResult = document.getElementById("incidentResult");
        const incidentInput = document.getElementById("incidentId");

        async function login() {
            const userName =
                document.getElementById("username").value;

            const password =
                document.getElementById("password").value;

            try {
                const response = await fetch("/api/auth/login", {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify({ userName, password })
                });

                authResult.textContent = response.ok
                    ? "Авторизація успішна (HTTP " + response.status + ")"
                    : "Помилка авторизації (HTTP " + response.status + ")";

                authResult.className =
                    response.ok ? "success" : "error";
            }
            catch {
                authResult.textContent =
                    "Помилка з'єднання із сервером.";
                authResult.className = "error";
            }
        }

        async function logout() {
            try {
                const response = await fetch("/api/auth/logout", {
                    method: "POST",
                    credentials: "same-origin"
                });

                authResult.textContent =
                    "Вихід: HTTP " + response.status;

                authResult.className =
                    response.ok ? "success" : "error";
            }
            catch {
                authResult.textContent =
                    "Не вдалося виконати вихід.";
                authResult.className = "error";
            }
        }

        async function loadIncidentDetails(id) {
            incidentInput.value = id;
            incidentResult.textContent = "Завантаження...";

            try {
                const response = await fetch(
                    "/api/incidents/" + encodeURIComponent(id),
                    { credentials: "same-origin" }
                );

                if (response.status === 401) {
                    incidentResult.textContent =
                        "HTTP 401 Unauthorized: потрібно увійти.";
                    return;
                }

                if (response.status === 403) {
                    incidentResult.textContent =
                        "HTTP 403 Forbidden: доступ до інциденту заборонено.";
                    return;
                }

                if (response.status === 404) {
                    incidentResult.textContent =
                        "HTTP 404 Not Found: інцидент не знайдено.";
                    return;
                }

                if (!response.ok) {
                    incidentResult.textContent =
                        "Помилка HTTP " + response.status;
                    return;
                }

                const incident = await response.json();

                incidentResult.textContent = JSON.stringify(
                    incident, null, 2
                );
            }
            catch {
                incidentResult.textContent =
                    "Помилка з'єднання із сервером.";
            }
        }

        document.getElementById("loginButton")
            .addEventListener("click", login);

        document.getElementById("logoutButton")
            .addEventListener("click", logout);

        document.getElementById("loadButton")
            .addEventListener("click", () =>
                loadIncidentDetails(incidentInput.value));

        document.getElementById("ownButton")
            .addEventListener("click", () =>
                loadIncidentDetails(aliceIncident));

        document.getElementById("foreignButton")
            .addEventListener("click", () =>
                loadIncidentDetails(bobIncident));
