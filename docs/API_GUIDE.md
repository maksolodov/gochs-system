# API и интеграции системы ГОЧС

## 1. Назначение

Документ описывает фактическую модель доступа к API и основные группы endpoint'ов. Он дополняет Swagger и объясняет правила интеграции, которые важно учитывать клиенту.

## 2. Общая модель

Все служебные маршруты находятся под `/api`.

Большинство данных защищены cookie-аутентификацией.

Публично доступны:

- `/api/health`;
- `/api/auth/token`;
- `/api/auth/session`;
- `/api/auth/login`.

Swagger в локальной среде также требует авторизованной сессии. В server mode по production-конфигурации Swagger отключён.

## 3. Аутентификация

### Получить CSRF token

```http
GET /api/auth/token
```

Ответ:

```json
{
  "token": "..."
}
```

Одновременно устанавливается antiforgery cookie.

### Войти

```http
POST /api/auth/login
Content-Type: application/json
X-CSRF-TOKEN: <token>
```

```json
{
  "name": "Иванов И.И.",
  "password": "..."
}
```

При успехе сервер устанавливает cookie `Gochs.Session`.

### Проверить сессию

```http
GET /api/auth/session
```

### Выйти

```http
POST /api/auth/logout
X-CSRF-TOKEN: <token>
```

После logout server-side session ID удаляется.

## 4. CSRF

Все изменяющие запросы — POST, PUT, PATCH, DELETE — должны проходить antiforgery validation.

Используется заголовок:

```text
X-CSRF-TOKEN
```

Требование действует и для multipart upload.

GET/HEAD/OPTIONS token не требуют.

## 5. Коды ответов

| Код | Значение |
|---:|---|
| 200 | операция выполнена |
| 201 | ресурс создан |
| 204 | выполнено без тела |
| 400 | validation / binding / CSRF / некорректный запрос |
| 401 | отсутствует действующая сессия |
| 403 | доступ запрещён |
| 404 | ресурс или маршрут не найден |
| 409 | конфликт бизнес-состояния, связей или уникальности |
| 429 | временная блокировка после неудачных входов |
| 500 | внутренняя ошибка |

Типичный business error:

```json
{
  "message": "Описание причины"
}
```

Контроллеры с `[ApiController]` также могут вернуть стандартный framework validation response. Клиент не должен считать все `400` одной фиксированной схемой.

## 6. JSON и enum

Контроллерные enum сериализуются строками; числовые значения enum JSON converter'ом запрещены.

В интегрированной части проекта ряд статусов остаётся строковыми полями (`InStock`, `Draft`, `Ready` и т. д.). Используйте только документированные значения.

## 7. Время

```http
GET /api/time/resolve?local=yyyy-MM-ddTHH:mm
```

Возвращает UTC и зону:

```json
{
  "utc": "...",
  "timeZone": "Europe/Moscow"
}
```

Для приказов режима используется `DateTimeOffset` со смещением. Критичное время хранится в UTC, а UI и документы используют московское время.

## 8. Универсальные CRUD-справочники

Для следующих ресурсов используется схема:

```text
GET    <path>
GET    <path>/{id}
POST   <path>
PUT    <path>/{id}
DELETE <path>/{id}
```

Пути:

- `/api/siz/warehouses`
- `/api/siz/types`
- `/api/siz/contractors`
- `/api/nfgo/formations`
- `/api/nfgo/units`
- `/api/nfgo/norms`
- `/api/protective-structures`
- `/api/evacuation/vehicles`
- `/api/evacuation/points`
- `/api/evacuation/routes`
- `/api/activities`

Удаление может возвращать `409`, если запись связана с другими данными.

## 9. Сотрудники

```text
GET    /api/employees
GET    /api/employees/{id}
POST   /api/employees
PUT    /api/employees/{id}
DELETE /api/employees/{id}
POST   /api/employees/import
```

### Импорт

```json
{
  "source": "external-system",
  "sourceId": "12345",
  "employee": {}
}
```

Связь хранится как:

```text
Source + SourceId → EmployeeId
```

Если mapping существует, возвращается связанный сотрудник. Если mapping отсутствует, но найден тот же уникальный `PersonnelNumber`, внешний ID связывается с существующим Employee.

ФИО не следует использовать как ключ интеграции.

## 10. СИЗ

### Партии

```text
GET  /api/siz/batches
POST /api/siz/batches
PUT  /api/siz/batches/{id}
POST /api/siz/batches/bulk
```

### Карточки

```text
GET /api/siz/cards
GET /api/siz/cards/{id}
GET /api/siz/cards/{id}/barcode
PUT /api/siz/cards/{id}/condition
GET /api/siz/alerts
```

Barcode возвращается как `image/svg+xml`.

### Ведомости выдачи

```text
GET    /api/siz/statements
POST   /api/siz/statements
PUT    /api/siz/statements/{id}
POST   /api/siz/statements/auto
POST   /api/siz/statements/{id}/confirm
DELETE /api/siz/statements/{id}
```

