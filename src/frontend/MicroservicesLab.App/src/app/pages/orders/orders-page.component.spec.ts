import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { InventoryApiService, ProductResponse } from '../../core/api/inventory-api.service';
import { OrderApiService, OrderResponse } from '../../core/api/order-api.service';
import { OrdersPageComponent } from './orders-page.component';

const product: ProductResponse = {
  id: 'product-1',
  name: 'Notebook',
  price: 5500,
  availableQuantity: 3
};

const pendingOrder: OrderResponse = {
  id: 'order-1',
  productId: 'product-1',
  quantity: 1,
  unitPrice: 5500,
  totalPrice: 5500,
  status: 'Pending',
  createdAt: '2026-10-12T00:00:00Z'
};

const paidOrder: OrderResponse = {
  ...pendingOrder,
  status: 'Paid'
};

const rejectedOrder: OrderResponse = {
  ...pendingOrder,
  status: 'PaymentRejected'
};

describe('OrdersPageComponent', () => {
  let component: any;
  let inventoryApi: { listProducts: ReturnType<typeof vi.fn> };
  let orderApi: {
    listOrders: ReturnType<typeof vi.fn>;
    createOrder: ReturnType<typeof vi.fn>;
    getOrder: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    vi.useFakeTimers();

    inventoryApi = {
      listProducts: vi.fn(() => of([product]))
    };
    orderApi = {
      listOrders: vi.fn(() => of([])),
      createOrder: vi.fn(() => of(pendingOrder)),
      getOrder: vi.fn(() => of(paidOrder))
    };

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: InventoryApiService, useValue: inventoryApi },
        { provide: OrderApiService, useValue: orderApi }
      ]
    });

    component = TestBed.runInInjectionContext(() => new OrdersPageComponent()) as any;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('loads products and orders when created', () => {
    expect(inventoryApi.listProducts).toHaveBeenCalledOnce();
    expect(orderApi.listOrders).toHaveBeenCalledOnce();
    expect(component.products()).toEqual([product]);
    expect(component.ordersStatus()).toBe('success');
  });

  it('validates product and quantity before creating an order', () => {
    component.orderForm.setValue({ productId: '', quantity: 0 });

    component.createOrder();

    expect(orderApi.createOrder).not.toHaveBeenCalled();
    expect(component.createError()).toBe('Selecione um produto e informe uma quantidade maior que zero.');
  });

  it('creates an order and stops polling when payment becomes paid', () => {
    component.orderForm.setValue({ productId: 'product-1', quantity: 1 });
    orderApi.createOrder.mockReturnValue(of(pendingOrder));
    orderApi.getOrder.mockReturnValue(of(paidOrder));

    component.createOrder();
    expect(component.isPolling()).toBe(true);

    vi.advanceTimersByTime(2500);

    expect(orderApi.getOrder).toHaveBeenCalledWith('order-1');
    expect(component.selectedOrder()).toEqual(paidOrder);
    expect(component.isPolling()).toBe(false);
    expect(component.pollingMessage()).toBe('Processamento finalizado: Pago.');
  });

  it('tracks Pending to PaymentRejected when polling selected order details', () => {
    orderApi.getOrder.mockReturnValueOnce(of(pendingOrder)).mockReturnValueOnce(of(rejectedOrder));

    component.selectOrder('order-1');
    expect(component.isPolling()).toBe(true);

    vi.advanceTimersByTime(2500);

    expect(component.selectedOrder()).toEqual(rejectedOrder);
    expect(component.isPolling()).toBe(false);
    expect(component.pollingMessage()).toBe('Processamento finalizado: Pagamento rejeitado.');
  });

  it('stops polling after consecutive refresh failures', () => {
    orderApi.createOrder.mockReturnValue(of(pendingOrder));
    orderApi.getOrder.mockReturnValue(throwError(() => ({ error: { detail: 'temporarily unavailable' } })));
    component.orderForm.setValue({ productId: 'product-1', quantity: 1 });

    component.createOrder();
    vi.advanceTimersByTime(2500 * 5);

    expect(orderApi.getOrder).toHaveBeenCalledTimes(5);
    expect(component.isPolling()).toBe(false);
    expect(component.pollingMessage()).toBe('Polling interrompido apos falhas consecutivas. Use atualizar para tentar novamente.');
  });

  it('cleans the polling subscription when destroyed', () => {
    orderApi.createOrder.mockReturnValue(of(pendingOrder));
    component.orderForm.setValue({ productId: 'product-1', quantity: 1 });

    component.createOrder();
    TestBed.resetTestingModule();
    vi.advanceTimersByTime(2500);

    expect(orderApi.getOrder).not.toHaveBeenCalled();
  });
});
