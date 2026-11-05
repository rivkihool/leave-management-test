import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LeaveRequest, CreateLeaveRequestDto, LeaveRequestResponse, ApiError } from '../models/leave-request.model';

@Injectable({
  providedIn: 'root'
})
export class LeaveRequestService {
  private apiUrl = 'http://localhost:5080/api/leave-requests';

  constructor(private http: HttpClient) {}

  /**
   * Fetch all leave requests with employee details
   */
  getAll(): Observable<LeaveRequest[]> {
    return this.http.get<LeaveRequest[]>(this.apiUrl);
  }

  /**
   * Create a new leave request
   */
  create(dto: CreateLeaveRequestDto): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(this.apiUrl, dto);
  }

  /**
   * Approve a pending leave request
   */
  approve(id: number): Observable<LeaveRequestResponse> {
    return this.http.post<LeaveRequestResponse>(`${this.apiUrl}/${id}/approve`, {});
  }

  /**
   * Search leave requests by employee name
   */
  search(name: string): Observable<LeaveRequest[]> {
    return this.http.get<LeaveRequest[]>(`${this.apiUrl}/search?name=${encodeURIComponent(name)}`);
  }
}
