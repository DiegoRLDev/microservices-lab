import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { API_ENDPOINTS, ApiEndpoints } from '../../core/api/api-endpoints';
import { LabHealthService, LabServiceHealthTarget } from '../../core/api/lab-health.service';
import { ObservabilityPageComponent } from './observability-page.component';

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

const targets: LabServiceHealthTarget[] = [
  {
    name: 'Order.Service',
    technology: '.NET 9',
    responsibility: 'Pedidos',
    healthUrl: '/api/order/health'
  },
  {
    name: 'Payment.Service',
    technology: 'Go',
    responsibility: 'Pagamentos',
    healthUrl: null,
    monitoringNote: 'Sem endpoint HTTP de health exposto nesta fase.'
  },
  {
    name: 'Pricing.Service',
    technology: '.NET 9',
    responsibility: 'Precos',
    healthUrl: '/api/pricing/health'
  }
];

describe('ObservabilityPageComponent', () => {
  let component: any;
  let healthApi: {
    observabilityTargets: LabServiceHealthTarget[];
    checkUrl: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(console, 'debug').mockImplementation(() => undefined);

    healthApi = {
      observabilityTargets: targets,
      checkUrl: vi.fn((url: string) => (url.includes('pricing') ? throwError(() => new Error('offline')) : of('Healthy')))
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: API_ENDPOINTS, useValue: endpoints },
        { provide: LabHealthService, useValue: healthApi }
      ]
    });

    component = TestBed.runInInjectionContext(() => new ObservabilityPageComponent()) as any;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('classifies healthy, unavailable and unmonitored services', () => {
    const services = component.services();

    expect(services.find((service: any) => service.target.name === 'Order.Service')?.status).toBe('healthy');
    expect(services.find((service: any) => service.target.name === 'Pricing.Service')?.status).toBe('unavailable');
    expect(services.find((service: any) => service.target.name === 'Payment.Service')?.status).toBe('unmonitored');
    expect(component.monitoredCount()).toBe(2);
    expect(component.healthyCount()).toBe(1);
    expect(component.unmonitoredCount()).toBe(1);
  });

  it('refreshes health checks manually', () => {
    healthApi.checkUrl.mockClear();

    component.refresh();
    component.refresh();

    expect(healthApi.checkUrl).toHaveBeenCalledTimes(4);
    expect(component.lastRefreshAt()).toBeInstanceOf(Date);
  });

  it('uses automatic polling and clears it when destroyed', () => {
    const initialCalls = healthApi.checkUrl.mock.calls.length;

    vi.advanceTimersByTime(15_000);
    expect(healthApi.checkUrl.mock.calls.length).toBe(initialCalls + 2);

    TestBed.resetTestingModule();
    vi.advanceTimersByTime(15_000);
    expect(healthApi.checkUrl.mock.calls.length).toBe(initialCalls + 2);
  });

  it('exposes observability tool links from central configuration', () => {
    expect(component.tools.map((tool: any) => tool.url)).toEqual([
      'http://localhost:3000',
      'http://localhost:9090',
      'http://localhost:16686'
    ]);
  });
});
