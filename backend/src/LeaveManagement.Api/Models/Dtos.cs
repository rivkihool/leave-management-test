namespace LeaveManagement.Api.Models;

// Incoming payload for creating a leave request.
public class CreateLeaveRequestDto
{
    public int EmployeeId { get; set; }
    public LeaveType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Days { get; set; }
}
