# תוכנית מימוש – Real-Time Financial Monitor

> מסמך תכנון בלבד. לא נכתב קוד. הנחות ושאלות פתוחות מופיעות בסוף (סעיף 8).

## 1. מבנה ה-Solution והתיקיות

### תמונה כללית
```
financial-monitor/
├─ backend/                   ← כל קוד ה-.NET (פרויקט + בדיקות)
├─ frontend/                  ← React + TypeScript (Vite)
├─ k8s/                       ← קבצי Kubernetes (בונוס)
├─ docs/                      ← תיעוד
├─ Dockerfile                 ← בניית image (בונוס)
├─ .dockerignore
├─ .github/
│  └─ workflows/
│     └─ ci.yml               ← GitHub Actions: מריץ את כל הבדיקות בכל push
├─ README.md                  ← נוצר בשלב 0 ומתעדכן בכל שלב
├─ .gitignore                 ← bin/obj, node_modules
└─ CLAUDE.md
```

בשורש רואים מיד את שני חלקי המערכת, `backend/` ו-`frontend/`. בתוך `backend/` נשמרת מוסכמת .NET של `src/` + `tests/`.

### Backend – ‏`backend/` (מאורגן בשכבות)
```
backend/
├─ FinancialMonitor.sln
├─ src/
│  └─ FinancialMonitor.Api/
│     ├─ FinancialMonitor.Api.csproj
│     ├─ Program.cs                          ← DI + middleware בלבד (כולל Swagger UI ו-exception handler)
│     ├─ Contracts/                          ← חוזה ה-API (מה שעובר ב-JSON)
│     │  ├─ TransactionRequest.cs            ← קלט POST (שדות nullable + DataAnnotations לולידציה)
│     │  ├─ TransactionResponse.cs           ← פלט GET ו-SignalR
│     │  ├─ IngestResponse.cs                ← תשובת POST: { transactionId, outcome }
│     │  └─ TransactionMappings.cs           ← ToDomain() / ToResponse()
│     ├─ Models/                             ← מודל הדומיין (פנימי)
│     │  ├─ Transaction.cs                   ← record immutable, תמיד תקין
│     │  ├─ TransactionStatus.cs             ← Pending / Completed / Failed
│     │  └─ UpsertResult.cs                  ← Created / Updated / IgnoredStale
│     ├─ Controllers/
│     │  └─ TransactionsController.cs        ← POST ו-GET
│     ├─ Services/
│     │  ├─ ITransactionService.cs
│     │  └─ TransactionService.cs            ← שמירה ואז שידור
│     ├─ Repositories/
│     │  ├─ ITransactionRepository.cs
│     │  └─ InMemoryTransactionRepository.cs ← אחסון בזיכרון + lock
│     ├─ Notifiers/
│     │  ├─ ITransactionNotifier.cs
│     │  └─ SignalRTransactionNotifier.cs    ← שליחת עדכון ללקוחות
│     └─ Hubs/
│        └─ TransactionsHub.cs               ← נקודת החיבור של SignalR
└─ tests/
   └─ FinancialMonitor.Tests/
      ├─ FinancialMonitor.Tests.csproj
      ├─ Contracts/
      │  └─ TransactionMappingsTests.cs
      ├─ Repositories/
      │  ├─ InMemoryTransactionRepositoryTests.cs
      │  └─ InMemoryTransactionRepositoryConcurrencyTests.cs
      ├─ Services/
      │  └─ TransactionServiceTests.cs
      └─ Integration/                        ← WebApplicationFactory
         ├─ TransactionsApiTests.cs          ← POST/GET, קודי סטטוס, 500, concurrency מקצה לקצה
         ├─ RequestValidationTests.cs        ← 400 לכל סוג קלט לא תקין
         └─ TransactionsHubTests.cs          ← לקוחות SignalR מקבלים אירועים
```

**למה אין בדיקות unit ל-Controller ול-Notifier:** שניהם שכבה דקה (מיפוי תוצאה לקוד HTTP, ושורה אחת של `SendAsync`). בדיקת unit עם mocks רק הייתה חוזרת על הקוד, ובדיקות ה-Integration כבר מכסות אותם מול ה-pipeline האמיתי (routing, model binding, סריאליזציה, SignalR).

### Frontend – ‏`frontend/` (מאורגן לפי feature)
```
frontend/
├─ package.json
├─ vite.config.ts                        ← כולל proxy ל-Backend
├─ index.html
└─ src/
   ├─ main.tsx                           ← נקודת הכניסה
   ├─ App.tsx                            ← Router: ‏/add, ‏/monitor
   ├─ features/
   │  ├─ simulator/                      ← כל מה ששייך ל-‏/add
   │  │  ├─ AddPage.tsx                  ← טופס + כפתורים
   │  │  ├─ generateTransaction.ts       ← יצירת טרנזקציה אקראית
   │  │  ├─ transactionsApi.ts           ← שליחת POST
   │  │  └─ SubmitResult.tsx             ← הודעת הצלחה / שגיאה / סיכום burst
   │  └─ monitor/                        ← כל מה ששייך ל-‏/monitor
   │     ├─ MonitorPage.tsx
   │     ├─ useTransactionsHub.ts        ← SignalR + טעינה מחדש אחרי reconnect
   │     ├─ createBatcher.ts             ← לוגיקת ה-batching כפונקציה טהורה
   │     ├─ createBatcher.test.ts
   │     ├─ ErrorBanner.tsx              ← כשל בטעינה + כפתור Retry
   │     ├─ transactionsReducer.ts       ← ניהול ה-state
   │     ├─ transactionsReducer.test.ts
   │     ├─ TransactionList.tsx
   │     ├─ TransactionRow.tsx
   │     ├─ StatusBadge.tsx
   │     ├─ FilterBar.tsx
   │     ├─ filterTransactions.ts         ← לוגיקת הסינון כפונקציה טהורה
   │     ├─ filterTransactions.test.ts
   │     ├─ ConnectionStatus.tsx
   │     └─ monitor.css                  ← עיצוב ואנימציות
   └─ shared/                            ← רק מה ששני הדפים צריכים
      ├─ api/
      │  ├─ types.ts                     ← טיפוסי ה-API, נכתבים ידנית במקביל ל-DTOs (Transaction, IngestResponse וכו')
      │  ├─ apiClient.ts                 ← fetch רגיל + המרת שגיאות ל-ApiError
      │  └─ ApiError.ts                  ← שגיאה אחידה: ולידציה / שרת / רשת
      ├─ Layout.tsx
      └─ NavBar.tsx
```

