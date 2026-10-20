import { Injectable, NgZone, inject } from '@angular/core';
import { Observable, retry } from 'rxjs';
import { API_ENDPOINTS } from './api-endpoints';

export type PriceFeedConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error';

export interface PriceFeedUpdate {
  symbol: string;
  price: number;
  occurredAt: string;
}

export type PriceFeedSseEvent =
  | {
      type: 'status';
      status: PriceFeedConnectionStatus;
      message: string;
    }
  | {
      type: 'price-update';
      update: PriceFeedUpdate;
    }
  | {
      type: 'invalid-payload';
      message: string;
      raw: string;
    };

@Injectable({ providedIn: 'root' })
export class PriceFeedSseService {
  private readonly endpoints = inject(API_ENDPOINTS);
  private readonly zone = inject(NgZone);

  connect() {
    return new Observable<PriceFeedSseEvent>((subscriber) => {
      const eventSource = new EventSource(`${this.endpoints.priceFeed}/prices/stream`);

      this.emit(subscriber, {
        type: 'status',
        status: 'connecting',
        message: 'Conectando ao PriceFeed.Service...'
      });

      eventSource.onopen = () => {
        this.emit(subscriber, {
          type: 'status',
          status: 'connected',
          message: 'Conectado ao stream SSE do PriceFeed.Service.'
        });
      };

      eventSource.addEventListener('price-update', (event) => {
        const parsed = this.parseUpdate(event.data);

        if (parsed) {
          this.emit(subscriber, {
            type: 'price-update',
            update: parsed
          });
          return;
        }

        this.emit(subscriber, {
          type: 'invalid-payload',
          message: 'Payload SSE invalido recebido.',
          raw: event.data
        });
      });

      eventSource.onerror = () => {
        const reconnecting = eventSource.readyState === EventSource.CONNECTING;
        this.emit(subscriber, {
          type: 'status',
          status: reconnecting ? 'reconnecting' : 'error',
          message: reconnecting
            ? 'Conexao SSE interrompida. O navegador tentara reconectar.'
            : 'Conexao SSE indisponivel.'
        });

        // A non-200 handshake can close EventSource permanently, unlike a transient disconnect.
        if (!reconnecting) {
          subscriber.error(new Error('SSE connection closed.'));
        }
      };

      return () => {
        eventSource.close();
      };
    }).pipe(retry({ delay: 3000 }));
  }

  private parseUpdate(payload: string): PriceFeedUpdate | null {
    try {
      const value = JSON.parse(payload) as Partial<PriceFeedUpdate>;

      if (
        typeof value.symbol !== 'string' ||
        typeof value.price !== 'number' ||
        typeof value.occurredAt !== 'string'
      ) {
        return null;
      }

      return {
        symbol: value.symbol,
        price: value.price,
        occurredAt: value.occurredAt
      };
    } catch {
      return null;
    }
  }

  private emit(subscriber: { next: (event: PriceFeedSseEvent) => void }, event: PriceFeedSseEvent) {
    this.zone.run(() => subscriber.next(event));
  }
}
