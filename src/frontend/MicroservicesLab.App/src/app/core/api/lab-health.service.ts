import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { timeout } from 'rxjs';
import { API_ENDPOINTS } from './api-endpoints';

export interface ServiceHealthTarget {
  name: string;
  url: string;
}

export interface LabServiceHealthTarget {
  name: string;
  technology: string;
  responsibility: string;
  healthUrl: string | null;
  monitoringNote?: string;
}

@Injectable({ providedIn: 'root' })
export class LabHealthService {
  private readonly http = inject(HttpClient);
  private readonly endpoints = inject(API_ENDPOINTS);

  readonly targets: ServiceHealthTarget[] = [
    { name: 'Order.Service', url: `${this.endpoints.order}/health` },
    { name: 'Inventory.Service', url: `${this.endpoints.inventory}/health` },
    { name: 'PriceFeed.Service', url: `${this.endpoints.priceFeed}/health` },
    { name: 'Pricing.Service', url: `${this.endpoints.pricing}/health` }
  ];

  readonly observabilityTargets: LabServiceHealthTarget[] = [
    {
      name: 'Order.Service',
      technology: '.NET 9',
      responsibility: 'Cria pedidos, consulta estoque por gRPC e publica eventos de pedido.',
      healthUrl: `${this.endpoints.order}/health`
    },
    {
      name: 'Inventory.Service',
      technology: '.NET 9',
      responsibility: 'Mantem produtos, estoque e disponibilidade para pedidos.',
      healthUrl: `${this.endpoints.inventory}/health`
    },
    {
      name: 'Payment.Service',
      technology: 'Go',
      responsibility: 'Consome pedidos no Kafka e publica eventos de pagamento.',
      healthUrl: null,
      monitoringNote: 'Sem endpoint HTTP de health exposto nesta fase.'
    },
    {
      name: 'Audit.Worker',
      technology: '.NET 9',
      responsibility: 'Consome eventos Kafka e persiste auditoria em PostgreSQL.',
      healthUrl: `${this.endpoints.audit}/health`
    },
    {
      name: 'PriceFeed.Service',
      technology: 'Go',
      responsibility: 'Produz atualizacoes de precos por SSE, gRPC e Kafka.',
      healthUrl: `${this.endpoints.priceFeed}/health`
    },
    {
      name: 'Pricing.Service',
      technology: '.NET 9',
      responsibility: 'Consome precos do Kafka e expoe o ultimo preco conhecido.',
      healthUrl: `${this.endpoints.pricing}/health`
    }
  ];

  check(target: ServiceHealthTarget) {
    return this.checkUrl(target.url);
  }

  checkUrl(url: string) {
    return this.http.get(url, { responseType: 'text' }).pipe(timeout(5000));
  }
}
