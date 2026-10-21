import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_ENDPOINTS } from './api-endpoints';

export interface PriceResponse {
  symbol: string;
  price: number;
  occurredAt: string;
}

@Injectable({ providedIn: 'root' })
export class PricingApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoints = inject(API_ENDPOINTS);

  listPrices() {
    return this.http.get<PriceResponse[]>(`${this.endpoints.pricing}/prices`);
  }
}
