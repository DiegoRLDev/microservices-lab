import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_ENDPOINTS } from './api-endpoints';

export interface ProductResponse {
  id: string;
  name: string;
  price: number;
  availableQuantity: number;
}

@Injectable({ providedIn: 'root' })
export class InventoryApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoints = inject(API_ENDPOINTS);

  listProducts() {
    return this.http.get<ProductResponse[]>(`${this.endpoints.inventory}/products`);
  }
}