Создание проекта не меняет остатки. Подтверждение повторно проверяет доступность и затем меняет карточки.

### Списание

```text
GET  /api/siz/writeoffs
GET  /api/siz/writeoffs/candidates
POST /api/siz/writeoffs
POST /api/siz/writeoffs/{id}/sign
```

### Утилизация

```text
GET  /api/siz/disposals
POST /api/siz/disposals
```

## 11. НФГО

Дополнительные операции:

```text
POST /api/nfgo/equipment
POST /api/nfgo/units/{id}/assemble
GET  /api/nfgo/orders
POST /api/nfgo/orders
POST /api/nfgo/orders/{id}/refresh
POST /api/nfgo/orders/{id}/sign
```

`assemble` создаёт внутренние уведомления сотрудникам звена.

Приказ НФГО хранит snapshot состава.

## 12. Защитные сооружения

CRUD:

```text
/api/protective-structures
```

Проверки:

```text
GET  /api/protective-structures/inspections
POST /api/protective-structures/inspections
```

Новая проверка обновляет текущее состояние сооружения.

## 13. Эвакуация

CRUD:

```text
/api/evacuation/vehicles
/api/evacuation/points
/api/evacuation/routes
```

Операции:

```text
GET  /api/evacuation/summary
POST /api/evacuation/auto-plan
POST /api/evacuation/reset
```

`auto-plan` распределяет по доступным вместимостям, но не выполняет GIS-оптимизацию.

## 14. Обучение

Prefix:

```text
/api/training
```

```text
GET  /api/training/groups
POST /api/training/groups
GET  /api/training/employees

GET  /api/training/sessions
POST /api/training/sessions
PUT  /api/training/sessions/{id}
POST /api/training/sessions/{id}/sign

GET /api/training/sessions/{id}/attendance
PUT /api/training/sessions/{id}/attendance

POST /api/training/lms-results

GET /api/training/summary
GET /api/training/protocol
GET /api/training/reminders

POST /api/training/sessions/{id}/change-requests
POST /api/training/change-requests/{id}/resolve
GET  /api/training/sessions/{id}/audit
```

Также:

```text
GET /api/training/change-requests
GET /api/training/protocol/export/{format}
```

## 15. Приказы и специальный режим

Prefix:

```text
/api/wartime/orders
```

```text
GET  /api/wartime/orders
GET  /api/wartime/orders/{id}
POST /api/wartime/orders
PUT  /api/wartime/orders/{id}
POST /api/wartime/orders/{id}/sign
POST /api/wartime/orders/{id}/activate
GET  /api/wartime/orders/{id}/document
GET  /api/wartime/orders/{id}/audit
GET  /api/wartime/orders/state
```

Дополнительно:

```text
GET  /api/wartime/leaves
POST /api/wartime/leaves
POST /api/wartime/leaves/{id}/cancel
GET  /api/wartime/schedules
GET  /api/wartime/protected-forms
```

## 16. Отчётность

```text
GET /api/dashboard
GET /api/reports/nfgo
GET /api/reports/procurement
GET /api/reports/nfgo/export/{format}
GET /api/reports/procurement/export/{format}
GET /api/documents/{kind}/{id}/{format}
```

Форматы зависят от endpoint. Используются HTML, DOCX, XLSX, XML и JSON.

## 17. Вложения

Метаданные:

```http
GET /api/attachments?resource=<type>&resourceId=<id>
```

Загрузка:

```http
POST /api/attachments/{resource}/{id}
Content-Type: multipart/form-data
X-CSRF-TOKEN: ...
```

Допустимые resource:

- `batch`
- `route`
- `writeoff`
- `statement`
- `disposal`

Скачивание:

```http
GET /api/attachments/{id}
```

Ограничения:

- до 10 МБ;
- PDF/PNG/JPEG;
- проверяются имя, MIME и сигнатура.

## 18. Настройки, аудит и уведомления

```text
GET /api/settings
PUT /api/settings
GET /api/audit

GET  /api/notifications
POST /api/notifications/{id}/acknowledge
```

## 19. Health

```http
GET /api/health
```

Без авторизации:

```json
{"status":"ok"}
```

Health подтверждает доступность процесса, а не полную функциональную готовность всех бизнес-сценариев.

## 20. Параллельность

Изменяющие HTTP-запросы в одном процессе сериализуются общей блокировкой.

Следствия:

- write API не предназначен для высокого параллелизма;
- сервер защищает критические проверки от части race conditions;
- нельзя запускать несколько процессов с одной SQLite-базой.

## 21. Версионирование

API сейчас не использует `/v1`.

Для внешней интеграции:

- фиксируйте commit/tag;
- документируйте используемые endpoints;
- выполняйте contract/integration tests перед update;
- не полагайтесь на недокументированные поля.

## 22. Swagger

В локальной/учебной среде:

```text
/swagger
```

Swagger показывает фактический OpenAPI schema, но интеграция должна дополнительно учитывать cookie auth, CSRF, business states и ограничения, описанные выше.
