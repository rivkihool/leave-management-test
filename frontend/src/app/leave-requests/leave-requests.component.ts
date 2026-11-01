import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { LeaveRequest } from '../models/leave-request.model';
import { dateRangeValidator } from './validators/date-range.validator';

// NOTE: This component was written quickly for a POC.
// It talks to the API directly, manages state by hand and uses `any` everywhere.
@Component({
  selector: 'app-leave-requests',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './leave-requests.component.html',
  styleUrls: ['./leave-requests.component.css']
})
export class LeaveRequestsComponent implements OnInit, OnDestroy {
  requests: LeaveRequest[] = [];
  loading = false;
  showForm = false;
  submitting = false;
  successMessage = '';
  errorMessage = '';

  // Approval state: tracks loading/error per request ID
  approvingIds = new Set<number>();
  approvalErrors: { [requestId: number]: string } = {};
  approvalSuccess: { [requestId: number]: boolean } = {};

  form!: FormGroup;

  private apiUrl = 'http://localhost:5080/api/leave-requests';
  private destroy$ = new Subject<void>();

  constructor(
    private http: HttpClient,
    private fb: FormBuilder
  ) {}

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  ngOnInit(): void {
    this.initForm();
    this.load();
  }

  initForm(): void {
    this.form = this.fb.group(
      {
        employeeId: ['', [Validators.required]],
        type: ['', [Validators.required]],
        startDate: ['', [Validators.required]],
        endDate: ['', [Validators.required]]
      },
      {
        validators: [dateRangeValidator()]
      }
    );
  }

  load(): void {
    this.loading = true;
    this.http.get<LeaveRequest[]>(this.apiUrl)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.requests = data;
          this.loading = false;
        },
        error: (error) => {
          this.loading = false;
          this.errorMessage = 'Failed to load leave requests. Please refresh the page.';
          console.error('Load error:', error);
        }
      });
  }


  onSubmit(): void {
    if (!this.form.valid) {
      return;
    }

    this.submitting = true;
    this.errorMessage = '';
    this.successMessage = '';

    const formValue = this.form.value;
    const payload = {
      employeeId: parseInt(formValue.employeeId),
      type: parseInt(formValue.type),
      startDate: this.formatDateForApi(formValue.startDate),
      endDate: this.formatDateForApi(formValue.endDate)
    };

    this.http.post<LeaveRequest>(this.apiUrl, payload)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
      next: () => {
        this.successMessage = 'Leave request submitted successfully!';
        this.form.reset();
        this.showForm = false;
        this.load();
        this.submitting = false;
        setTimeout(() => {
          this.successMessage = '';
        }, 5000);
      },
      error: (error) => {
        this.submitting = false;
        this.errorMessage = error.error?.message || error.error || 'Failed to submit leave request. Please try again.';
      }
    });
  }

  private formatDateForApi(date: string): string {
    // Ensure the date is formatted as YYYY-MM-DD
    if (!date) return '';
    const d = new Date(date);
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  toggleForm(): void {
    this.showForm = !this.showForm;
    if (!this.showForm) {
      this.form.reset();
      this.errorMessage = '';
    }
  }

  approve(id: number): void {
    // Set loading state for this specific request
    this.approvingIds.add(id);
    
    // Clear any previous error/success messages for this request
    delete this.approvalErrors[id];
    delete this.approvalSuccess[id];

    this.http.post<{ message: string; data: LeaveRequest }>(`${this.apiUrl}/${id}/approve`, {})
      .pipe(takeUntil(this.destroy$))
      .subscribe({
      next: (response) => {
        // Update the request status locally instead of full reload
        const request = this.requests.find(r => r.id === id);
        if (request) {
          request.status = 1; // 1 = Approved
          this.approvalSuccess[id] = true;
          
          // Auto-clear success message after 3 seconds
          setTimeout(() => {
            delete this.approvalSuccess[id];
          }, 3000);
        }
      },
      error: (error) => {
        // Handle both object and string error responses
        const errorMessage = error.error?.error || error.error?.message || 'Failed to approve leave request';
        this.approvalErrors[id] = errorMessage;
        
        // Auto-clear error after 5 seconds
        setTimeout(() => {
          delete this.approvalErrors[id];
        }, 5000);
      },
      complete: () => {
        // Always remove loading state
        this.approvingIds.delete(id);
      }
    });
  }

  typeLabel(type: number): string {
    if (type == 0) return 'Vacation';
    if (type == 1) return 'Sick';
    return 'Unpaid';
  }

  statusLabel(status: number): string {
    if (status == 0) return 'Pending';
    if (status == 1) return 'Approved';
    return 'Rejected';
  }

  getErrorMessage(fieldName: string): string {
    const field = this.form.get(fieldName);
    if (!field || !field.errors || !field.touched) {
      return '';
    }

    if (field.errors['required']) {
      return `${this.getFieldLabel(fieldName)} is required`;
    }
    if (field.errors['min']) {
      return `${this.getFieldLabel(fieldName)} must be at least ${field.errors['min'].min}`;
    }

    return '';
  }

  getFormErrorMessage(): string {
    if (this.form.errors?.['dateRange']) {
      return 'Start date cannot be later than end date';
    }
    return '';
  }

  private getFieldLabel(fieldName: string): string {
    const labels: { [key: string]: string } = {
      employeeId: 'Employee',
      type: 'Leave Type',
      startDate: 'Start Date',
      endDate: 'End Date'
    };
    return labels[fieldName] || fieldName;
  }
}
