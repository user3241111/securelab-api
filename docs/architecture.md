# Карта архітектури

Це карта архітектури системи після виконання ЛР 1 з трасуванням розширеного маршруту, ключовими компонентами, межами довіри та конфігураційними входами.

## Компоненти

| Компонент | Розташування | Відповідальність |
|---|---|---|
| Browser client | `src/SecureLab.Api/Client/` | Надсилає HTTP-запити через fetch, підтримує стани завантаження/помилок, безпечно показує відповідь через DOM API (`textContent`) |
| Presentation | `src/SecureLab.Api/Presentation/` | Описує Minimal API endpoints, валідує вхідні параметри (allowlist), формує HTTP-відповіді (200, 400, 404) |
| Application | `src/SecureLab.Api/Application/` | Виконує read-only сценарії отримання списку, деталей та зведеної статистики інцидентів з логуванням |
| Data | `src/SecureLab.Api/Data/` | Відображає C#-сутності на PostgreSQL через EF Core/Npgsql (`SecureLabDbContext`), містить міграції та seed/reset логіку |
| PostgreSQL | `infra/compose.yaml` | Зберігає навчальні дані у локальному контейнері Docker Compose |
## Підготовлений наскрізний маршрут

```text
Кнопка summary або фільтр у Client/index.html
  → handler у Client/app.js (виклик apiFetch)
  → GET /api/incidents/severity-summary?status=New
  → Presentation/Endpoints/IncidentEndpoints.cs (перевірка allowlist через Enum.TryParse / Enum.IsDefined)
  → Application/Incidents/IncidentQueries.cs (AsNoTracking, Where, GroupBy, ToListAsync)
  → Data/SecureLabDbContext.cs (DbSet<Incident> Incidents, таблиця incidents)
  → PostgreSQL (SQL-агрегація за колонками severity та status)
  → response DTO IncidentSeveritySummaryResponse у Presentation/Contracts/
  → JSON (HTTP 200 OK з масивом { severity, count })
  → textContent та createElement у Client/app.js без небезпечних DOM-приймачів
  ```

## Межі довіри

Доповніть таблицю щонайменше трьома конкретними спостереженнями.

| Межа | Чому даним ще не можна довіряти | Де перевіряємо або обмежуємо |
|---|---|---|
| Користувач → Browser client | Користувач повністю контролює введення в інтерфейсі (поля форми, параметри, вибір у фільтрах)[cite: 8] | Скасування стандартного submit через `event.preventDefault()`, клієнтська перевірка стану форми перед запитом[cite: 8] |
| Browser client → API | Будь-який HTTP-запит і параметри можна підробити або надіслати в обхід браузера через curl чи REST-клієнт[cite: 8] | Маршрутне обмеження `:guid` для details; серверна перевірка query-параметра `status` за білим списком (allowlist) через `Enum.TryParse` та `Enum.IsDefined` у `IncidentEndpoints.cs` з поверненням 400 ValidationProblemDetails[cite: 7, 8, 9] |
| PostgreSQL → API → DOM | У базі даних може зберігатися раніше збережений шкідливий текст (наприклад, тестовий payload `<script>` у seed-описі інциденту)[cite: 8] | Явна проєкція у response DTO без зайвих внутрішніх полів; виведення в інтерфейсі виключно через безпечні текстові операції `textContent` та `document.createTextNode` із повною забороною `innerHTML`[cite: 8] |

## Конфігураційні входи

- `global.json` — версія .NET SDK;
- `src/SecureLab.Api/appsettings*.json` — режим міграцій і локальний connection string;
- `infra/compose.yaml` — версія PostgreSQL, порт і локальні навчальні облікові дані;
- змінна середовища `ConnectionStrings__SecureLab` — безпечний спосіб перевизначити connection string поза репозиторієм.
