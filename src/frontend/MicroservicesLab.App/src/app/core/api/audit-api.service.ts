import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_ENDPOINTS } from './api-endpoints';

export interface AuditEventResponse {
  id: string;
  eventId: string;
  eventType: string;
  topic: string;
  messageKey: string;
  partition: number;
  offset: number;
  occurredAt: string | null;
  correlationId: string;
  payload: string;
  auditedAt: string;
}

export interface AuditEventQuery {
  eventType?: string;
  messageKey?: string;
  limit?: number;
  offset?: number;
}

@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoints = inject(API_ENDPOINTS);

  listEvents(query: AuditEventQuery = {}) {
    let params = new HttpParams();

    if (query.eventType) {
      params = params.set('eventType', query.eventType);
    }

    if (query.messageKey) {
      params = params.set('messageKey', query.messageKey);
    }

    if (query.limit) {
      params = params.set('limit', query.limit);
    }

    if (query.offset) {
      params = params.set('offset', query.offset);
    }

    return this.http.get<AuditEventResponse[]>(`${this.endpoints.audit}/audit-events`, { params });
  }
}
