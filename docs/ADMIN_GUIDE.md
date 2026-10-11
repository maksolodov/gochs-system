# Руководство администратора системы ГОЧС

## 1. Назначение

Документ предназначен для технического специалиста, который устанавливает, обновляет и сопровождает приложение.

Подробная серверная конфигурация уже описана в [DEPLOY.md](DEPLOY.md). Этот документ — эксплуатационный runbook.

## 2. Техническая модель

```text
Browser
  ↓ HTTPS
Caddy
  ↓ internal Docker network
ASP.NET Core 8
  ↓
EF Core 8
  ↓
SQLite + WAL
```

Frontend и API обслуживаются одним процессом.

Система рассчитана на **один экземпляр приложения на одну SQLite-базу**.

`instance.lock` блокирует второй процесс с тем же `DataPath`.

## 3. Требования

### Локально

- Docker + Compose; или
- .NET 8 SDK.

### Сервер

- Linux + Docker Compose;
- DNS;
- HTTPS;
- persistent volumes;
- отдельное место для backup;
- защищённый secret file.

## 4. Локальный запуск

```bash
docker compose up --build -d
```

Приложение:

```text
http://127.0.0.1:5080
```

Получить локально созданный пароль:

```bash
docker compose exec gochs cat /app/data/operator-password.txt
```

Остановка:

```bash
docker compose down
```

Не использовать `down -v`, если данные нужны.

## 5. Запуск без Docker

```bash
cd backend
dotnet run -- --urls http://127.0.0.1:5080
```

## 6. Server mode

Используется `compose.prod.yaml`.

Создайте `.env` на основе `.env.example`.

| Параметр | Назначение |
|---|---|
| `DOMAIN` | DNS и AllowedHosts |
| `ACME_EMAIL` | сертификат Caddy |
| `BIND_ADDRESS` | внешний интерфейс |
| `HTTP_PORT` | обычно 80 |
| `HTTPS_PORT` | обычно 443 |
| `PROXY_SUBNET` | приватная Docker subnet |
| `PROXY_IP` | фиксированный IP Caddy |
| `OPERATOR_PASSWORD_FILE` | secret file |
| `SESSION_MINUTES` | 1–480 минут |

Проверка:

```bash
docker compose --env-file .env -f compose.prod.yaml config --quiet
```

Запуск:

```bash
docker compose --env-file .env -f compose.prod.yaml up --build -d
```

## 7. Секрет

В server mode задайте случайный пароль минимум 16 символов.

Рекомендации:

- не хранить в Git;
- не помещать в `.env`;
- ограничить права;
- не передавать в shell arguments;
- менять при компрометации.

Контейнер работает от пользователя `app` (UID 1654).

Для Linux рекомендуется:

```text
owner: 1654
mode: 0400
```

## 8. DataPath

Ключевые файлы:

```text
DataPath/
├── gochs.db
├── instance.lock
├── keys/
├── backups/
└── operator-password.txt
```

`keys/` содержит ASP.NET Data Protection keys.

## 9. SQLite

Особенности:

- foreign keys включены;
- WAL;
- изменяющие HTTP-запросы сериализуются;
- несколько процессов с одной БД не поддерживаются;
- схема обновляется SQL-миграциями;
- версии хранятся в `__GochsMigrations`.

Не редактируйте рабочую схему вручную.

## 10. Миграции

`SchemaUpgrade.Apply`:

1. читает применённые версии;
2. сверяет SHA-256;
3. распознаёт поддерживаемую legacy-схему;
4. перед изменением создаёт backup;
5. применяет SQL транзакционно;
6. выполняет `PRAGMA integrity_check`;
7. включает WAL.

При неизвестной схеме запуск останавливается.

Не удаляйте `__GochsMigrations`, чтобы «починить» миграцию.

## 11. Backup

### Docker

```bash
docker compose --env-file .env -f compose.prod.yaml exec gochs \
  dotnet Gochs.dll --backup /app/backups/manual.db
```

На хост:

```bash
docker compose --env-file .env -f compose.prod.yaml cp \
  gochs:/app/backups/manual.db ./manual.db
```

### Без Docker

```bash
cd backend
dotnet build -c Release
dotnet bin/Release/net8.0/Gochs.dll \
  --backup ../data/backups/manual.db
```

Backup использует SQLite Backup API и проверку целостности.

## 12. Политика backup

