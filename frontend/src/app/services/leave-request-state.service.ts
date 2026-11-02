import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { LeaveRequest, LeaveStatus } from '../models/leave-request.model';

/**
 * Component state interface for centralized state management
 */
export interface ComponentState {
  requests: LeaveRequest[];
  loading: boolean;
  error: string | null;
  submitting: boolean;
  approvingIds: Set<number>;
  approvalErrors: { [requestId: number]: string };
  approvalSuccess: { [requestId: number]: boolean };
}

/**
 * State Service for managing leave requests component state
 * Centralizes state management using BehaviorSubject
 */
@Injectable({
  providedIn: 'root'
})
export class LeaveRequestStateService {
  private readonly initialState: ComponentState = {
    requests: [],
    loading: false,
    error: null,
    submitting: false,
    approvingIds: new Set(),
    approvalErrors: {},
    approvalSuccess: {}
  };

  private state$ = new BehaviorSubject<ComponentState>(this.initialState);

  constructor() {}

  /**
   * Get the current state as an Observable
   */
  getState(): Observable<ComponentState> {
    return this.state$.asObservable();
  }

  /**
   * Get the current state value synchronously
   */
  getCurrentState(): ComponentState {
    return this.state$.value;
  }

  /**
   * Set loading state for data fetch
   */
  setLoading(loading: boolean): void {
    this.state$.next({
      ...this.state$.value,
      loading
    });
  }

  /**
   * Update the requests list
   */
  setRequests(requests: LeaveRequest[]): void {
    this.state$.next({
      ...this.state$.value,
      requests,
      loading: false,
      error: null
    });
  }

  /**
   * Set error message
   */
  setError(error: string | null): void {
    this.state$.next({
      ...this.state$.value,
      error,
      loading: false
    });
  }

  /**
   * Set submitting state for form submission
   */
  setSubmitting(submitting: boolean): void {
    this.state$.next({
      ...this.state$.value,
      submitting
    });
  }

  /**
   * Set approval loading state for a specific request
   */
  setApprovingId(id: number, isApproving: boolean): void {
    const approvingIds = new Set(this.state$.value.approvingIds);
    if (isApproving) {
      approvingIds.add(id);
    } else {
      approvingIds.delete(id);
    }
    this.state$.next({
      ...this.state$.value,
      approvingIds
    });
  }

  /**
   * Set approval error for a specific request
   */
  setApprovalError(id: number, error: string | null): void {
    const approvalErrors = { ...this.state$.value.approvalErrors };
    if (error) {
      approvalErrors[id] = error;
    } else {
      delete approvalErrors[id];
    }
    this.state$.next({
      ...this.state$.value,
      approvalErrors
    });
  }

  /**
   * Set approval success for a specific request
   */
  setApprovalSuccess(id: number, success: boolean): void {
    const approvalSuccess = { ...this.state$.value.approvalSuccess };
    if (success) {
      approvalSuccess[id] = true;
    } else {
      delete approvalSuccess[id];
    }
    this.state$.next({
      ...this.state$.value,
      approvalSuccess
    });
  }

  /**
   * Update a specific request in the list
   */
  updateRequest(id: number, updates: Partial<LeaveRequest>): void {
    const requests = this.state$.value.requests.map(r =>
      r.id === id ? { ...r, ...updates } : r
    );
    this.state$.next({
      ...this.state$.value,
      requests
    });
  }

  /**
   * Reset state to initial
   */
  reset(): void {
    this.state$.next(this.initialState);
  }
}
