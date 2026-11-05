import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, timer, Observable } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { LeaveRequest, LeaveType, LeaveStatus } from '../models/leave-request.model';
import { LeaveRequestService } from '../services/leave-request.service';
import { LeaveRequestStateService, ComponentState } from '../services/leave-request-state.service';
import { dateRangeValidator } from './validators/date-range.validator';

@Component({
  selector: 'app-leave-requests',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './leave-requests.component.html',
  styleUrls: ['./leave-requests.component.css']
})
export class LeaveRequestsComponent implements OnInit, OnDestroy {
  // Expose enums to template
  readonly LeaveType = LeaveType;
  readonly LeaveStatus = LeaveStatus;

  form!: FormGroup;
  showForm = false; // Toggle form visibility

  // State stream from state service — properly typed Observable
  state$!: Observable<ComponentState>;

  private destroy$ = new Subject<void>();

  constructor(
    private leaveRequestService: LeaveRequestService,
    private stateService: LeaveRequestStateService,
    private fb: FormBuilder
  ) {}

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  ngOnInit(): void {
    this.initForm();
    this.state$ = this.stateService.getState();
    this.loadRequests();
  }

  private initForm(): void {
    this.form = this.fb.group(
      {
        employeeId: ['', [Validators.required]],
        type: ['', [Validators.required]],
        startDate: ['', [Validators.required]],
        endDate: ['', [Validators.required]],
        days: ['', [Validators.required, this.validateDays.bind(this)]]
      },
      {
        validators: [dateRangeValidator(), this.validateDaysMatch.bind(this)]
      }
    );
  }

  /**
   * Custom validator for days field - checks for negative and minimum value
   */
  private validateDays(control: any) {
    const value = control.value;
    
    if (value === null || value === undefined || value === '') {
      return null; // required validator handles this
    }

    const numValue = Number(value);

    // Check if negative
    if (numValue < 0) {
      return { negative: true };
    }

    // Check if less than 1
    if (numValue < 1) {
      return { min: { min: 1 } };
    }

    return null;
  }

  /**
   * Form-level validator to check if days match the date range
   */
  private validateDaysMatch(formGroup: any) {
    const startDate = formGroup.get('startDate')?.value;
    const endDate = formGroup.get('endDate')?.value;
    const days = formGroup.get('days')?.value;

    if (!startDate || !endDate || !days) {
      return null; // Don't validate if fields are empty
    }

    const start = new Date(startDate);
    const end = new Date(endDate);
    const calculatedDays = Math.floor((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24)) + 1;
    const inputDays = Number(days);

    if (calculatedDays !== inputDays) {
      return { daysMismatch: { expected: calculatedDays, provided: inputDays } };
    }

    return null;
  }