Рекомендуется:

- ежедневная копия;
- несколько поколений;
- off-site/отдельный носитель;
- restore drill;
- backup перед каждым release;
- контроль возраста последней копии.

Конкретный retention утверждается организацией.

## 13. Restore

### Docker

Остановить:

```bash
docker compose --env-file .env -f compose.prod.yaml stop gochs
```

Восстановить:

```bash
docker compose --env-file .env -f compose.prod.yaml run --rm --no-deps \
  gochs --restore /app/backups/manual.db
```

Запустить:

```bash
docker compose --env-file .env -f compose.prod.yaml up -d gochs
```

Перед заменой существующая БД резервируется.

После restore проверить:

- health;
- вход;
- Dashboard;
- ключевые данные;
- текущий режим;
- документы;
- новый backup.

## 14. Обновление

1. зафиксировать текущую версию;
2. создать backup;
3. сохранить конфигурацию;
4. получить release;
5. остановить приложение;
6. запустить новый release;
7. дождаться migrations;
8. проверить health;
9. выполнить smoke-test;
10. проверить данные;
11. сделать backup новой схемы;
12. записать факт обновления.

Старый код нельзя запускать поверх новой схемы без совместимой копии.

## 15. Health

```http
GET /api/health
```

Ожидается:

```json
{"status":"ok"}
```

Endpoint доступен без авторизации.

## 16. Логи

```bash
docker compose logs -f gochs
```

или:

```bash
docker compose --env-file .env -f compose.prod.yaml logs -f gochs
```

При инциденте сохраняйте:

- время;
- endpoint/операцию;
- версию;
- браузер;
- шаги;
- server log.

Не сохраняйте пароль в тикете.

## 17. Swagger

В учебной среде:

```text
/swagger
```

Требует сессию.

В production compose:

```text
SwaggerEnabled=false
```

## 18. Security baseline

Реализованы:

- cookie auth;
- HttpOnly;
- SameSite Strict;
- Secure в server mode;
- CSRF для write-запросов;
- rate limiting попыток входа;
- server-side session ID;
- Data Protection;
- HSTS;
- nosniff;
- DENY framing;
- same-origin referrer policy;
- no-store для API;
- trusted proxy;
- точный AllowedHosts;
- лимиты request body;
- non-root container;
- no-new-privileges;
- drop capabilities.

## 19. Ограничения безопасности

Администратор должен учитывать:

- одну операторскую модель;
- отсутствие RBAC;
- отсутствие УКЭП;
- отсутствие malware scanner;
- отсутствие криптографической защиты SQLite-файла со стороны приложения;
- необходимость фактической проверки production deployment;
- общий audit не заменяет полноценный защищённый журнал действий многопользовательской системы.

## 20. Вложения

До 10 МБ, PDF/PNG/JPEG.

Проверяются:

- расширение;
- MIME;
- сигнатура.

## 21. После restart

Проверить:

1. health;
2. вход;
3. Dashboard;
4. справочники;
5. текущий режим;
6. последнюю операцию;
7. отсутствие повторного seed;
8. отсутствие migration errors.

Seed создаётся только при пустой БД.

## 22. Регулярные задачи

### Ежедневно

- health;
- процесс/контейнер;
- диск;
- backup;
- P1/P2.

### Еженедельно

- повторяющиеся ошибки;
- размер БД;
- внешний backup;
- одна контрольная бизнес-операция.

### Ежемесячно

- restore drill;
- ревизия секретов;
- зависимости;
- открытые дефекты;
- документация.

Периодичность рекомендуется адаптировать под организацию.

## 23. Аварии

### Не запускается после обновления

- не удалять БД;
- прочитать log;
- проверить SchemaUpgrade;
- использовать backup до update;
- восстановить совместимую версию.

### Потерян/скомпрометирован пароль

Заменить secret и перезапустить сервис.

### Повреждена БД

- остановить приложение;
- сохранить повреждённый файл;
- восстановить проверенный backup;
- зарегистрировать инцидент.

### Закончился диск

- остановить необязательные операции;
- безопасно освободить место;
- не удалять актуальную БД и `keys/`;
- проверить backup;
- выполнить smoke-test после восстановления.

## 24. Передача release

Каждая версия сопровождается:

- commit/tag;
- changelog;
- migrations;
- known limitations;
- test results;
- special upgrade steps;
- актуальной документацией.
