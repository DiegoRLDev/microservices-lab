import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuditApiService, AuditEventResponse } from '../../core/api/audit-api.service';

type LoadStatus = 'idle' | 'loading' | 'success' | 'error';

@Component({
  selector: 'mlab-audit-page',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './audit-page.component.html',
  styleUrl: './audit-page.component.css'
})
export class AuditPageComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly auditApi = inject(AuditApiService);

  protected readonly events = signal<AuditEventResponse[]>([]);
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<string | null>(null);
  protected readonly selectedEvent = signal<AuditEventResponse | null>(null);
  protected readonly lastUpdatedAt = signal<Date | null>(null);

  protected readonly filtersForm = this.formBuilder.nonNullable.group({
    eventType: [''],
    messageKey: [''],
    limit: [50, [Validators.required, Validators.min(1), Validators.max(200)]]
  });

  protected readonly hasEvents = computed(() => this.events().length > 0);
  protected readonly selectedPayload = computed(() => {
    const auditEvent = this.selectedEvent();
    return auditEvent ? this.formatPayload(auditEvent.payload) : '';
  });

  constructor() {
    this.loadEvents();
  }

  protected loadEvents() {
    this.filtersForm.markAllAsTouched();

    if (this.filtersForm.invalid) {
      this.error.set('Informe um limite entre 1 e 200.');
      this.status.set('error');
      return;
    }

    const filters = this.filtersForm.getRawValue();
    this.status.set('loading');
    this.error.set(null);

    this.auditApi.listEvents({
      eventType: filters.eventType.trim() || undefined,
      messageKey: filters.messageKey.trim() || undefined,
      limit: filters.limit
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (events) => {
          this.events.set(events);
          this.status.set('success');
          this.lastUpdatedAt.set(new Date());

          const selected = this.selectedEvent();
          if (selected) {
            this.selectedEvent.set(events.find((auditEvent) => auditEvent.id === selected.id) ?? null);
          }
        },
        error: (error: HttpErrorResponse) => {
          this.status.set('error');
          this.error.set(this.toErrorMessage(error, 'Nao foi possivel consultar eventos de auditoria.'));
        }
      });
  }

  protected clearFilters() {
    this.filtersForm.reset({
      eventType: '',
      messageKey: '',
      limit: 50
    });
    this.loadEvents();
  }

  protected selectEvent(auditEvent: AuditEventResponse) {
    this.selectedEvent.set(auditEvent);
  }

  protected fieldHasError() {
    const control = this.filtersForm.controls.limit;
    return control.invalid && (control.dirty || control.touched);
  }

  protected aggregateLabel(auditEvent: AuditEventResponse) {
    return auditEvent.messageKey || 'Sem chave';
  }

  private formatPayload(payload: string) {
    try {
      return JSON.stringify(JSON.parse(payload), null, 2);
    } catch {
      return payload;
    }
  }

  private toErrorMessage(error: HttpErrorResponse, fallback: string) {
    const detail = typeof error.error?.detail === 'string'
      ? error.error.detail
      : null;

    return detail ?? fallback;
  }
}
