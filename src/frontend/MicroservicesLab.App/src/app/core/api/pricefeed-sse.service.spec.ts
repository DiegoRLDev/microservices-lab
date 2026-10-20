import { TestBed } from '@angular/core/testing';
import { API_ENDPOINTS, ApiEndpoints } from './api-endpoints';
import { PriceFeedSseService } from './pricefeed-sse.service';

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

class MockEventSource {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;
  static readonly instances: MockEventSource[] = [];

  readonly listeners = new Map<string, (event: MessageEvent<string>) => void>();
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  readyState = MockEventSource.CONNECTING;
  closed = false;

  constructor(readonly url: string) {
    MockEventSource.instances.push(this);
  }

  addEventListener(type: string, listener: (event: MessageEvent<string>) => void) {
    this.listeners.set(type, listener);
  }

  close() {
    this.readyState = MockEventSource.CLOSED;
    this.closed = true;
  }

  emit(type: string, data: string) {
    this.listeners.get(type)?.({ data } as MessageEvent<string>);
  }
}

describe('PriceFeedSseService', () => {
  beforeEach(() => {
    MockEventSource.instances.length = 0;
    vi.stubGlobal('EventSource', MockEventSource);

    TestBed.configureTestingModule({
      providers: [{ provide: API_ENDPOINTS, useValue: endpoints }]
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('opens the configured SSE URL and emits connection status', () => {
    const events = subscribeToService();
    const source = MockEventSource.instances[0];

    expect(source.url).toBe('/api/pricefeed/prices/stream');
    expect(events[0]).toEqual({
      type: 'status',
      status: 'connecting',
      message: 'Conectando ao PriceFeed.Service...'
    });

    source.onopen?.();
    expect(events.at(-1)).toEqual({
      type: 'status',
      status: 'connected',
      message: 'Conectado ao stream SSE do PriceFeed.Service.'
    });
  });

  it('parses valid price-update events and reports invalid payloads', () => {
    const events = subscribeToService();
    const source = MockEventSource.instances[0];

    source.emit('price-update', JSON.stringify({ symbol: 'PETR4', price: 3810, occurredAt: '2026-10-12T00:00:00Z' }));
    expect(events.at(-1)).toEqual({
      type: 'price-update',
      update: { symbol: 'PETR4', price: 3810, occurredAt: '2026-10-12T00:00:00Z' }
    });

    source.emit('price-update', '{ broken');
    expect(events.at(-1)).toEqual({
      type: 'invalid-payload',
      message: 'Payload SSE invalido recebido.',
      raw: '{ broken'
    });
  });

  it('reports reconnecting/error states and closes the connection on unsubscribe', () => {
    const events = subscribeToService();
    const source = MockEventSource.instances[0];

    source.readyState = MockEventSource.CONNECTING;
    source.onerror?.();
    expect(events.at(-1)).toMatchObject({ type: 'status', status: 'reconnecting' });

    source.readyState = MockEventSource.CLOSED;
    source.onerror?.();
    expect(events.at(-1)).toMatchObject({ type: 'status', status: 'error' });

    events.unsubscribe();
    expect(source.closed).toBe(true);
  });
});

function subscribeToService() {
  const events: ReturnType<PriceFeedSseService['connect']> extends import('rxjs').Observable<infer T> ? T[] : never[] = [];
  const subscription = TestBed.inject(PriceFeedSseService).connect().subscribe((event) => events.push(event));

  return Object.assign(events, {
    unsubscribe: () => subscription.unsubscribe()
  });
}
