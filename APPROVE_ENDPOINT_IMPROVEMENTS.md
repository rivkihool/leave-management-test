# Approve Endpoint - Error Handling & Concurrency Implementation

## Overview
Enhanced the `Approve` endpoint with comprehensive error handling and optimistic concurrency control.

## Changes Made

### 1. Model Updates: `LeaveRequest.cs`
- Added `RowVersion` property with `[Timestamp]` attribute for optimistic concurrency control
- Added `using System.ComponentModel.DataAnnotations;`
- RowVersion is automatically managed by EF Core and incremented on each update

```csharp
[Timestamp]
public byte[]? RowVersion { get; set; }
```

### 2. Database Configuration: `LeaveDbContext.cs`
- Configured RowVersion as row version in `OnModelCreating`
- EF Core now tracks concurrency changes and throws `DbUpdateConcurrencyException` when conflicts occur

```csharp
modelBuilder.Entity<LeaveRequest>()
    .Property(r => r.RowVersion)
    .IsRowVersion();
```

### 3. Enhanced Approve Endpoint: `LeaveRequestsController.cs`
Implemented comprehensive 4-step validation and error handling:

#### Step 1: Request Lookup
- Returns `404 NotFound` if leave request doesn't exist
- Includes Employee details via `.Include()`

#### Step 2: Status Validation
- Returns `400 BadRequest` if request is not in `Pending` status
- Includes current status in error response

#### Step 3: Quota Validation (Vacation Only)
- For vacation requests, validates employee has sufficient annual quota
- Calculates already-approved vacation days
- Returns `400 BadRequest` with detailed balance information if quota exceeded:
  - `used`: Days already approved
  - `requested`: Days being approved
  - `available`: Remaining quota
  - `quota`: Total annual quota

#### Step 4: Concurrency-Safe Update
- Handles `DbUpdateConcurrencyException` with `409 Conflict` response
- Provides user-friendly message for refresh-and-retry workflow
- Catches `DbUpdateException` for other database errors
- Returns `200 OK` with success message on successful approval

### Error Response Formats

**Not Found (404):**
```json
{
  "error": "Leave request not found"
}
```

**Invalid Status (400):**
```json
{
  "error": "Cannot approve a leave request with status 'Approved'",
  "currentStatus": "Approved"
}
```

**Insufficient Quota (400):**
```json
{
  "error": "Insufficient vacation balance",
  "used": 18,
  "requested": 4,
  "available": 2,
  "quota": 20
}
```

**Concurrency Conflict (409):**
```json
{
  "error": "The leave request was modified by another user. Please refresh and try again.",
  "conflictDetails": "..."
}
```

**Success (200):**
```json
{
  "message": "Leave request approved successfully",
  "data": { /* LeaveRequest object */ }
}
```

## Tests Added: `LeaveRequestsTests.cs`

7 comprehensive test cases covering:

1. **Approve_PendingRequest_Succeeds** - Happy path approval
2. **Approve_NonExistentRequest_ReturnsNotFound** - 404 handling
3. **Approve_AlreadyApprovedRequest_ReturnsBadRequest** - Status validation
4. **Approve_RejectedRequest_ReturnsBadRequest** - Rejected status handling
5. **Approve_VacationRequestExceedsQuota_ReturnsBadRequest** - Quota validation
6. **Approve_VacationRequestWithinQuota_Succeeds** - Quota approval
7. **Approve_SickLeaveRequest_IgnoresQuota** - Non-vacation type handling

## Benefits

- **Concurrency Safety**: Optimistic concurrency prevents race conditions
- **Clear Error Messages**: Clients know exactly why requests failed
- **Business Logic**: Prevents quota overages and invalid state transitions
- **User Experience**: Provides actionable information for UI retry logic
- **Maintainability**: Follows ASP.NET Core best practices with proper exception handling

## Database Migration Note
A database migration is required to add the `RowVersion` column:
```bash
dotnet ef migrations add AddRowVersionToLeaveRequest
dotnet ef database update
```

For SQLite: The RowVersion is stored as a BLOB (byte array).
For PostgreSQL: Uses the native xmin system column if configured.
