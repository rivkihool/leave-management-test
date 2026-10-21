import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { LeaveRequest } from '../models/leave-request.model';
import { dateRangeValidator } from './validators/date-range.validator';
import { noDaysValidator } from './validators/no-days.validator';

// NOTE: This component was written quickly for a POC.
// It talks to the API directly, manages state by hand and uses `any` everywhere.
@Component({
  selector: 'app-leave-requests',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './leave-requests.component.html',
  styleUrls: ['./leave-requests.component.css']
})
export class LeaveRequestsComponent implements OnInit {
  requests: any[] = [];
  loading = false;
  showForm = false;
  submitting = false;
  successMessage = '';
  errorMessage = '';

  form!: FormGroup;

  private apiUrl = 'http://localhost:5080/api/leave-requests';

  constructor(
    private http: HttpClient,
    private fb: FormBuilder
  ) {}

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
        endDate: ['', [Validators.required]],
        days: ['', [Validators.required, Validators.min(1)]]
      },
      {
        validators: [dateRangeValidator(), noDaysValidator()]
      }
    );
  }

  load(): void {
    this.loading = true;
    this.http.get<any>(this.apiUrl).subscribe((data) => {
      this.requests = data;
      this.loading = false;
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
      endDate: this.formatDateForApi(formValue.endDate),
      days: parseInt(formValue.days)
    };

    this.http.post<LeaveRequest>(this.apiUrl, payload).subscribe({
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
        this.errorMessage = error.error?.message || 'Failed to submit leave request. Please try again.';
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

  // Wired up by the candidate as part of the assignment.
  approve(id: number): void {
    // TODO (candidate): call POST /api/leave-requests/{id}/approve
    // and handle loading / error / success without a generic alert.
    this.http.post<any>(this.apiUrl + '/' + id + '/approve', {}).subscribe(() => {
      this.load();
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
    if (this.form.errors?.['noDays']) {
      return 'Number of days cannot be negative';
    }
    return '';
  }

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
}
