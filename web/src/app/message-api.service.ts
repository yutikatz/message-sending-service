import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { EmployeeDto, MessageListDto, MessageStatus, SendResultDto } from './models';

/**
 * Single place where the Angular app talks to the .NET API.
 * In docker-compose nginx proxies /api to the api container, so the base stays '/api'.
 */
@Injectable({ providedIn: 'root' })
export class MessageApiService {
  private readonly base = '/api';

  constructor(private readonly http: HttpClient) {}

  employees(): Observable<EmployeeDto[]> {
    return this.http.get<EmployeeDto[]>(`${this.base}/employees`);
  }

  send(employeeInstanceId: number, body: string): Observable<SendResultDto> {
    return this.http.post<SendResultDto>(`${this.base}/messages`, { employeeInstanceId, body });
  }

  list(status?: MessageStatus, page = 1, pageSize = 20): Observable<MessageListDto> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    if (status) params = params.set('status', status);

    return this.http.get<MessageListDto>(`${this.base}/messages`, { params });
  }
}
