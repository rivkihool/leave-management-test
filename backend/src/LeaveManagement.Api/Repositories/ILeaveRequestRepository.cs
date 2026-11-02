using LeaveManagement.Api.Models;

namespace LeaveManagement.Api.Repositories;

public interface ILeaveRequestRepository
{
    Task<LeaveRequest?> GetByIdAsync(int id);
    Task<List<LeaveRequest>> GetAllAsync();
    Task<int> GetUsedVacationDaysAsync(int employeeId);
    Task AddAsync(LeaveRequest request);
    Task SaveAsync();
}
