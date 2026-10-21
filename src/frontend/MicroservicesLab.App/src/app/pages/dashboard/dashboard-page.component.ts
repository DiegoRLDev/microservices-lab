import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { catchError, forkJoin, map, of } from 'rxjs';
import { InventoryApiService } from '../../core/api/inventory-api.service';
import { LabHealthService, ServiceHealthTarget } from '../../core/api/lab-health.service';
import { OrderApiService } from '../../core/api/order-api.service';
import { PricingApiService } from '../../core/api/pricing-api.service';

type LoadStatus = 'idle' | 'loading' | 'success' | 'error';
type HealthStatus = 'loading' | 'online' | 'offline';

interface SummaryState {
  status: LoadStatus;
  value: number | null;
  error: string | null;
}

interface HealthState {
  name: string;
  url: string;
  status: HealthStatus;
}

interface SummaryCard {
  label: string;
  value: number | null;
  status: LoadStatus;
  error: string | null;
  emptyText: string;
}

const initialSummaryState: SummaryState = {
  status: 'idle',
  value: null,
  error: null
};

@Component({
  selector: 'mlab-dashboard-page',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.css'
})
export class DashboardPageComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly inventoryApi = inject(InventoryApiService);
  private readonly orderApi = inject(OrderApiService);
  private readonly pricingApi = inject(PricingApiService);
  private readonly healthApi = inject(LabHealthService);

  protected readonly products = signal<SummaryState>(initialSummaryState);
  protected readonly orders = signal<SummaryState>(initialSummaryState);
  protected readonly prices = signal<SummaryState>(initialSummaryState);
  protected readonly health = signal<HealthState[]>(
    this.healthApi.targets.map((target) => this.toLoadingHealth(target))
  );
  protected readonly isRefreshing = signal(false);
  protected readonly lastUpdatedAt = signal<Date | null>(null);

  protected readonly summaryCards = computed<SummaryCard[]>(() => [
    {
      label: 'Produtos cadastrados',
      emptyText: 'Nenhum produto cadastrado.',
      ...this.products()
    },
    {
      label: 'Pedidos registrados',
      emptyText: 'Nenhum pedido registrado.',
      ...this.orders()
    },
    {
      label: 'Simbolos com preco',
      emptyText: 'Nenhum preco recebido ainda.',
      ...this.prices()
    }
  ]);

  protected readonly onlineServices = computed(() =>
    this.health().filter((service) => service.status === 'online').length
  );

  constructor() {
    this.refresh();
  }

  protected refresh() {
    this.isRefreshing.set(true);
    this.products.set({ status: 'loading', value: null, error: null });
    this.orders.set({ status: 'loading', value: null, error: null });
    this.prices.set({ status: 'loading', value: null, error: null });
    this.health.set(this.healthApi.targets.map((target) => this.toLoadingHealth(target)));

    forkJoin({
      products: this.inventoryApi.listProducts().pipe(
        map((products) => this.toSummarySuccess(products.length)),
        catchError(() => of(this.toSummaryError('Nao foi possivel consultar produtos.')))
      ),
      orders: this.orderApi.listOrders().pipe(
        map((orders) => this.toSummarySuccess(orders.length)),
        catchError(() => of(this.toSummaryError('Nao foi possivel consultar pedidos.')))
      ),
      prices: this.pricingApi.listPrices().pipe(
        map((prices) => this.toSummarySuccess(prices.length)),
        catchError(() => of(this.toSummaryError('Nao foi possivel consultar precos.')))
      ),
      health: forkJoin(
        this.healthApi.targets.map((target) =>
          this.healthApi.check(target).pipe(
            map(() => this.toHealth(target, 'online')),
            catchError(() => of(this.toHealth(target, 'offline')))
          )
        )
      )
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        this.products.set(result.products);
        this.orders.set(result.orders);
        this.prices.set(result.prices);
        this.health.set(result.health);
        this.lastUpdatedAt.set(new Date());
        this.isRefreshing.set(false);
      });
  }

  private toSummarySuccess(value: number): SummaryState {
    return {
      status: 'success',
      value,
      error: null
    };
  }

  private toSummaryError(error: string): SummaryState {
    return {
      status: 'error',
      value: null,
      error
    };
  }

  private toLoadingHealth(target: ServiceHealthTarget): HealthState {
    return this.toHealth(target, 'loading');
  }

  private toHealth(target: ServiceHealthTarget, status: HealthStatus): HealthState {
    return {
      name: target.name,
      url: target.url,
      status
    };
  }
}
