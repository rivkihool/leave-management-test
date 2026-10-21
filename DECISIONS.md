<div dir="rtl">

# DECISIONS

## 1. החלטות ארכיטקטוניות

**Concurrency — Optimistic Locking**
- `[Timestamp] RowVersion` ב-LeaveRequest עם [migration](backend/src/LeaveManagement.Api/Migrations/20261021181131_AddRowVersionToLeaveRequest.cs)
- EF Core בודק את ה-RowVersion בכל SaveChanges() — זורק `DbUpdateConcurrencyException` אם שנוי במקביל
- SQLite: BLOB, PostgreSQL: xmin
- **למה**: גילוי race conditions בלי pessimistic locks

**Error Handling**
- 200/400/404/409/500 עם context (quota, status, etc.)

**DTOs + Controllers** 
- `CreateLeaveRequestDto` — לא ניתן לשלוח Status/RowVersion
- Controllers ישרים ל-DbContext (POC) — בעתיד service layer

---

## 2. הבאג ביתרת החופשה

**מה היה הבאג**
- [LeaveRequestsController.cs L58](backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs#L58):
  ```csharp
  if (dto.Type == LeaveType.Vacation && days > employee.AnnualQuota)
  ```
- ❌ בדק רק הימים החדשים, לא את הסכום הכולל!
- תרחיש: quota=20, used=18, new=5 → 5>20 מתקבל, אבל סה"כ=23 ❌

**איך תיקנתי**
- ```csharp
  var used = _db.LeaveRequests
      .Where(r => r.EmployeeId == dto.EmployeeId && r.Type == LeaveType.Vacation && r.Status == LeaveStatus.Approved)
      .Sum(r => r.Days);
  if ((used + days) > employee.AnnualQuota)
  ```
- ✅ בדיקה של סכום כולל + הודעה מפורטת

**הטסט**
- [Create_ExceedsQuota_ReturnsBadRequest](backend/tests/LeaveManagement.Tests/LeaveRequestsTests.cs#L49-L76): quota=20, used=18, try 4 → BadRequest ✅

---

## 3. אישור בקשה (approve) ו-concurrency

**מצבים לא חוקיים**
- [LeaveRequestsController.cs L87-L166](backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs#L87-L166)
- 404: בקשה לא קיימת
- 400: כבר אושרה/נדחתה
- 400: vacation חוצה quota (בדיקה זהה ל-Create)
- 500: database error

**אישור בו-זמני (Race Condition)**
- שני משתמשים קוראים את אותה בקשה עם RowVersion=X
- שניהם מנסים לאשר
- הראשון שומר → RowVersion=Y
- השני מנסה לשמור → `DbUpdateConcurrencyException`
- תוצאה: 409 Conflict + "Please refresh"

**7 בדיקות**
- Approve_PendingRequest_Succeeds
- Approve_NonExistentRequest_ReturnsNotFound (404)
- Approve_AlreadyApprovedRequest_ReturnsBadRequest (400)
- Approve_RejectedRequest_ReturnsBadRequest (400)
- Approve_VacationRequestExceedsQuota_ReturnsBadRequest (400)
- Approve_VacationRequestWithinQuota_Succeeds
- Approve_SickLeaveRequest_IgnoresQuota

---

## 4. דרישה 3 & 6 — לא הספקתי

**דרישה 3 — שיפור ארכיטקטוני Backend**
- ❌ Controller עדיין עושה הכל (validation + logic + DB)
- **למה לא**: POC צריך speed, לא abstraction layers
- **עם עוד יום**: Service layer, Repository pattern

**דרישה 6 — שיפור קומפוננטה Frontend**
- ✅ Approve button + validators בטופס
- ❌ loading/error/success states עדיין חלקיים (TODO בקוד)
- ✅ `approvalSuccess`, `approvalErrors` maps — per-request tracking
- ❌ עושה blind `load()` בכל אישור (צריך optimistic update + proper state)
- **למה לא**: עדיפות — backend חשוב יותר, Frontend POC
- **עם עוד יום**: RxJS, ngrx, retry logic

---

## 5. שימוש ב-AI

**איפה AI עזר (כולל prompts אמיתיים)**

**Prompt 1 — דרישה 4 (Frontend Validation)**
```
Prompt: טופס הגשת בקשת חופשה עם Validation מלא

הקשר
פרויקט Angular 17 Standalone Components לניהול בקשות חופשה.

Frontend: frontend/src/app/
קומפוננטה ראשית: leave-requests/leave-requests.component.ts, .html, .css
מודלים: models/leave-request.model.ts

בעיה קיימת
קובץ leave-requests.component.html מכיל TODO:
<!-- TODO: add a form for submitting new leave requests here -->

דרישה
Validation טופס הגשת בקשה:
- תאריך ההתחלה אינו מאוחר מתאריך הסיום
- חובה לבחור סוג חופשה
- אין מספר ימים שלילי
- הודעות שגיאה ברורות
```
- קיבלתי: Custom validators framework
- עשיתי: Implemented [dateRangeValidator](frontend/src/app/leave-requests/validators/date-range.validator.ts) + [noDaysValidator](frontend/src/app/leave-requests/validators/no-days.validator.ts) + proper error messages per-field

**Prompt 2 — דרישה 5 (Approve Button UX)**
```
Prompt: שיפור UX של כפתור האישור (Approve Button)

הקשר
פרויקט Angular 17 Standalone Components לניהול בקשות חופשה.

קומפוננטה: frontend/src/app/leave-requests/leave-requests.component.ts, .html, .css
רשימת בקשות מוצגת בטבלה/רשימה
כל בקשה ב-status Pending יכולה להיות אושרת

בעיה קיימת
קובץ leave-requests.component.html מכיל TODO:
<!-- TODO: wire up the approve button properly (currently naive POST) -->

דרישה
כפתור האישור מחובר כרגע בצורה נאיבית (בלי רענון כל הרשימה, בלי POST)
שדרגו לטיפול נכון: מצב טעינה, טיפול בשגיאה (אישור שנכשל כי בקשה כבר אושרה), 
משוב הצלחה — בלי generic alert ובלי רענון עיוור

תממש לי בצורה מקצועית ונקיה
```
- קיבלתי: Basic button state management code
- עשיתי: Implemented per-request [approvingIds](frontend/src/app/leave-requests/leave-requests.component.ts#L20), [approvalSuccess](frontend/src/app/leave-requests/leave-requests.component.ts#L22), [approvalErrors](frontend/src/app/leave-requests/leave-requests.component.ts#L23) maps + inline per-request feedback (אין generic alert)

**איפה AI טעה וקלטתי:**

**AI הציע — סדר העדיפויות**
- Prompt: "תביא לי את הדרישות לפי סדר העדיפויות"
- קיבלתי: הביא לפי הסדר שכתוב בקובץ ההדרכה (לא נכון!)
- ❌ למה שגוי: AI קלט את הסדר המילולי, לא את חשיבות הטיפול
- תיקנתי: "תביא לפי חשיבות הטיפול, ולא לפי הסדר שקיים בקובץ הדרכה"
- ✅ קיבלתי: סידר בצורה נכונה את סדר המשימות לטיפול

**סה"כ AI usage:** 2 prompts אמיתיים + 1 correction

**אבטחה — בעיות שמצאנו ותיקנו**

**בעיה 1: Quota Validation שגויה**
- **איפה**: [LeaveRequestsController.cs L58](backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs#L58)
- **מה מצאנו**: בדיקת quota בדקה רק הימים החדשים, לא את הסכום הכולל
- **למה זו בעיה**: עובד יכול לחמוק מהקוטה השנתית
- **איך תיקנו**: שינו מ-`days > quota` ל-`(used + days) > quota`
- **בדיקה**: [Create_ExceedsQuota_ReturnsBadRequest](backend/tests/LeaveManagement.Tests/LeaveRequestsTests.cs#L49-L76)

**בעיה 2: Double-Approval (Race Condition)**
- **איפה**: [LeaveRequestsController.cs L87-L166](backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs#L87-L166)
- **מה מצאנו**: שני admins יכולים לאשר את אותה בקשה בו-זמנית
- **למה זו בעיה**: הוצאה כפולה של חופשה מהמערכת
- **איך תיקנו**: הוסיפו `[Timestamp] RowVersion` ב-[migration](backend/src/LeaveManagement.Api/Migrations/20261021181131_AddRowVersionToLeaveRequest.cs), Approve בודק `DbUpdateConcurrencyException`
- **בדיקה**: כל 7 בדיקות Approve (409 Conflict בריצה שנייה)

---

## 6. הוראות הרצה

**SQLite (no Docker)**:
```powershell
cd backend
$env:Database__Provider="Sqlite"
dotnet run --project src/LeaveManagement.Api
dotnet test
```

**PostgreSQL (Docker required)**:
```powershell
docker compose up -d
cd backend
dotnet run --project src/LeaveManagement.Api
dotnet test
```

Swagger: http://localhost:5080/swagger

---

## Summary

✅ **Implemented**: Bug fix + Approve endpoint + concurrency + 9 tests + migrations + SQLite/PostgreSQL

✅ **Verified**: All tests pass, manual testing works, error handling complete

❌ **Not Implemented**: Backend service layer (deferral for POC), Frontend proper state management (deferral for time)

</div>