### Kubernetes ותיעוד
```
k8s/
├─ deployment.yaml     ← כמה עותקים (pods) של השרת להריץ, ואיך
├─ service.yaml        ← כתובת אחת שמחלקת את הבקשות בין ה-pods
└─ redis.yaml          ← (אופציונלי) Redis לסנכרון בין pods

docs/
├─ ASSIGNMENT.md
├─ PLAN.md                      ← המסמך הזה
└─ adr/
   └─ 0001-multi-pod-sync.md    ← ייכתב בשלב 15
```

### החלטות מבנה
- **`backend/` במקביל ל-`frontend/`:** סימטריה בשורש, כך שברור מיד מה כל חלק. בתוך `backend/` – מוסכמת `src/` + `tests/` של .NET, שמאפשרת להוסיף פרויקטים בלי לשנות מבנה.
- **Backend בשכבות:** Controller ← Service ← Repository / Notifier, מאחורי interfaces. החלפת המימוש (למשל Repository משותף) היא חלק מהפתרון לבונוס.
- **פרויקט Api אחד + פרויקט Tests אחד:** פיצול ל-Domain/Application/Infrastructure הוא over-engineering ל-MVP.
- **`Contracts/` נפרד מ-`Models/`:** חוזה ה-API (DTO) מופרד ממודל הדומיין:
  - ב-DTO השדות nullable, ולכן אפשר להבדיל בין "חסר" לבין "0" (אחרת `amount` חסר הופך בשקט ל-0 ו-`transactionId` ל-`Guid.Empty`).
  - `Transaction` נשאר `record` immutable ותמיד תקין, בלי attributes של JSON.
  - שינוי בחוזה החיצוני לא שובר את הלוגיקה הפנימית – משנים רק DTO ו-mapping.
- **Frontend לפי feature (vertical slice):** כל route הוא feature עצמאי עם הקומפוננטות, ה-hook וה-state שלו. רק מה שמשותף באמת יושב ב-`shared/`. כך שני הפיצ'רים מהמטלה נראים כשתי יחידות נפרדות.

## 2. ארכיטקטורת Backend

### זרימה
```
POST /api/transactions  (JSON → TransactionRequest)
  → TransactionsController   (HTTP בלבד: ToDomain(), קודי סטטוס. הוולידציה רצה לפני כן אוטומטית מ-[ApiController])
  → ITransactionService      (לוגיקה על Transaction: upsert, החלטה אם לשדר)
      ├─ ITransactionRepository  (אחסון)
      └─ ITransactionNotifier    (שידור ללקוחות כ-TransactionResponse)
  ← IngestResponse           ({ transactionId, outcome })
GET  /api/transactions       → TransactionResponse[] – snapshot ראשוני לדשבורד
/hubs/transactions           → SignalR, אירוע "TransactionUpdated" עם TransactionResponse
```

הדומיין (`Transaction`) לא יוצא מה-Backend ולא נכנס אליו ישירות: בכניסה ממירים מ-DTO, וביציאה ממירים ל-DTO. ההמרה נעשית ב-boundary בלבד (Controller ו-Notifier).

### Contracts (DTO)
- `TransactionRequest` – `Guid? TransactionId`, `decimal? Amount`, `string? Currency`, `TransactionStatus? Status`, `DateTimeOffset? Timestamp`. הכול nullable כדי שהוולידציה תזהה שדות חסרים. `Status` הוא enum ולא מחרוזת: `JsonStringEnumConverter` ממיר בלי תלות ב-case (`"completed"` עובד), וערך לא מוכר נכשל כבר ב-deserialization ומחזיר `400` אוטומטית.
- `TransactionResponse` – אותם שדות, לא nullable, `Status` מסוג `TransactionStatus` (מסריאלז כמחרוזת), כך שגם Swagger UI מציג את הערכים האפשריים. ב-`types.ts` הוא מוגדר כ-`'Pending' | 'Completed' | 'Failed'`. משמש גם ל-GET וגם לאירוע SignalR, כך שה-Frontend מקבל מבנה אחד.
- `IngestResponse` – `Guid TransactionId`, `string Outcome` (`Created` / `Updated` / `IgnoredStale`).
- `TransactionMappings` – extension methods: `request.ToDomain()` (נקרא רק אחרי ולידציה מוצלחת) ו-`transaction.ToResponse()`.

### מודל (דומיין)
- `Transaction` – `record` immutable: `Guid TransactionId`, `decimal Amount`, `string Currency`, `TransactionStatus Status`, `DateTimeOffset Timestamp`.
- `TransactionStatus` – enum (`Pending`, `Completed`, `Failed`), מסריאלז כמחרוזת (`JsonStringEnumConverter`).
- `UpsertResult` – enum: `Created`, `Updated`, `IgnoredStale`.

### ולידציה (ב-boundary)
הוולידציה מוגדרת על `TransactionRequest` (לא על הדומיין) בכלים המובנים של ASP.NET Core, בלי מחלקת ולידטור נפרדת:
- `[Required]` על כל השדות; `[RegularExpression("^[A-Z]{3}$")]` על `currency`.
- `status` – ה-enum ב-DTO (ראה למעלה) מטפל בערכים לא מוכרים.
- שני כללים שאין להם attribute פשוט (`transactionId` ≠ `Guid.Empty`, `amount` > 0) – ב-`IValidatableObject.Validate` על ה-DTO.

`[ApiController]` מריץ את הוולידציה לפני ה-Action ומחזיר `400` עם `ValidationProblemDetails` (שגיאה לכל שדה), כך שה-Controller לא מכיל קוד ולידציה. רק בקשה תקינה מומרת ל-`Transaction`.

**למה לא ולידטור ידני:** אותה תוצאה בפחות קוד, ופורמט השגיאה זהה לזה של JSON שבור – ה-Frontend מפענח פורמט אחד.

### Interfaces
```csharp
interface ITransactionRepository {
    UpsertResult Upsert(Transaction t);                 // אטומי
    IReadOnlyList<Transaction> GetLatest(int count);
}
interface ITransactionNotifier {
    Task NotifyAsync(Transaction t, CancellationToken ct);
}
interface ITransactionService {
    Task<UpsertResult> ProcessAsync(Transaction t, CancellationToken ct);
    IReadOnlyList<Transaction> GetLatest();
}
```

### קודי תגובה וטיפול בשגיאות

העיקרון: רק מצבים שבאמת יכולים לקרות ושהמשתמש צריך לראות. כל תשובת שגיאה היא `ProblemDetails` (RFC 7807), כך שה-Frontend מפענח פורמט אחד.

