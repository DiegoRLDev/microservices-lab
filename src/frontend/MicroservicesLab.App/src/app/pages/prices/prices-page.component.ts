import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PriceFeedConnectionStatus, PriceFeedSseService, PriceFeedUpdate } from '../../core/api/pricefeed-sse.service';
import { PriceResponse, PricingApiService } from '../../core/api/pricing-api.service';

type LoadStatus = 'idle' | 'loading' | 'success' | 'error';

interface ConnectionState {
  status: PriceFeedConnectionStatus;
  message: string;
}

const maxRecentUpdates = 18;

@Component({
  selector: 'mlab-prices-page',
  standalone: true,
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './prices-page.component.html',
  styleUrl: './prices-page.component.css'
})
export class PricesPageComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly pricingApi = inject(PricingApiService);
  private readonly priceFeedSse = inject(PriceFeedSseService);
  private readonly highlightTimers = new Map<string, ReturnType<typeof setTimeout>>();

  protected readonly restPrices = signal<PriceResponse[]>([]);
  protected readonly restStatus = signal<LoadStatus>('idle');
  protected readonly restError = signal<string | null>(null);
  protected readonly restUpdatedAt = signal<Date | null>(null);
  protected readonly livePrices = signal<Record<string, PriceFeedUpdate>>({});
  protected readonly recentUpdates = signal<PriceFeedUpdate[]>([]);
  protected readonly highlightedSymbols = signal<Record<string, boolean>>({});
  protected readonly invalidPayloadError = signal<string | null>(null);
  protected readonly connection = signal<ConnectionState>({
    status: 'disconnected',
    message: 'Stream ainda nao conectado.'
  });

  protected readonly hasRestPrices = computed(() => this.restPrices().length > 0);
  protected readonly livePriceRows = computed(() =>
    Object.values(this.livePrices()).sort((left, right) => left.symbol.localeCompare(right.symbol))
  );
  protected readonly hasLivePrices = computed(() => this.livePriceRows().length > 0);

  constructor() {
    this.loadRestSnapshot();
    this.connectToStream();

    this.destroyRef.onDestroy(() => {
      for (const timer of this.highlightTimers.values()) {
        clearTimeout(timer);
      }

      this.connection.set({
        status: 'disconnected',
        message: 'Stream encerrado ao sair da tela.'
      });
    });
  }

  protected loadRestSnapshot() {
    this.restStatus.set('loading');
    this.restError.set(null);

    this.pricingApi.listPrices()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (prices) => {
          this.restPrices.set([...prices].sort((left, right) => left.symbol.localeCompare(right.symbol)));
          this.restStatus.set('success');
          this.restUpdatedAt.set(new Date());
        },
        error: (error: HttpErrorResponse) => {
          this.restStatus.set('error');
          this.restError.set(this.toErrorMessage(error, 'Nao foi possivel consultar o Pricing.Service.'));
        }
      });
  }

  protected priceAmount(priceInCents: number) {
    return priceInCents / 100;
  }

  protected connectionLabel(status: PriceFeedConnectionStatus) {
    if (status === 'connected') {
      return 'Conectado';
    }

    if (status === 'connecting') {
      return 'Conectando';
    }

    if (status === 'reconnecting') {
      return 'Reconectando';
    }

    if (status === 'error') {
      return 'Erro';
    }

    return 'Desconectado';
  }

  protected connectionClass(status: PriceFeedConnectionStatus) {
    return `connection-pill ${status}`;
  }

  protected isHighlighted(symbol: string) {
    return Boolean(this.highlightedSymbols()[symbol]);
  }

  private connectToStream() {
    this.priceFeedSse.connect()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.type === 'status') {
          this.connection.set({
            status: event.status,
            message: event.message
          });
          return;
        }

        if (event.type === 'invalid-payload') {
          this.invalidPayloadError.set(event.message);
          return;
        }

        this.invalidPayloadError.set(null);
        this.applyLiveUpdate(event.update);
      });
  }

  private applyLiveUpdate(update: PriceFeedUpdate) {
    this.livePrices.update((current) => ({
      ...current,
      [update.symbol]: update
    }));

    this.recentUpdates.update((updates) => [update, ...updates].slice(0, maxRecentUpdates));
    this.highlight(update.symbol);
  }

  private highlight(symbol: string) {
    const existing = this.highlightTimers.get(symbol);
    if (existing) {
      clearTimeout(existing);
    }

    this.highlightedSymbols.update((current) => ({
      ...current,
      [symbol]: true
    }));

    const timer = setTimeout(() => {
      this.highlightedSymbols.update((current) => ({
        ...current,
        [symbol]: false
      }));
      this.highlightTimers.delete(symbol);
    }, 1400);

    this.highlightTimers.set(symbol, timer);
  }

  private toErrorMessage(error: HttpErrorResponse, fallback: string) {
    const detail = typeof error.error?.detail === 'string'
      ? error.error.detail
      : null;

    return detail ?? fallback;
  }
}
