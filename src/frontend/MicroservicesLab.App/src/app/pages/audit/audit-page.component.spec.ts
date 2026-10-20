import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { AuditApiService, AuditEventResponse } from '../../core/api/audit-api.service';
import { AuditPageComponent } from './audit-page.component';

const auditEvent: AuditEventResponse = {
  id: 'audit-1',
  eventId: 'event-1',
  eventType: 'OrderCreated',
  topic: 'order-created',
  messageKey: 'order-1',
  partition: 0,
  offset: 10,
  occurredAt: '2026-10-12T00:00:00Z',
  correlationId: 'trace-1',
  payload: '{"orderId":"order-1"}',
  auditedAt: '2026-10-12T00:00:01Z'
};

describe('AuditPageComponent', () => {
  let component: any;
  let auditApi: { listEvents: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    auditApi = {
      listEvents: vi.fn(() => of([auditEvent]))
    };

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: AuditApiService, useValue: auditApi }
      ]
    });

    component = TestBed.runInInjectionContext(() => new AuditPageComponent()) as any;
  });

  it('loads audit events with default filters and selects formatted JSON payload', () => {
    expect(auditApi.listEvents).toHaveBeenCalledWith({ eventType: undefined, messageKey: undefined, limit: 50 });
    expect(component.events()).toEqual([auditEvent]);
    expect(component.status()).toBe('success');

    component.selectEvent(auditEvent);

    expect(component.selectedPayload()).toBe(JSON.stringify({ orderId: 'order-1' }, null, 2));
  });

  it('sends trimmed filters and supports empty results', () => {
    auditApi.listEvents.mockReturnValueOnce(of([]));
    component.filtersForm.setValue({
      eventType: ' PaymentApproved ',
      messageKey: ' order-2 ',
      limit: 25
    });

    component.loadEvents();

    expect(auditApi.listEvents).toHaveBeenLastCalledWith({
      eventType: 'PaymentApproved',
      messageKey: 'order-2',
      limit: 25
    });
    expect(component.events()).toEqual([]);
    expect(component.hasEvents()).toBe(false);
  });

  it('keeps invalid or empty payload content unchanged', () => {
    component.selectEvent({ ...auditEvent, payload: '{ broken' });
    expect(component.selectedPayload()).toBe('{ broken');

    component.selectEvent({ ...auditEvent, payload: '' });
    expect(component.selectedPayload()).toBe('');
  });

  it('does not call the API when the limit is invalid', () => {
    auditApi.listEvents.mockClear();
    component.filtersForm.setValue({ eventType: '', messageKey: '', limit: 999 });

    component.loadEvents();

    expect(auditApi.listEvents).not.toHaveBeenCalled();
    expect(component.status()).toBe('error');
    expect(component.error()).toBe('Informe um limite entre 1 e 200.');
  });

  it('shows API error details when available', () => {
    auditApi.listEvents.mockReturnValueOnce(throwError(() => ({ error: { detail: 'invalid filter' } })));

    component.loadEvents();

    expect(component.status()).toBe('error');
    expect(component.error()).toBe('invalid filter');
  });
});