| קוד | מתי זה קורה בפועל | תוכן התשובה |
|-----|-------------------|-------------|
| `201 Created` | טרנזקציה חדשה | `IngestResponse` עם `outcome: "Created"` |
| `200 OK` | עדכון ל-id קיים | `IngestResponse` עם `outcome: "Updated"` |
| `200 OK` | timestamp ישן מהשמור (מערכת חיצונית שולחת שלא לפי סדר – לא שגיאה) | `IngestResponse` עם `outcome: "IgnoredStale"` |
| `400 Bad Request` | שדה חסר או ערך לא חוקי (סכום שלילי, מטבע לא תקין, סטטוס לא מוכר) | `ValidationProblemDetails` – שגיאה לכל שדה |
| `400 Bad Request` | JSON שבור או טיפוס שגוי (למשל `"amount": "abc"`) – יכול להגיע ממערכת חיצונית | `ValidationProblemDetails` (אוטומטי מ-`[ApiController]`) |
| `500 Internal Server Error` | חריגה לא צפויה | `ProblemDetails` כללי עם `traceId`, בלי stack trace מחוץ ל-Development. נרשם ללוג |

**מימוש:**
- `AddProblemDetails()` + `UseExceptionHandler()` ב-`Program.cs` – handler גלובלי אחד, בלי try/catch מפוזר ב-Controller.
- שימי לב: ב-.NET 8, ‏`WebApplication` מפעיל אוטומטית את ה-Developer Exception Page כשהסביבה היא `Development`, והוא כן מחזיר stack trace. זה מכוון (נוח לדיבאג מקומי). בכל סביבה אחרת (`Production` ב-Docker/k8s) רק ה-`UseExceptionHandler` פעיל, ואין stack trace.
- כשל בשידור SignalR לא מחזיר שגיאה: הטרנזקציה כבר נשמרה, ולכן ה-Service רושם Warning ללוג והבקשה מצליחה.

**לא נוסף בכוונה** (לא יכול לקרות במערכת הזו או שאין מה להציג למשתמש): `401`/`403` (אין הזדהות במטלה), `404` (אין endpoint לפי id), `409` (טרנזקציה ישנה אינה קונפליקט), `415` (ה-Client שלנו תמיד שולח JSON), `429` (אין rate limiting).

### אבטחה (Authentication / Authorization)

**החלטה: לא ממומש ב-MVP.** המטלה לא דורשת, ומימוש חלקי גרוע ממימוש חסר. למשל, API key בסימולטור גלוי לכל מי שפותח DevTools, ולכן הוא אבטחה מדומה.

**מה כן קיים בקוד:** ולידציה של כל קלט, בלי stack trace בתשובות, ו-container שרץ כ-non-root.

**אין CORS, בכוונה:** ה-Frontend וה-API תמיד באותו origin – בפיתוח דרך ה-proxy של Vite, וב-Docker ה-Backend מגיש את קבצי ה-Frontend בעצמו. הגדרת CORS הייתה קוד שלא עושה כלום. אם בעתיד ה-Frontend יוגש מדומיין אחר, מוסיפים `AddCors` עם origin מהקונפיגורציה.

**איך זה היה נפתר בייצור** (לתיעוד בלבד):

| מי | Authentication | Authorization |
|----|----------------|---------------|
| המערכת החיצונית (`POST`) | OAuth2 Client Credentials | policy ‏`Transactions.Ingest` |
| סוכני תמיכה (`GET` + SignalR) | OIDC + JWT Bearer | policy ‏`Transactions.Read` |

פרטים שכדאי להכיר: ב-WebSocket הדפדפן לא שולח header של `Authorization`, ולכן SignalR מעביר את הטוקן ב-query string (`access_token`). HTTPS מסתיים ב-Ingress. הוספה בעתיד = `AddAuthentication()` + `[Authorize]` על ה-Controller וה-Hub, בלי לגעת ב-Service וב-Repository.

**ב-README:** סעיף **Security** של ארבע שורות בלבד, בלי טבלאות ובלי פרטי מימוש:

```markdown
## Security
- No authentication in this MVP.
- The API accepts transaction data via POST.
- In production, authentication and authorization are required.
- Access to data should be restricted by user role.
```

### Swagger UI

**Swashbuckle** (מגיעה עם תבנית ה-Web API של .NET 8) סורקת את ה-Controllers וה-DTOs ומציגה **Swagger UI** – דף לניסוי ה-API מהדפדפן, ב-`/swagger`, רק ב-Development. נוח לפיתוח ולבודק המטלה.

**טיפוסי ה-Frontend נכתבים ידנית** ב-`shared/api/types.ts`, במקביל ל-`TransactionResponse` ו-`IngestResponse`. עם שני endpoints וחמישה שדות זה פשוט יותר מ-code generation, ולא מוסיף שלב build או תלות בסדר `dotnet build` → `npm`. האירוע `TransactionUpdated` ב-SignalR שולח את אותו `TransactionResponse` שה-GET מחזיר, ולכן טיפוס אחד מכסה את שניהם.

### Concurrency (הנקודה המרכזית)
- **Repository:** `Dictionary<Guid, Transaction>` + `lock` יחיד. בתוך ה-lock נעשה בדיקה-והחלפה (השוואת timestamp ואז כתיבה) כך שהיא אטומית. בחרתי `lock` על פני `ConcurrentDictionary.AddOrUpdate` כי ה-delegate של `AddOrUpdate` עלול לרוץ כמה פעמים וקשה להחזיר ממנו את `UpsertResult` בצורה נקייה. `lock` פשוט וקריא, ומספיק ל-MVP. לפי TDD, ה-lock לא קיים בשלד (שלב 1) ונוסף רק בשלב 8, אחרי שבדיקות ה-concurrency נכשלו (ראה סעיף 4ג).
- **גבול גודל:** שומרים רק את N האחרונות (ברירת מחדל 1000) – בהיסט של הוצאת הישנה ביותר לפי timestamp בתוך אותו lock.
  - חיפוש הישנה ביותר הוא סריקה של O(n). עם 1000 פריטים זה זניח, ולכן לא מוסיפים מבנה נוסף (למשל `SortedSet` לפי timestamp) – זה היה מסבך את הקוד בלי תועלת מדידה.
  - **מגבלה ידועה:** אחרי שטרנזקציה פונתה, ה-Repository כבר לא זוכר את ה-timestamp שלה. עדכון ישן שיגיע עליה לאחר מכן ייקלט כ-`Created` ולא יידחה. מקובל ל-MVP, ומתועד ב-README.
