import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { EMPTY, Subscription, catchError, exhaustMap, timer } from 'rxjs';
import { InventoryApiService, ProductResponse } from '../../core/api/inventory-api.service';
import { OrderApiService, OrderResponse, OrderStatus } from '../../core/api/order-api.service';

type LoadStatus = 'idle' | 'loading' | 'success' | 'error';

const pollingIntervalMilliseconds = 2500;
const maxPollingFailures = 5;

@Component({
  selector: 'mlab-orders-page',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule],
  templateUrl: './orders-page.component.html',
  styleUrl: './orders-page.component.css'
})
export class OrdersPageComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly inventoryApi = inject(InventoryApiService);
  private readonly orderApi = inject(OrderApiService);
  private pollingSubscription: Subscription | null = null;

  protected readonly orders = signal<OrderResponse[]>([]);
  protected readonly ordersStatus = signal<LoadStatus>('idle');
  protected readonly ordersError = signal<string | null>(null);
  protected readonly products = signal<ProductResponse[]>([]);
  protected readonly productsStatus = signal<LoadStatus>('idle');
  protected readonly productsError = signal<string | null>(null);
  protected readonly createStatus = signal<LoadStatus>('idle');
  protected readonly createMessage = signal<string | null>(null);
  protected readonly createError = signal<string | null>(null);
  protected readonly selectedOrder = signal<OrderResponse | null>(null);
  protected readonly detailsStatus = signal<LoadStatus>('idle');
  protected readonly detailsError = signal<string | null>(null);
  protected readonly pollingMessage = signal<string | null>(null);
  protected readonly pollingError = signal<string | null>(null);
  protected readonly isPolling = signal(false);

  protected readonly orderForm = this.formBuilder.nonNullable.group({
    productId: ['', [Validators.required]],
    quantity: [1, [Validators.required, Validators.min(1)]]
  });

  protected readonly hasOrders = computed(() => this.orders().length > 0);

  constructor() {
    this.loadProducts();
    this.loadOrders();
    this.destroyRef.onDestroy(() => this.stopPolling());
  }

  protected loadProducts() {
    this.productsStatus.set('loading');
    this.productsError.set(null);

    this.inventoryApi.listProducts()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (products) => {
          this.products.set(products);
          this.productsStatus.set('success');
        },
        error: (error: HttpErrorResponse) => {
          this.productsStatus.set('error');
          this.productsError.set(this.toErrorMessage(error, 'Nao foi possivel carregar produtos.'));
        }
      });
  }

  protected loadOrders() {
    this.ordersStatus.set('loading');
    this.ordersError.set(null);

    this.orderApi.listOrders()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (orders) => {
          this.orders.set(orders);
          this.ordersStatus.set('success');

          const selected = this.selectedOrder();
          if (selected) {
            const refreshed = orders.find((order) => order.id === selected.id) ?? selected;
            this.selectedOrder.set(refreshed);
          }
        },
        error: (error: HttpErrorResponse) => {
          this.ordersStatus.set('error');
          this.ordersError.set(this.toErrorMessage(error, 'Nao foi possivel carregar pedidos.'));
        }
      });
  }

  protected createOrder() {
    this.orderForm.markAllAsTouched();
    this.createMessage.set(null);
    this.createError.set(null);

    if (this.orderForm.invalid) {
      this.createError.set('Selecione um produto e informe uma quantidade maior que zero.');
      return;
    }

    this.createStatus.set('loading');

    this.orderApi.createOrder(this.orderForm.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (created) => {
          this.createStatus.set('success');
          this.createMessage.set(`Pedido ${created.id} criado. Aguardando processamento do pagamento.`);
          this.orderForm.reset({
            productId: '',
            quantity: 1
          });
          this.loadOrders();
          this.setSelectedOrder(created);
        },
        error: (error: HttpErrorResponse) => {
          this.createStatus.set('error');
          this.createError.set(this.toErrorMessage(error, 'Nao foi possivel criar o pedido.'));
        }
      });
  }

  protected selectOrder(id: string) {
    this.detailsStatus.set('loading');
    this.detailsError.set(null);

    this.orderApi.getOrder(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (order) => {
          this.detailsStatus.set('success');
          this.setSelectedOrder(order);
        },
        error: (error: HttpErrorResponse) => {
          this.detailsStatus.set('error');
          this.detailsError.set(this.toErrorMessage(error, 'Nao foi possivel consultar o pedido.'));
          this.stopPolling();
        }
      });
  }

  protected refreshSelectedOrder() {
    const order = this.selectedOrder();
    if (!order) {
      return;
    }

    this.selectOrder(order.id);
  }

  protected productName(productId: string) {
    return this.products().find((product) => product.id === productId)?.name ?? 'Produto nao carregado';
  }

  protected statusLabel(status: OrderStatus) {
    const normalized = this.normalizeStatus(status);

    if (normalized === 'Paid') {
      return 'Pago';
    }

    if (normalized === 'PaymentRejected') {
      return 'Pagamento rejeitado';
    }

    return 'Pendente';
  }

  protected statusClass(status: OrderStatus) {
    return `status-pill ${this.normalizeStatus(status).toLowerCase()}`;
  }

  protected isPending(status: OrderStatus) {
    return this.normalizeStatus(status) === 'Pending';
  }

  protected orderFieldHasError(field: 'productId' | 'quantity') {
    const control = this.orderForm.controls[field];
    return control.invalid && (control.dirty || control.touched);
  }

  private setSelectedOrder(order: OrderResponse) {
    this.selectedOrder.set(order);
    this.detailsError.set(null);
    this.detailsStatus.set('success');
    this.upsertOrder(order);

    if (this.isPending(order.status)) {
      this.startPolling(order.id);
      return;
    }

    this.stopPolling();
    this.pollingMessage.set(`Processamento finalizado: ${this.statusLabel(order.status)}.`);
  }

  private startPolling(orderId: string) {
    this.stopPolling();
    this.isPolling.set(true);
    this.pollingMessage.set('Acompanhando pagamento...');
    this.pollingError.set(null);

    let failures = 0;

    this.pollingSubscription = timer(pollingIntervalMilliseconds, pollingIntervalMilliseconds)
      .pipe(
        exhaustMap(() => this.orderApi.getOrder(orderId).pipe(
          catchError((error: HttpErrorResponse) => {
            failures += 1;
            this.pollingError.set(this.toErrorMessage(error, 'Falha temporaria ao atualizar o pedido.'));

            if (failures >= maxPollingFailures) {
              this.pollingMessage.set('Polling interrompido apos falhas consecutivas. Use atualizar para tentar novamente.');
              this.stopPolling();
            }

            return EMPTY;
          })
        )),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((order) => {
        failures = 0;
        this.pollingError.set(null);
        this.selectedOrder.set(order);
        this.upsertOrder(order);

        if (!this.isPending(order.status)) {
          this.stopPolling();
          this.pollingMessage.set(`Processamento finalizado: ${this.statusLabel(order.status)}.`);
        }
      });
  }

  private stopPolling() {
    this.pollingSubscription?.unsubscribe();
    this.pollingSubscription = null;
    this.isPolling.set(false);
  }

  private upsertOrder(order: OrderResponse) {
    const current = this.orders();
    const index = current.findIndex((item) => item.id === order.id);

    if (index < 0) {
      this.orders.set([order, ...current]);
      return;
    }

    this.orders.set(current.map((item) => item.id === order.id ? order : item));
  }

  private normalizeStatus(status: OrderStatus) {
    if (status === 2 || status === 'Paid') {
      return 'Paid';
    }

    if (status === 3 || status === 'PaymentRejected') {
      return 'PaymentRejected';
    }

    return 'Pending';
  }

  private toErrorMessage(error: HttpErrorResponse, fallback: string) {
    const detail = typeof error.error?.detail === 'string'
      ? error.error.detail
      : null;

    return detail ?? fallback;
  }
}
