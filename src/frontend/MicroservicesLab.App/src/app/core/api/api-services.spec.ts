import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_ENDPOINTS, ApiEndpoints } from './api-endpoints';
import { AuditApiService } from './audit-api.service';
import { InventoryApiService } from './inventory-api.service';
import { LabHealthService } from './lab-health.service';
import { OrderApiService } from './order-api.service';
import { PricingApiService } from './pricing-api.service';

const endpoints: ApiEndpoints = {
  order: '/api/order',
  inventory: '/api/inventory',
  priceFeed: '/api/pricefeed',
  pricing: '/api/pricing',
  audit: '/api/audit',
  observability: {
    grafana: 'http://localhost:3000',
    prometheus: 'http://localhost:9090',
    jaeger: 'http://localhost:16686'
  }
};

describe('Angular API services', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_ENDPOINTS, useValue: endpoints }
      ]
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('uses the expected Order.Service endpoints and propagates errors', () => {
    const service = TestBed.inject(OrderApiService);

    service.listOrders().subscribe((orders) => expect(orders).toEqual([]));
    let request = http.expectOne('/api/order/orders');
    expect(request.request.method).toBe('GET');
    request.flush([]);

    service.getOrder('order-1').subscribe((order) => expect(order.id).toBe('order-1'));
    request = http.expectOne('/api/order/orders/order-1');
    expect(request.request.method).toBe('GET');
    request.flush({
      id: 'order-1',
      productId: 'product-1',
      quantity: 1,
      unitPrice: 10,
      totalPrice: 10,
      status: 'Pending',
      createdAt: '2026-10-12T00:00:00Z'
    });

    service.createOrder({ productId: 'product-1', quantity: 2 }).subscribe((order) => expect(order.quantity).toBe(2));
    request = http.expectOne('/api/order/orders');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ productId: 'product-1', quantity: 2 });
    request.flush({
      id: 'order-2',
      productId: 'product-1',
      quantity: 2,
      unitPrice: 10,
      totalPrice: 20,
      status: 'Pending',
      createdAt: '2026-10-12T00:00:00Z'
    });

    service.getOrder('missing').subscribe({
      error: (error: HttpErrorResponse) => expect(error.status).toBe(404)
    });
    http.expectOne('/api/order/orders/missing').flush({ detail: 'not found' }, { status: 404, statusText: 'Not Found' });
  });

  it('uses Inventory.Service endpoints for products and availability', () => {
    const service = TestBed.inject(InventoryApiService);

    service.listProducts().subscribe((products) => expect(products.length).toBe(1));
    let request = http.expectOne('/api/inventory/products');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 'product-1', name: 'Notebook', price: 3500, availableQuantity: 3 }]);

    service.createProduct({ name: 'Mouse', price: 100, availableQuantity: 5 }).subscribe((product) => {
      expect(product.name).toBe('Mouse');
    });
    request = http.expectOne('/api/inventory/products');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Mouse', price: 100, availableQuantity: 5 });
    request.flush({ id: 'product-2', name: 'Mouse', price: 100, availableQuantity: 5 });

    service.getProduct('product-2').subscribe((product) => expect(product.id).toBe('product-2'));
    request = http.expectOne('/api/inventory/products/product-2');
    expect(request.request.method).toBe('GET');
    request.flush({ id: 'product-2', name: 'Mouse', price: 100, availableQuantity: 5 });

    service.checkAvailability('product-2', { quantity: 4 }).subscribe((availability) => {
      expect(availability.isAvailable).toBe(true);
    });
    request = http.expectOne('/api/inventory/products/product-2/availability');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ quantity: 4 });
    request.flush({
      productId: 'product-2',
      isAvailable: true,
      requestedQuantity: 4,
      availableQuantity: 5,
      unitPrice: 100
    });
  });

  it('uses Pricing.Service and Audit.Worker query endpoints', () => {
    TestBed.inject(PricingApiService).listPrices().subscribe((prices) => expect(prices[0].symbol).toBe('PETR4'));
    let request = http.expectOne('/api/pricing/prices');
    expect(request.request.method).toBe('GET');
    request.flush([{ symbol: 'PETR4', price: 3810, occurredAt: '2026-10-12T00:00:00Z' }]);

    TestBed.inject(AuditApiService)
      .listEvents({ eventType: 'OrderCreated', messageKey: 'order-1', limit: 25, offset: 5 })
      .subscribe((events) => expect(events).toEqual([]));
    request = http.expectOne((req) => req.url === '/api/audit/audit-events');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('eventType')).toBe('OrderCreated');
    expect(request.request.params.get('messageKey')).toBe('order-1');
    expect(request.request.params.get('limit')).toBe('25');
    expect(request.request.params.get('offset')).toBe('5');
    request.flush([]);
  });

  it('checks real health URLs and marks Payment.Service as unmonitored metadata', () => {
    const service = TestBed.inject(LabHealthService);

    expect(service.observabilityTargets.find((target) => target.name === 'Payment.Service')?.healthUrl).toBeNull();
    expect(service.observabilityTargets.find((target) => target.name === 'Audit.Worker')?.healthUrl).toBe('/api/audit/health');

    service.check({ name: 'Order.Service', url: '/api/order/health' }).subscribe((body) => expect(body).toBe('Healthy'));
    let request = http.expectOne('/api/order/health');
    expect(request.request.method).toBe('GET');
    request.flush('Healthy');

    service.checkUrl('/api/pricing/health').subscribe((body) => expect(body).toBe('Healthy'));
    request = http.expectOne('/api/pricing/health');
    expect(request.request.method).toBe('GET');
    request.flush('Healthy');
  });

  it.each(['check', 'checkUrl'] as const)('cancels a stalled health request through %s and allows a later check', (method) => {
    vi.useFakeTimers();
    try {
      const service = TestBed.inject(LabHealthService);
      const error = vi.fn();
      const response = method === 'check'
        ? service.check({ name: 'Order.Service', url: '/api/order/health' })
        : service.checkUrl('/api/order/health');

      response.subscribe({ error });
      const stalled = http.expectOne('/api/order/health');
      vi.advanceTimersByTime(5000);
      expect(stalled.cancelled).toBe(true);
      expect(error).toHaveBeenCalledOnce();
      expect(error.mock.calls[0][0].name).toBe('TimeoutError');

      service.checkUrl('/api/order/health').subscribe((body) => expect(body).toBe('Healthy'));
      http.expectOne('/api/order/health').flush('Healthy');
    } finally {
      vi.useRealTimers();
    }
  });
});
