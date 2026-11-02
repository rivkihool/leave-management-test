using LeaveManagement.Api.Models;
using LeaveManagement.Api.Repositories;
using LeaveManagement.Api.Validators;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Services;

public interface ILeaveRequestService
{
    Task<List<LeaveRequest>> GetAllAsync();
    Task<LeaveRequest> CreateAsync(CreateLeaveRequestDto dto, Employee employee);
    Task<LeaveRequest> ApproveAsync(int id);
}

public class LeaveRequestService : ILeaveRequestService
{
    private readonly ILeaveRequestRepository _repository;
    private readonly VacationQuotaValidator _quotaValidator;

    public LeaveRequestService(ILeaveRequestRepository repository, VacationQuotaValidator quotaValidator)
    {
        _repository = repository;
        _quotaValidator = quotaValidator;
    }

    public async Task<List<LeaveRequest>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<LeaveRequest> CreateAsync(CreateLeaveRequestDto dto, Employee employee)
    {
        var days = (dto.EndDate - dto.StartDate).Days + 1;

        // Validate vacation quota if this is a vacation request
        if (dto.Type == LeaveType.Vacation)
        {
            var (isValid, errorMessage, _, _) = await _quotaValidator
                .ValidateVacationQuotaAsync(dto.EmployeeId, days, employee.AnnualQuota);

            if (!isValid)
            {
                throw new InvalidOperationException(errorMessage);
            }
        }

        var request = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Days = days,
            Status = LeaveStatus.Pending
        };

        await _repository.AddAsync(request);
        await _repository.SaveAsync();

        return request;
    }

    public async Task<LeaveRequest> ApproveAsync(int id)
    {
        var request = await _repository.GetByIdAsync(id);

        if (request == null)
        {
            throw new KeyNotFoundException($"Leave request with ID {id} not found");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot approve a leave request with status '{request.Status}'. Only Pending requests can be approved.");
        }

        // For vacation requests, validate the employee has sufficient balance
        if (request.Type == LeaveType.Vacation && request.Employee != null)
        {
            var (isValid, errorMessage, _, _) = await _quotaValidator
                .ValidateVacationQuotaAsync(request.EmployeeId, request.Days, request.Employee.AnnualQuota);

            if (!isValid)
            {
                throw new InvalidOperationException(errorMessage);
            }
        }

        request.Status = LeaveStatus.Approved;

        try
        {
            await _repository.SaveAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Reload the entity to see current state
            await _repository.GetByIdAsync(id);
            throw new InvalidOperationException(
                "The leave request was modified by another user. Please refresh and try again.");
        }

        return request;
    }
}