- **קריאות:** `GetLatest` לוקח snapshot (העתקה) בתוך ה-lock ומחזיר רשימה immutable – אין חשיפת מבנה פנימי.
- **שידור מחוץ ל-lock:** ה-Service מפעיל את ה-Notifier רק אחרי שה-Repository סיים; לעולם לא מחזיקים lock בזמן I/O.
- **סדר שידור:** שני עדכונים מקבילים לאותו id עלולים להשתדר בסדר הפוך. לכן ה-Frontend מיישם את אותו כלל ("ignore older timestamp") – ה-merge אידמפוטנטי.
- **חיבורים מקבילים:** `IHubContext` של SignalR בטוח לשימוש מ-threads מרובים ומנהל את החיבורים בעצמו; אין לנו state משותף משלנו בצד השידור.
- **Singleton:** ה-Repository רשום כ-Singleton (הוא מחזיק את ה-state); Service ו-Notifier – גם Singleton / stateless.

## 3. ארכיטקטורת Frontend

### Routes (React Router)
- `/add` – סימולטור: טופס (amount, currency, status), כפתור "Generate random", כפתור "Send burst of 100" (לבדיקת עומס), ואפשרות לשלוח עדכון ל-id קיים (בדיקת lifecycle).
- `/monitor` – דשבורד חי.
- `/` → redirect ל-`/monitor`. ניווט עליון בין שני המסכים.

