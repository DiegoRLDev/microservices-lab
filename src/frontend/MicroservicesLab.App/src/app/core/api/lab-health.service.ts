import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_ENDPOINTS } from './api-endpoints';

export interface ServiceHealthTarget {
  name: string;
  url: string;
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

  check(target: ServiceHealthTarget) {
    return this.http.get(target.url, { responseType: 'text' });
  }
}
