# Source-to-Sink Analysis — Lab 05

## 1. Мета

Мета аналізу — визначити повний шлях недовірених даних від джерела до місця їх відображення в браузері та перевірити усунення Stored XSS у полях `Incident.Description` і `IncidentComment.Text`.

## 2. Incident.Description

**Source:** поле опису інциденту на сторінці `browser-lab.html`.

**Передавання даних:** файл `browser-lab.js` надсилає опис у JSON через `POST /api/incidents`.

**Обробка:** у `Lab02Endpoints.cs` запит приймається через `CreateIncidentRequest`. Сервер перевіряє максимальну довжину опису (4000 символів) і виконує `Trim()`.

**Збереження:** значення записується в `Incident.Description` через Entity Framework Core до PostgreSQL.

**Отримання:** метод `IncidentQueries.GetDetailsAsync()` читає опис із бази. Endpoint `GET /api/incidents/{id}` повертає його у складі `IncidentDetailsResponse`.

**Sink:** у `browser-lab.js` опис відображається в елементі `#description` за допомогою `renderSafeText()`, яка використовує `textContent`.

**Шлях даних:**

`Форма → POST /api/incidents → CreateIncidentRequest → PostgreSQL → IncidentQueries → GET /api/incidents/{id} → renderSafeText() → textContent`

## 3. IncidentComment.Text

**Source:** тестовий коментар із XSS-маркером, визначений константою `Lab05Marker` у `DbSeeder.cs`.

**Збереження:** під час початкового заповнення бази створюється запис `IncidentComment`, у якому поле `Text` містить тестовий HTML-код. Коментар прив'язаний до інциденту Alice та має `IsInternal = false`.

**Отримання:** `IncidentQueries.GetDetailsAsync()` читає коментарі з бази даних і включає їх до `IncidentDetailsResponse`. Endpoint `GET /api/incidents/{id}` повертає їх у JSON.

**Sink:** у `browser-lab.js` для кожного коментаря створюється елемент `p`, а його вміст встановлюється через `renderSafeText()`.

**Шлях даних:**

`DbSeeder.cs → IncidentComment.Text → PostgreSQL → IncidentQueries → GET /api/incidents/{id} → renderSafeText() → textContent`

Окремий endpoint `GET /api/incident-comments/{id}` також дозволяє отримати коментар після перевірки прав доступу, але тестова браузерна сторінка використовує маршрут отримання інциденту.

## 4. Причина Stored XSS

У вразливій реалізації недовірений текст із бази даних передавався до `innerHTML`. Через це браузер міг інтерпретувати HTML-код як розмітку сторінки та виконувати JavaScript-обробники подій.

Збереження даних у PostgreSQL і передавання через JSON самі по собі не усувають XSS. Вирішальне значення має спосіб вставлення отриманих даних у DOM.

## 5. Виправлення

Для обох полів використовується спільна функція:

```javascript
function renderSafeText(element, value) {
  element.textContent = value ?? "";
}
```

Метод `textContent` вставляє значення як текст і не створює HTML-елементів із отриманого рядка.

## 6. Результати перевірки

- До виправлення тестовий Stored XSS-маркер виконувався в браузері.
- Після виправлення маркер відображався як звичайний текст.
- Для коментарів перевірка DOM підтвердила відсутність створених елементів `img` з обробником `onerror`.
- Текст із лапками, апострофами, HTML-подібними тегами та Unicode відображався без змін.
- Опис довжиною 4000 символів зберігався та відображався повністю.

## 7. Залишкові ризики

Використання `textContent` захищає перевірені текстові місця відображення, але не гарантує відсутності XSS у всьому застосунку. Нові компоненти можуть повторно створити вразливість, якщо використовуватимуть `innerHTML` або інші небезпечні способи вставлення даних.

Також у `UpdateContentAsync()` відсутня явна перевірка максимальної довжини `Description`, хоча обмеження 4000 символів існує на рівні моделі бази даних. Це окреме питання узгодженості валідації, а не причина Stored XSS.