### ניהול State
- `useReducer` + `Map<id, Transaction>` (או מערך + אינדקס). אין צורך ב-Redux/Zustand.
- Actions: `snapshotLoaded`, `batchReceived` (מערך עדכונים). ה-reducer מיישם "ignore older timestamp".
- זרימת טעינה: מתחברים ל-Hub, ואז טוענים `GET /api/transactions` ומוזגים. כיוון שה-merge אידמפוטנטי, אין בעיה אם אירוע הגיע לפני ה-snapshot.
- Filter ("All / Pending / Completed / Failed", ו-"Errors only") – state מקומי + `useMemo` שקורא ל-`filterTransactions` (פונקציה טהורה, נבדקת ב-Vitest). לא נשמר ב-store.
- סטטוס חיבור (Connected / Reconnecting) עם `withAutomaticReconnect` ו-retry policy שלא מוותרת (backoff עד 10 שניות). ברירת המחדל של SignalR מוותרת אחרי 4 ניסיונות, ואז צריך כפתור Reconnect ידני – policy של שורה אחת פשוטה יותר וטובה יותר לסוכן תמיכה שמשאיר את הדשבורד פתוח.
- הטיפוסים (`Transaction`, `IngestResponse` וכו') נכתבים ידנית ב-`shared/api/types.ts`, במקביל ל-DTOs של ה-Backend (ראה סעיף 2).

### טיפול בשגיאות ב-UI

רק מצבים שבאמת יכולים לקרות. `apiClient` ממיר כל כשל ל-`ApiError` אחיד עם `kind`, כך שהקומפוננטות לא מפענחות HTTP בעצמן:

| מצב | איפה | מה המשתמש רואה |
|-----|------|-----------------|
| `400` ולידציה | `/add` | שגיאה ליד כל שדה בטופס, לפי ה-`errors` מה-`ValidationProblemDetails` |
| `500` | `/add`, `/monitor` | הודעה כללית "שגיאת שרת, נסה שוב" (בלי פרטים טכניים) |
| השרת לא זמין (fetch נכשל, או `502`/`504` מה-proxy בפיתוח) | `/add`, `/monitor` | "השרת לא זמין" |
| `IgnoredStale` | `/add` | הודעת מידע (לא שגיאה): "התעלמנו – קיים עדכון חדש יותר" |
| חלק מה-burst נכשל | `/add` | סיכום: "97 נשלחו, 3 נכשלו" (`Promise.allSettled`) |
| טעינת ה-snapshot (GET) נכשלה | `/monitor` | `ErrorBanner` עם כפתור Retry. אירועים חיים ממשיכים להגיע |
| SignalR מנותק | `/monitor` | `ConnectionStatus`: "Reconnecting..." עד שהחיבור חוזר (ה-retry לא מוותר, ולכן אין צורך בכפתור Reconnect) |
| חזרה מ-reconnect | `/monitor` | אין הודעה – טוענים מחדש את ה-snapshot ברקע, כי SignalR לא שולח את האירועים שפוספסו בזמן הניתוק. ה-merge האידמפוטנטי מונע כפילויות |

### שמירה על רספונסיביות (100+ טרנזקציות בפרק זמן קצר)
1. **Batching:** הודעות SignalR נאספות ב-`useRef` (מבלי לגרום ל-render) ומרוקנות ל-reducer פעם אחת בכל `requestAnimationFrame`/כ-100ms → render אחד לכל batch במקום 100. הלוגיקה עצמה ב-`createBatcher` – פונקציה טהורה שלא תלויה ב-React, כך שאפשר להוכיח בבדיקה ש-100 הודעות מהירות = flush אחד.
2. **תקרת רשימה:** מציגים עד 500 שורות אחרונות (ממוינות לפי timestamp יורד).
3. **`React.memo` על `TransactionRow`** עם key=`transactionId`, כך ששורה שלא השתנתה לא מרונדרת מחדש.
4. **Virtualization** (`react-window`) – רק אם המדידה תראה צורך; מתחילים בלי, כדי לשמור על פשטות.
5. **אנימציות ב-CSS בלבד** (`transform`/`opacity`, לא `width/height`), ללא ספריית אנימציה.
6. **מדידה:** כפתור ה-burst בסימולטור + Performance tab ב-DevTools כדי לוודא שאין long tasks.

### אנימציות (בונוס)
- שורה חדשה: `@keyframes` של slide-in + הבהוב רקע קצר.
- שינוי סטטוס: `transition` על צבע הרקע/ה-badge, והבהוב קצר בעדכון.
- `prefers-reduced-motion` מכבה אנימציות.
- צבעים: Completed = ירוק, Failed = אדום, Pending = כתום/אפור. לא להסתמך על צבע בלבד – גם טקסט/אייקון ב-badge.

## 4. רשימת Unit Tests

הרשימות מקובצות לפי רכיב. סדר הכתיבה בפועל נקבע בסעיף 7: בשלד נכתבות רק בדיקות ה-happy path, והשאר בשלבי ההקשחה. כל טבלה בסעיף 7 מפנה למספרי הבדיקות כאן.

כלים: xUnit + Shouldly + NSubstitute. כל בדיקה נכתבת כשהיא נכשלת, ואז המימוש המינימלי.

**א. Validation (`RequestValidationTests` – דרך HTTP, `WebApplicationFactory`)**

הוולידציה היא של ה-framework (DataAnnotations + JSON), ולכן בודקים אותה במקום שבו היא רצה באמת – בקשת POST שמחזירה `400`. כל בדיקה גם מוודאת שהשגיאה נמצאת על השדה הנכון ב-`errors`.
1. כל שדה חסר (null) → `400` עם שגיאה על אותו שדה (בדיקה פרמטרית `[Theory]`).
2. `transactionId` = `Guid.Empty` → `400`.
3. `amount` ≤ 0 → `400`.
4. `currency` ריק / לא 3 אותיות גדולות → `400`.
5. `status` לא מוכר → `400`.
6. `status` ב-case שונה (`"completed"`) → מתקבל.
7. JSON שבור או טיפוס שגוי (`"amount": "abc"`) → `400` עם `ValidationProblemDetails`.

**א2. Contracts / Mapping (`TransactionMappings`)**
1. `ToDomain()` מעביר את כל השדות נכון.
2. `amount` נשמר כ-`decimal` ללא איבוד דיוק (למשל 1500.50 ו-0.1+0.2).
3. `ToResponse()` מחזיר את כל השדות.
4. סריאליזציה של `TransactionResponse` ל-JSON תואמת את מבנה המטלה (camelCase, `status` כמחרוזת).

**ב. InMemoryTransactionRepository**
1. Upsert של id חדש → `Created`, ו-GetLatest מחזיר אותו.
2. Upsert של אותו id עם timestamp חדש → `Updated`, והערך הוחלף.
3. Upsert עם timestamp ישן יותר → `IgnoredStale`, הערך לא השתנה.
4. timestamp זהה → `Updated` (ראה החלטה 2 בסעיף 8).
5. מעבר Pending → Completed ו-Pending → Failed נשמר.
6. GetLatest ממוין לפי timestamp יורד ומכבד את `count`.
7. חריגה מהקיבולת – הישנה ביותר מוסרת.
8. GetLatest מחזיר snapshot: שינוי לאחר מכן לא משפיע על הרשימה שהוחזרה.

**ג. Concurrency (על ה-Repository)**

הבדיקות האלה נכתבות מול המימוש מהשלד, שלב 1 (Dictionary **בלי lock**), וחייבות להיכשל עליו לפני שמוסיפים `lock`. Race condition הוא הסתברותי, ולכן הן בנויות כך שייכשלו באופן אמין:
- **התחלה בו-זמנית:** כל ה-threads ממתינים על `Barrier` ומתחילים יחד. בלי זה הם רצים כמעט ברצף ולא מתנגשים.
- **עומס:** אלפי פעולות, והתרחיש חוזר כמה פעמים בתוך כל בדיקה.
- **Timeout:** כתיבה מקבילה ל-`Dictionary` רגיל יכולה לזרוק `InvalidOperationException` או, לעיתים נדירות, להיתקע. ה-timeout הופך היתקעות לכשל ברור.
- **כלל:** אם בדיקה עוברת על המימוש בלי lock – מחזקים את הבדיקה, לא מוסיפים lock. ה-lock נוסף רק אחרי שראינו אדום.

אחרי הוספת ה-lock הבדיקות ירוקות באופן דטרמיניסטי; רק האדום הוא הסתברותי, והוא נבדק פעם אחת.

1. 1000 Upserts מקבילים של ids שונים → בדיוק 1000 פריטים, ללא חריגה.
2. אותו id, 100 timestamps שונים בסדר אקראי ובמקביל → הנשמר הוא תמיד ה-timestamp הגדול ביותר.
3. קריאות GetLatest במקביל לכתיבות → ללא חריגה וללא פריט פגום.
4. אותו id חדש נשלח מכמה threads במקביל → בדיוק תוצאה אחת היא `Created`, כל השאר `Updated`/`IgnoredStale`.

**ד. TransactionService** (עם NSubstitute ל-Repository ול-Notifier)
1. Created → קורא ל-Repository ומשדר פעם אחת.
2. Updated → משדר.
3. IgnoredStale → **לא** משדר.
4. נכשל שידור (Notifier זורק) → ה-Transaction עדיין נשמר, והחריגה לא מפילה את הבקשה (נרשמת ללוג).
5. הנתונים המשודרים הם בדיוק אלה שנשמרו.

**ה. Integration (`WebApplicationFactory`)** – מכסה גם את ה-Controller וה-Notifier (ראה הסבר בסעיף 1)
1. POST חדש → `201` עם `IngestResponse` (`outcome: "Created"`), ו-GET מחזיר את הטרנזקציה.
2. POST לאותו id עם timestamp חדש → `200` עם `outcome: "Updated"`.
3. POST עם timestamp ישן → `200` עם `outcome: "IgnoredStale"`, ו-GET מחזיר את הערך הקודם.
4. לקוח SignalR מחובר מקבל `TransactionUpdated` אחרי POST, במבנה ה-JSON של המטלה.
5. שני לקוחות מחוברים – שניהם מקבלים.
6. Service שזורק חריגה → `500` עם `ProblemDetails` ו-`traceId`, בלי stack trace. הבדיקה מריצה את האפליקציה בסביבת `Production` (`builder.UseEnvironment("Production")`), כי `WebApplicationFactory` רץ כברירת מחדל ב-`Development`, ושם ה-Developer Exception Page היה מחזיר stack trace והבדיקה הייתה נכשלת.
7. **Concurrency מקצה לקצה:** 200 POSTs מקבילים דרך HTTP → ה-GET מחזיר את כולם; ובמקביל עדכונים לאותו id → נשמר ה-timestamp האחרון. מוכיח שכל השרשרת (Controller → Service → Repository) בטוחה, לא רק ה-Repository.
8. **ניתוק לקוח:** שני לקוחות SignalR מחוברים, לקוח A מתנתק, נשלח POST → לקוח B עדיין מקבל את האירוע, והבקשה מצליחה.

**ו. Frontend (Vitest – לוגיקה טהורה בלבד)**

Reducer:
1. snapshot ממלא את ה-state. 2. batch מוסיף חדשות. 3. עדכון ל-id קיים מחליף. 4. timestamp ישן נדחה. 5. תקרת 500 נאכפת.

`apiClient` (עם fetch מדומה):
1. `400` → `ApiError` מסוג validation עם שגיאות לפי שדה.
2. `500` → `ApiError` מסוג server.
3. fetch נכשל (רשת) → `ApiError` מסוג network.

`createBatcher` (עם fake timers של Vitest):
1. 100 הודעות שנוספות בפרק זמן קצר → `onFlush` נקרא פעם אחת, עם כל ה-100.
2. הודעות שמגיעות אחרי flush → נאספות ל-batch הבא.
3. אין הודעות → `onFlush` לא נקרא.
4. `dispose()` מבטל flush ממתין (כדי שלא יהיה עדכון state אחרי unmount).

`filterTransactions`:
1. "All" → מחזיר את כל הרשימה.
2. סטטוס ספציפי (למשל `Pending`) → רק טרנזקציות בסטטוס הזה.
3. "Errors only" → רק `Failed`.
4. שומר על הסדר של רשימת הקלט.

## 5. סנכרון בין Pods (בונוס)

**הבעיה:** כל pod מחזיק חיבורי WebSocket משלו ו-state בזיכרון משלו. POST שהגיע ל-Pod B לא מגיע ללקוח שמחובר ל-Pod A.

**הפתרון המוצע (ייכתב כ-ADR):** שתי שכבות, כי יש כאן שתי בעיות נפרדות:

1. **שידור בין pods – SignalR Redis backplane** (`AddStackExchangeRedis`). כל pod מפרסם הודעות ל-Redis Pub/Sub וכל ה-pods מעבירים אותן ללקוחות שלהם. שינוי מינימלי בקוד: שורה אחת ב-`Program.cs` מותנית בקונפיגורציה (`Redis:ConnectionString`). ללא הגדרה – ממשיך לעבוד in-memory בפיתוח מקומי.
2. **State משותף:** ה-`InMemoryTransactionRepository` הוא per-pod, לכן `GET /api/transactions` ו-ה-timestamp rule יתנהגו שונה בין pods. בייצור מחליפים אותו במימוש אחר של אותו `ITransactionRepository` (Redis / SQL עם upsert מותנה על timestamp, למשל `UPDATE ... WHERE Timestamp <= @new`). זה בדיוק הערך של ה-interface.

**חלופות שנשקלו:** Azure SignalR Service (מנהל החיבורים מחוץ ל-pods; פחות תחזוקה, עלות ותלות בענן); Kafka/RabbitMQ (כבד מדי ל-MVP); Sticky sessions (לא פותר את הבעיה – רק מקבע חיבור ל-pod).

**נקודות נוספות ל-ADR:** WebSockets בלבד (`skipNegotiation`) מבטל את הצורך ב-sticky sessions; ה-Ingress צריך לתמוך ב-WebSocket ו-timeouts ארוכים; readiness/liveness probes (`/healthz`); graceful shutdown.

**מה נממש:** ה-backplane (מותנה בקונפיגורציה) + `docker-compose` מקומי עם שני עותקים של ה-Api ו-Redis כדי להדגים. את מימוש ה-Repository המשותף נתעד ב-ADR בלבד (ראה שאלה 5).

## 6. CI ו-Deployment

### CI – GitHub Actions (חובה, משלב 0)
המטלה דורשת שהבדיקות יהיו *"executable automatically"*. קובץ `.github/workflows/ci.yml` גורם ל-GitHub להריץ את כל הבדיקות על מכונה נקייה בכל push, ולהציג ✅/❌ ליד ה-commit וב-badge ב-README.

- **trigger:** `push` בלבד (עבודה עצמאית על `main`, בלי PRs).
- **job `backend`** (משלב 0): `setup-dotnet` (8.0) → `dotnet restore` → `dotnet build` → `dotnet test`.
- **job `frontend`** (נוסף בשלב 4): `setup-node` → `npm ci` → `npm test` → `npm run build` (מוודא שגם `tsc` עובר). רץ במקביל ל-job של ה-Backend, בלי תלות ביניהם.

### Deployment (בונוס)
- **Dockerfile:** multi-stage: `node:alpine` (`npm ci` + `npm run build` ל-frontend) ו-`mcr.microsoft.com/dotnet/sdk:8.0` (publish ל-backend) – שני שלבי build בלתי תלויים → `aspnet:8.0-alpine` (runtime, משרת גם את קבצי ה-frontend הסטטיים). ה-Dockerfile יושב בשורש כי הוא בונה גם את `frontend/` וגם את `backend/`; משתמש non-root; `ASPNETCORE_HTTP_PORTS=8080`.
- **k8s:** `deployment.yaml` (3 replicas, requests/limits, probes, env ל-Redis) ו-`service.yaml` (ClusterIP; Ingress – הערה בלבד).
  - **הערה מפורשת ב-`deployment.yaml`:** ה-backplane מסנכרן את השידור בין ה-pods, אבל ה-Repository עדיין בזיכרון של כל pod, ולכן `GET /api/transactions` יחזיר תמונה שונה לפי ה-pod שענה. זה הפער שה-ADR מתאר (Repository משותף), וכתוב שם במפורש כדי שלא ייראה כמו פספוס.

## 7. שלבי מימוש (כל שלב = commit נפרד, ואני עוצר בסופו)

**הגישה: Walking Skeleton.** קודם מחברים שלד דק שעובד מקצה לקצה (טופס → API → שמירה → SignalR → דשבורד), ורק אחר כך מעבים כל שכבה. כך הסיכונים של החיבורים (proxy, SignalR מהדפדפן דרך ה-proxy, שמות שדות ב-JSON) מתגלים בהתחלה, ובכל נקודה יש מערכת שאפשר להדגים.

**גם השלד נכתב ב-TDD** – אבל רק ל-happy path. מקרי קצה (ולידציה, כלל ה-timestamp, concurrency, שגיאות) נוספים בשלב ההקשחה. ה-Repository בשלד הוא `Dictionary` בלי lock, ובדיוק עליו בדיקות ה-concurrency ייכשלו בשלב 8.

הסדר הכללי נגזר מהמטלה: **Core Requirements** ו-**Unit Tests** הם חובה (שלבים 0–14); **Bonus 4** ו-**Bonus 5** רק אחרי שה-MVP שלם, ובתוכם הפריטים שהמטלה מסמנת "Optional" באים אחרונים.

מספרי הבדיקות בטבלאות מפנים לרשימות בסעיף 4.

### א. Walking Skeleton – עסקה אחת מקצה לקצה

| # | שלב | תוצר | בדיקות | הצעת commit |
|---|-----|------|--------|-------------|
| 0 | שלד Solution | `backend/` (sln, Api, Tests), חבילות (xUnit/Shouldly/NSubstitute), `.gitignore`, בדיקת smoke, `ci.yml` (job backend), שלד `README.md` עם badge | smoke | `chore: scaffold solution, ci and readme` |
| 1 | מודל + Repository בסיסי | `Transaction`, `TransactionStatus`, `UpsertResult`, `InMemoryTransactionRepository` עם `Dictionary` רגיל, **בלי lock** | ב'1, ב'2 | `feat: transaction model and in-memory repository` |
| 2 | Service + SignalR | `TransactionService`, `SignalRTransactionNotifier`, `TransactionsHub` | ד'1, ד'2, ד'5 (ה-Notifier נבדק ב-Integration בשלב 3) | `feat: transaction service and signalr notifier` |
| 3 | API | `TransactionRequest`/`TransactionResponse`/`IngestResponse` + mappings, `TransactionsController` (POST, GET), DI, Swagger UI, `/healthz`, סעיף Security ב-README | א2'1, א2'3, א2'4, ה'1, ה'2, ה'4, ה'5 | `feat: transactions api` |
| 4 | Frontend + `/add` בסיסי | Vite + TS + Router, proxy ל-Backend, `types.ts`, טופס פשוט ששולח POST, job frontend ב-`ci.yml` | – (UI) | `feat(ui): frontend skeleton with simulator form` |
| 5 | `/monitor` בסיסי | חיבור SignalR, GET snapshot, רשימה פשוטה | – (UI) | `feat(ui): basic live monitor` |

✅ **נקודת ביקורת:** טרנזקציה שנשלחת מ-`/add` מופיעה ב-`/monitor` בזמן אמת. בדיקה ידנית בדפדפן לפני שממשיכים.

בשלד אין עדיין DataAnnotations: `status` לא מוכר או JSON שבור כבר מחזירים `400` (מה-deserialization), אבל בקשה עם שדה חסר תיכשל ב-`ToDomain()` ותחזיר `500`. זה צפוי ומתוקן בשלב 6.

### ב. הקשחת Backend

| # | שלב | תוצר | בדיקות | הצעת commit |
|---|-----|------|--------|-------------|
| 6 | ולידציה | DataAnnotations + `IValidatableObject` על `TransactionRequest`, `400` עם `ValidationProblemDetails` | א' (כולן), א2'2 | `feat: request validation` |
| 7 | כלל ה-timestamp וקיבולת | `IgnoredStale`, timestamp זהה, מעברי סטטוס, מיון, snapshot, תקרת 1000 (+ המגבלה הידועה ב-README) | ב'3–ב'8, ד'3, ה'3 | `feat: timestamp rule and capacity` |
| 8 | Concurrency | בדיקות ג' → מוכח שהן נכשלות על ה-Repository מהשלד → הוספת `lock` → ירוק | ג' (כולן), ה'7 | `fix: make repository thread-safe with lock` (גוף ה-commit מתאר במה נכשלו הבדיקות בלי lock) |
| 9 | שגיאות ועמידות | `AddProblemDetails` + `UseExceptionHandler`, כשל שידור לא מפיל בקשה, ניתוק לקוח | ד'4, ה'6, ה'8 | `feat: global error handling` |

### ג. השלמת Frontend

| # | שלב | תוצר | בדיקות | הצעת commit |
|---|-----|------|--------|-------------|
| 10 | State + apiClient | `transactionsReducer` (כולל כלל ה-timestamp), `apiClient`, `ApiError` | ו' – reducer, apiClient | `feat(ui): reducer and api client` |
| 11 | `/add` מלא | generator, burst של 100, שגיאות לפי שדה, `SubmitResult`, הודעת `IgnoredStale` | – (UI) | `feat(ui): complete simulator` |
| 12 | `/monitor` מלא | `StatusBadge`, `FilterBar` + `filterTransactions`, `ConnectionStatus` (retry ללא הגבלה), `ErrorBanner`, טעינה מחדש אחרי reconnect | ו' – filterTransactions | `feat(ui): statuses, filters and reconnect` |
| 13 | ביצועים | `createBatcher`, תקרת 500, `React.memo`, מדידה עם burst | ו' – createBatcher | `perf(ui): batch updates` |
| 14 | ליטוש README | קריאה מלאה, תיקון חזרות, ווידוא הוראות הרצה מ-clone נקי | – | `docs: polish readme` |

**נקודת חיתוך:** בסיום שלב 14 ה-MVP שלם וניתן להגשה. כל מה שאחריו הוא בונוס.

### ד. בונוסים (סעיפים 4 ו-5 במטלה)

הסדר נקבע לפי יחס ערך/עלות: קודם מה שהמטלה מבקשת במפורש בתוך הבונוס (ADR, Docker, k8s), אחר כך אנימציות (זול ובולט מאוד לבודק), ובסוף ה-Redis backplane – היקר והמסוכן ביותר, ומסומן במטלה "Optional".

| # | שלב | דרישה במטלה | תוצר | הצעת commit |
|---|-----|-------------|------|-------------|
| 15 | ADR – סנכרון בין pods | 4.1 Architect (חובה בתוך הבונוס) | `docs/adr/0001-multi-pod-sync.md` + קישור מה-README | `docs: adr for multi-pod sync` |
| 16 | Dockerfile | 4.2 Deployment | multi-stage, image קטן, non-root | `chore: dockerfile` |
| 17 | Kubernetes manifests | 4.2 Deployment | `k8s/deployment.yaml` (כולל ההערה על Repository per-pod), `k8s/service.yaml` | `chore: k8s manifests` |
| 18 | אנימציות | 5 Animations | CSS animations לשורה חדשה ולמעבר סטטוס + reduced-motion, תוך שמירה על הביצועים משלב 13 | `feat(ui): animations` |
| 19 | Redis backplane | 4.1 Implement (Optional but recommended) | קונפיגורציה מותנית + docker-compose | `feat: redis backplane` |
| 20 | README סופי | הגשה | עדכון README: Docker/k8s, Redis, הפניה ל-ADR | `docs: update readme` |

**כלל קבלת החלטות אם נגמר הזמן:** מוותרים מהסוף להתחלה (19 ← 18 ← 17...), אבל שלב 20 (README סופי) תמיד נעשה. ה-ADR (שלב 15) נשאר גם אם לא מתקדמים לקוד של הבונוס, כי הוא התיאור שהמטלה דורשת.

### כללי עבודה – TDD ו-commits

**commit אחד לכל שלב, ורק כשהשלב ירוק.** כל commit בהיסטוריה מתקמפל ועובר את כל הבדיקות.

**שלב עם בדיקות (0–3, 6–10, 12, 13) – מחזור TDD בתוך השלב:**
1. כותב את הבדיקות.
2. מריץ ומציג לך את הפלט שמוכיח שהן נכשלות (Red).
3. מימוש מינימלי שמעביר אותן, בלי קוד שאף בדיקה לא דורשת.
4. מריץ את כל הבדיקות ומציג שהן עוברות (Green).
5. **עצירה** – את סוקרת ועושה commit אחד.

**שלב 8 (concurrency):** ההוכחה שהבדיקות נכשלו בלי lock נשמרת בגוף ה-commit message, למשל:
```
fix: make repository thread-safe with lock

Concurrency tests failed against the lock-free Dictionary
(InvalidOperationException / lost updates); all pass with a single lock.
```

**שלב בלי בדיקות אוטומטיות (4, 5, 11, 14–20: קומפוננטות UI, CSS, תיעוד, Docker, k8s):** מימוש → **עצירה** → commit.

**ב-README** (סעיף הבדיקות), משפט אחד: *"Developed test-first: each step's tests were written and seen failing before the implementation."*

**עדכון README בכל שלב:** כחלק מה-commit של השלב אני מעדכן את החלק הרלוונטי ב-`README.md`: איך מריצים מה שנוסף, ואיזו החלטה התקבלה ולמה. העדכון מופיע ב-diff יחד עם הקוד, כך שאת סוקרת אותו לפני ה-commit. אין כאן שום אוטומציה – זה חלק מהגדרת "שלב גמור".

**סעיף Key Decisions ב-README:** ההחלטות הארכיטקטוניות מתועדות כנקודות קצרות (משפט–שניים כל אחת) בסעיף אחד ב-README, ולא כ-ADRs נפרדים – בפרויקט בגודל הזה ADR לכל החלטה הוא טקס ולא תיעוד. כל נקודה נכתבת בשלב שבו ההחלטה מתקבלת:

| שלב | החלטה |
|-----|-------|
| 0 | Walking Skeleton – מקצה לקצה קודם, הקשחה אחר כך |
| 2 | SignalR ולא WebSockets גולמיים – reconnect אוטומטי, fallback ל-transports, ו-Redis backplane מובנה לריבוי pods |
| 3 | DTO נפרד מהדומיין – הבחנה בין "חסר" ל-0, דומיין immutable |
| 3 | אין CORS – Frontend ו-API תמיד באותו origin (proxy בפיתוח, אותו container בייצור) |
| 3 | Controller ו-Notifier נבדקים ב-Integration ולא ב-unit עם mocks – הם שכבה דקה, והערך הוא בבדיקה מול ה-pipeline האמיתי |
| 6 | ולידציה ב-DataAnnotations ו-enum ב-DTO, לא ולידטור ידני – פחות קוד, ופורמט שגיאה אחד לכל סוגי הקלט השגוי |
| 4 | Frontend לפי feature |
| 4 | טיפוסי ה-API נכתבים ידנית ב-`types.ts` – בשני endpoints זה פשוט יותר מ-code generation. במערכת גדולה יותר הטיפוסים ייווצרו מ-OpenAPI, כדי למנוע סטייה בין ה-Client ל-Server |
| 8 | `lock` יחיד ב-Repository – check-and-set אטומי, פשוט יותר מ-CAS על `ConcurrentDictionary` |
| 13 | Batching בצד הלקוח – 100 הודעות = render אחד |

**ADR יחיד:** `docs/adr/0001-multi-pod-sync.md` בשלב 15 – זה מה שהמטלה מבקשת במפורש, וזו ההחלטה היחידה שמשנה את הארכיטקטורה. ה-README מקשר אליו.

לא מריץ `git commit`/`git push` – את עושה commit בעצמך בכל עצירה.

## 8. הנחות ושאלות פתוחות

### הוחלט
1. **מבנה פרויקטים:** Api אחד + Tests אחד (ולא Domain/Application/Infrastructure נפרדים).
2. **Timestamp זהה:** עדכון לאותו `transactionId` עם timestamp זהה לשמור **מתקבל** (`Updated`); רק timestamp ישן יותר נדחה. כך לא הולך לאיבוד מעבר כמו Pending → Completed באותה שנייה, ו-retry של אותה בקשה לא נכשל. אם יגיעו שני עדכונים שונים עם אותו timestamp, האחרון שהגיע קובע. ה-timestamp אינו ייחודי – טרנזקציות שונות עם אותו timestamp נשמרות כרגיל.
3. **מעברי סטטוס:** לא אוכפים lifecycle. הכלל היחיד הוא ה-timestamp, ולכן גם Completed → Pending או Failed → Completed מתקבלים אם ה-timestamp חדש יותר. המערכת היא מוניטור ולא מערכת תשלומים: מקור האמת הוא המערכת החיצונית, ויש מעברים לגיטימיים (למשל ניסיון חוזר שהצליח אחרי Failed). מתועד במשפט ב-README.
5. **בונוס Redis:** רק ה-backplane + docker-compose. ה-Repository המשותף מתועד ב-ADR בלבד. ה-backplane פותר את הבעיה שהמטלה מתארת (לקוח ב-Pod A רואה בזמן אמת POST שהגיע ל-Pod B). Repository ב-Redis מחייב upsert מותנה אטומי (Lua script), פינוי דרך sorted set ו-Redis בבדיקות וב-CI – הרבה עלות לפריט שהמטלה מסמנת "Optional".
7. **שפת ה-UI:** אנגלית, LTR.
8. **Virtualization:** מתחילים בלי `react-window`. 500 שורות הן DOM קטן לדפדפן, ו-virtualization מסבך את אנימציות הכניסה (שורות נטענות ומוסרות בגלילה). מוסיפים רק אם המדידה עם ה-burst תראה long tasks.
6. **בדיקות Frontend:** Vitest ללוגיקה טהורה בלבד (reducer, `apiClient`, `createBatcher`, `filterTransactions`), בלי בדיקות קומפוננטות. הסינון מוצא לפונקציה טהורה כדי שגם דרישת ה-filtering מהמטלה תהיה מכוסה בבדיקה.
4. **קיבולת:** השרת שומר את 1000 הטרנזקציות האחרונות, וה-UI מציג 500. המטלה מבקשת לשמור את ה-"latest" בזיכרון, ובלי תקרה הזיכרון גדל ללא הגבלה (ב-k8s זה נגמר ב-OOMKilled). שני המספרים מוגדרים בקונפיגורציה. SQLite לשמירת הכול נשקל ונדחה: הוא מוסיף DB, migrations ו-concurrency של DB, בלי ערך לדשבורד שמציג את המצב הנוכחי.

כל השאלות הפתוחות נסגרו.
