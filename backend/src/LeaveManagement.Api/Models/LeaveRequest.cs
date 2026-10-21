using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Api.Models;

public enum LeaveType
{
    Vacation = 0,
    Sick = 1,
    Unpaid = 2
}

public enum LeaveStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public LeaveType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    // Number of calendar days the request covers (inclusive).
    public int Days { get; set; }

    // Concurrency token for optimistic concurrency control
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
