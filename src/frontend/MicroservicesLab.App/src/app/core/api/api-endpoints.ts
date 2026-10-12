import { InjectionToken } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface ObservabilityLinks {
  jaeger: string;
  prometheus: string;
  grafana: string;
}

export interface ApiEndpoints {
  order: string;
  inventory: string;
  priceFeed: string;
  pricing: string;
  audit: string;
  observability: ObservabilityLinks;
}

export const API_ENDPOINTS = new InjectionToken<ApiEndpoints>('MicroservicesLab API endpoints', {
  providedIn: 'root',
  factory: () => environment.api
});
