using LeaveManagement.Api.Controllers;
using LeaveManagement.Api.Models;
using LeaveManagement.Api.Repositories;
using LeaveManagement.Api.Services;
using LeaveManagement.Api.Validators;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LeaveManagement.Tests;

public class LeaveRequestsTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _database;

    public LeaveRequestsTests(TestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task Create_WithinQuota_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var repository = new LeaveRequestRepository(db);
        var validator = new VacationQuotaValidator(repository);
        var service = new LeaveRequestService(repository, validator);

        var dto = new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        };

        // Act & Assert
        var result = await service.CreateAsync(dto, emp);
        Assert.NotNull(result);
        Assert.Equal(LeaveStatus.Pending, result.Status);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public async Task Create_ExceedsQuota_ThrowsException()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Add an existing approved leave request for 18 days
        var existingRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 18),
            Days = 18,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(existingRequest);
        db.SaveChanges();

        var repository = new LeaveRequestRepository(db);
        var validator = new VacationQuotaValidator(repository);
        var service = new LeaveRequestService(repository, validator);

        var dto = new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 4)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(dto, emp));
        Assert.Contains("Not enough vacation balance", ex.Message);
        Assert.Equal(1, db.LeaveRequests.Count());
    }

    // ============ APPROVE ENDPOINT TESTS ============

    [Fact]
    public async Task Approve_PendingRequest_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(leaveRequest);
        db.SaveChanges();

        var repository = new LeaveRequestRepository(db);
        var validator = new VacationQuotaValidator(repository);
        var service = new LeaveRequestService(repository, validator);

        // Act
        var result = await service.ApproveAsync(leaveRequest.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(LeaveStatus.Approved, result.Status);
    }

    [Fact]
    public async Task Approve_NonExistentRequest_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var db = _database.NewDb();

        var repository = new LeaveRequestRepository(db);
        var validator = new VacationQuotaValidator(repository);
        var service = new LeaveRequestService(repository, validator);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ApproveAsync(999));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task Approve_AlreadyApprovedRequest_ThrowsException()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(leaveRequest);
        db.SaveChanges();

        var repository = new LeaveRequestRepository(db);
        var validator = new VacationQuotaValidator(repository);
        var service = new LeaveRequestService(repository, validator);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(leaveRequest.Id));
        Assert.Contains("Cannot approve", ex.Message);
    }

    [Fact]
    public async Task Approve_RejectedRequest_ThrowsException()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Rejected
        };
        db.LeaveRequests.Add(leaveRequest);
        db.SaveChanges();

        var repository = new LeaveRequestRepository(db);
        var validator = new VacationQuotaValidator(repository);
        var service = new LeaveRequestService(repository, validator);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(leaveRequest.Id));
        Assert.Contains("Cannot approve", ex.Message);
    }

    [Fact]
    public async Task Approve_VacationRequestExceedsQuota_ThrowsException()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Add an existing approved vacation request for 18 days
        var approvedRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 18),
            Days = 18,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(approvedRequest);

        // Add a pending request for 4 additional days (would exceed quota)
        var pendingRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 4),
            Days = 4,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(pendingRequest);
        db.SaveChanges();

        var repository = new LeaveRequestRepository(db);
        var validator = new VacationQuotaValidator(repository);
        var service = new LeaveRequestService(repository, validator);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(pendingRequest.Id));
        Assert.Contains("Not enough vacation balance", ex.Message);
        
        // Request should still be Pending
        var unchangedRequest = db.LeaveRequests.First(r => r.Id == pendingRequest.Id);
        Assert.Equal(LeaveStatus.Pending, unchangedRequest.Status);
    }

    [Fact]
    public async Task Approve_VacationRequestWithinQuota_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Add an existing approved vacation request for 15 days
        var approvedRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 15),
            Days = 15,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(approvedRequest);

        // Add a pending request for 5 days (within quota)
        var pendingRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(pendingRequest);
        db.SaveChanges();

        var repository = new LeaveRequestRepository(db);
        var validator = new VacationQuotaValidator(repository);
        var service = new LeaveRequestService(repository, validator);

        // Act
        var result = await service.ApproveAsync(pendingRequest.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(LeaveStatus.Approved, result.Status);
    }

    [Fact]
    public async Task Approve_SickLeaveRequest_IgnoresQuota()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        // Add an existing approved vacation request for 20 days (full quota)
        var approvedRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 20),
            Days = 20,
            Status = LeaveStatus.Approved
        };
        db.LeaveRequests.Add(approvedRequest);

        // Add a pending sick leave request (should not count against vacation quota)
        var sickLeaveRequest = new LeaveRequest
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Sick,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 5),
            Days = 5,
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(sickLeaveRequest);
        db.SaveChanges();

        var repository = new LeaveRequestRepository(db);
        var validator = new VacationQuotaValidator(repository);
        var service = new LeaveRequestService(repository, validator);

        // Act
        var result = await service.ApproveAsync(sickLeaveRequest.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(LeaveStatus.Approved, result.Status);
    }
}