  /**
   * Load all leave requests
   */
  loadRequests(): void {
    this.stateService.setLoading(true);

    this.leaveRequestService.getAll()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.stateService.setRequests(data);
        },
        error: (error) => {
          const errorMsg = this.extractErrorMessage(error) || 'Failed to load leave requests';
          this.stateService.setError(errorMsg);
        }
      });
  }

  /**
   * Submit new leave request
   */
  onSubmit(): void {
    if (!this.form.valid) {
      return;
    }

    this.stateService.setSubmitting(true);

    const formValue = this.form.value;
    const payload = {
      employeeId: parseInt(formValue.employeeId, 10),
      type: parseInt(formValue.type, 10) as LeaveType,
      startDate: this.formatDateForApi(formValue.startDate),
      endDate: this.formatDateForApi(formValue.endDate),
      days: parseInt(formValue.days, 10)
    };

    this.leaveRequestService.create(payload)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.form.reset();
          this.toggleForm();
          // Reload requests after successful creation
          this.loadRequests();
          this.stateService.setSubmitting(false);
        },
        error: (error) => {
          const errorMsg = this.extractErrorMessage(error) || 'Failed to submit leave request';
          this.stateService.setError(errorMsg);
          this.stateService.setSubmitting(false);
        }
      });
  }

  /**
   * Approve a pending leave request
   */
  approve(id: number): void {
    this.stateService.setApprovingId(id, true);
    this.stateService.setApprovalError(id, null);
    this.stateService.setApprovalSuccess(id, false);

    this.leaveRequestService.approve(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          // Update request status locally
          this.stateService.updateRequest(id, { status: LeaveStatus.Approved });
          this.stateService.setApprovalSuccess(id, true);

          // Auto-clear success message after 3 seconds using RxJS timer
          timer(3000)
            .pipe(takeUntil(this.destroy$))
            .subscribe(() => {
              this.stateService.setApprovalSuccess(id, false);
            });
        },
        error: (error) => {
          const errorMsg = this.extractErrorMessage(error) || 'Failed to approve request';
          this.stateService.setApprovalError(id, errorMsg);
          this.stateService.setApprovingId(id, false); // Remove loading state immediately

          // Auto-clear error message after 5 seconds using RxJS timer
          timer(5000)
            .pipe(takeUntil(this.destroy$))
            .subscribe(() => {
              this.stateService.setApprovalError(id, null);
            });
        },
        complete: () => {
          // Always remove loading state, whether success or error
          this.stateService.setApprovingId(id, false);
        }
      });
  }

  /**
   * Toggle form visibility
   */
  toggleForm(): void {
    const currentState = this.stateService.getCurrentState();
    
    if (currentState.submitting) {
      return; // Prevent toggling while submitting
    }

    this.showForm = !this.showForm;
    
    if (!this.showForm) {
      this.form.reset();
      this.stateService.setError(null);
    }
  }

  /**
   * Format date string to YYYY-MM-DD format
   */
  private formatDateForApi(date: string): string {
    if (!date) return '';
    const d = new Date(date);
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  /**
   * Extract error message from various error formats
   */
  private extractErrorMessage(error: any): string {
    if (typeof error === 'string') {
      return error;
    }
    return error?.error?.error || error?.error?.message || '';
  }

  /**
   * Get field error message for form validation
   */
  getErrorMessage(fieldName: string): string {
    const field = this.form.get(fieldName);
    if (!field || !field.errors || !field.touched) {
      return '';
    }

    if (field.errors['required']) {
      return `${this.getFieldLabel(fieldName)} is required`;
    }

    if (fieldName === 'days') {
      if (field.errors['negative']) {
        return 'Cannot enter negative value';
      }
      if (field.errors['min']) {
        return 'Must be at least 1 day';
      }
    }

    if (field.errors['min']) {
      return `${this.getFieldLabel(fieldName)} must be at least ${field.errors['min'].min}`;
    }

    return '';
  }

  /**
   * Get form-level error message
   */
  getFormErrorMessage(): string {
    if (this.form.errors?.['dateRange']) {
      return 'Start date cannot be later than end date';
    }
    if (this.form.errors?.['daysMismatch']) {
      const expected = this.form.errors['daysMismatch'].expected;
      const provided = this.form.errors['daysMismatch'].provided;
      return `Days mismatch: date range is ${expected} day(s), but you entered ${provided}`;
    }
    return '';
  }

  /**
   * Get human-readable field label
   */
  private getFieldLabel(fieldName: string): string {
    const labels: { [key: string]: string } = {
      employeeId: 'Employee',
      type: 'Leave Type',
      startDate: 'Start Date',
      endDate: 'End Date',
      days: 'Number of Days'
    };
    return labels[fieldName] || fieldName;
  }

  /**
   * Get human-readable label for LeaveType enum
   */
  leaveTypeLabel(type: LeaveType): string {
    switch (type) {
      case LeaveType.Vacation:
        return 'Vacation';
      case LeaveType.Sick:
        return 'Sick';
      case LeaveType.Unpaid:
        return 'Unpaid';
      default:
        return 'Unknown';
    }
  }

  /**
   * Get human-readable label for LeaveStatus enum
   */
  leaveStatusLabel(status: LeaveStatus): string {
    switch (status) {
      case LeaveStatus.Pending:
        return 'Pending';
      case LeaveStatus.Approved:
        return 'Approved';
      case LeaveStatus.Rejected:
        return 'Rejected';
      default:
        return 'Unknown';
    }
  }
}
