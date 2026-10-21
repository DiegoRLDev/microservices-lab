import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, map, of } from 'rxjs';
import { API_ENDPOINTS } from '../../core/api/api-endpoints';
import { LabHealthService, LabServiceHealthTarget } from '../../core/api/lab-health.service';

type HealthStatus = 'checking' | 'healthy' | 'unavailable' | 'unmonitored';

interface ServiceHealthState {
  target: LabServiceHealthTarget;
  status: HealthStatus;
  lastCheckedAt: Date | null;
  detail: string;
}

interface ObservabilityTool {
  name: string;
  description: string;
  url: string;
}

interface ArchitectureFlow {
  title: string;
  description: string;
  steps: string[];
}

const refreshIntervalMs = 15_000;

@Component({
  selector: 'mlab-observability-page',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './observability-page.component.html',
  styleUrl: './observability-page.component.css'
})
export class ObservabilityPageComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly healthApi = inject(LabHealthService);
  private readonly endpoints = inject(API_ENDPOINTS);
  private readonly pollerId: ReturnType<typeof window.setInterval>;

  protected readonly services = signal<ServiceHealthState[]>(
    this.healthApi.observabilityTargets.map((target) => this.toInitialState(target))
  );
  protected readonly isRefreshing = signal(false);
  protected readonly lastRefreshAt = signal<Date | null>(null);

  protected readonly tools: ObservabilityTool[] = [
    {
      name: 'Grafana',
      description: 'Dashboards e paineis configurados para consultar fontes de metricas.',
      url: this.endpoints.observability.grafana
    },
    {
      name: 'Prometheus',
      description: 'Coleta e consulta das metricas exportadas pelo OpenTelemetry Collector.',
      url: this.endpoints.observability.prometheus
    },
    {
      name: 'Jaeger',
      description: 'Rastreamento distribuido recebido via OpenTelemetry.',
      url: this.endpoints.observability.jaeger
    }
  ];

  protected readonly flows: ArchitectureFlow[] = [
    {
      title: 'Pedidos e pagamentos',
      description: 'Fluxo principal de criacao de pedido, pagamento assincrono e auditoria.',
      steps: [
        'Angular -> Order.Service',
        'Order.Service -> gRPC -> Inventory.Service',
        'Order.Service -> Kafka -> Payment.Service',
        'Payment.Service -> Kafka -> Order.Service',
        'Eventos Kafka -> Audit.Worker -> PostgreSQL'
      ]
    },
    {
      title: 'Precos realtime',
      description: 'Fluxo de precos demonstrando Kafka, estado derivado e SSE.',
      steps: [
        'PriceFeed.Service -> Kafka -> Pricing.Service',
        'Pricing.Service -> REST -> Angular',
        'PriceFeed.Service -> SSE -> Angular'
      ]
    },
    {
      title: 'Observabilidade',
      description: 'Instrumentacao existente encaminhada para as ferramentas do laboratorio.',
      steps: [
        'Servicos instrumentados -> OpenTelemetry Collector',
        'OpenTelemetry Collector -> Prometheus exporter',
        'OpenTelemetry Collector -> Jaeger',
        'Grafana consulta as fontes configuradas'
      ]
    }
  ];

  protected readonly monitoredCount = computed(
    () => this.services().filter((service) => service.status !== 'unmonitored').length
  );
  protected readonly healthyCount = computed(
    () => this.services().filter((service) => service.status === 'healthy').length
  );
  protected readonly unmonitoredCount = computed(
    () => this.services().filter((service) => service.status === 'unmonitored').length
  );

  constructor() {
    this.refresh();
    this.pollerId = window.setInterval(() => this.refresh(false), refreshIntervalMs);
    this.destroyRef.onDestroy(() => window.clearInterval(this.pollerId));
  }

  protected refresh(manual = true) {
    if (this.isRefreshing()) {
      return;
    }

    this.isRefreshing.set(true);
    const checkedAt = new Date();
    const monitoredTargets = this.healthApi.observabilityTargets.filter((target) => target.healthUrl);

    this.services.set(
      this.healthApi.observabilityTargets.map((target) =>
        target.healthUrl ? this.toCheckingState(target, checkedAt) : this.toUnmonitoredState(target)
      )
    );

    forkJoin(
      monitoredTargets.map((target) =>
        this.healthApi.checkUrl(target.healthUrl as string).pipe(
          map(() => this.toHealthyState(target, checkedAt)),
          catchError(() => of(this.toUnavailableState(target, checkedAt)))
        )
      )
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((monitoredStates) => {
        const statesByName = new Map(monitoredStates.map((state) => [state.target.name, state]));

        this.services.set(
          this.healthApi.observabilityTargets.map((target) =>
            statesByName.get(target.name) ?? this.toUnmonitoredState(target)
          )
        );
        this.lastRefreshAt.set(checkedAt);
        this.isRefreshing.set(false);

        if (manual) {
          console.debug('Observability health refreshed');
        }
      });
  }

  protected statusLabel(status: HealthStatus) {
    switch (status) {
      case 'healthy':
        return 'Saudavel';
      case 'unavailable':
        return 'Indisponivel';
      case 'checking':
        return 'Verificando';
      case 'unmonitored':
        return 'Nao monitorado';
    }
  }

  private toInitialState(target: LabServiceHealthTarget): ServiceHealthState {
    return target.healthUrl ? this.toCheckingState(target, null) : this.toUnmonitoredState(target);
  }

  private toCheckingState(target: LabServiceHealthTarget, checkedAt: Date | null): ServiceHealthState {
    return {
      target,
      status: 'checking',
      lastCheckedAt: checkedAt,
      detail: 'Consulta de health em andamento.'
    };
  }

  private toHealthyState(target: LabServiceHealthTarget, checkedAt: Date): ServiceHealthState {
    return {
      target,
      status: 'healthy',
      lastCheckedAt: checkedAt,
      detail: 'Endpoint de health respondeu com sucesso.'
    };
  }

  private toUnavailableState(target: LabServiceHealthTarget, checkedAt: Date): ServiceHealthState {
    return {
      target,
      status: 'unavailable',
      lastCheckedAt: checkedAt,
      detail: 'Falha ao consultar o endpoint de health exposto.'
    };
  }

  private toUnmonitoredState(target: LabServiceHealthTarget): ServiceHealthState {
    return {
      target,
      status: 'unmonitored',
      lastCheckedAt: null,
      detail: target.monitoringNote ?? 'Nao ha mecanismo HTTP de health exposto para este servico.'
    };
  }
}
