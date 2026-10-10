import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_ENDPOINTS } from './api-endpoints';

export interface ProductResponse {
  id: string;
  name: string;
  price: number;
  availableQuantity: number;
}

export interface CreateProductRequest {
  name: string;
  price: number;
  availableQuantity: number;
}

export interface CheckAvailabilityRequest {
  quantity: number;
}

export interface ProductAvailabilityResponse {
  productId: string;
  isAvailable: boolean;
  requestedQuantity: number;
  availableQuantity: number;
  unitPrice: number;
}

@Injectable({ providedIn: 'root' })
export class InventoryApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoints = inject(API_ENDPOINTS);

  listProducts() {
    return this.http.get<ProductResponse[]>(`${this.endpoints.inventory}/products`);
  }

  createProduct(request: CreateProductRequest) {
    return this.http.post<ProductResponse>(`${this.endpoints.inventory}/products`, request);
  }

  getProduct(id: string) {
    return this.http.get<ProductResponse>(`${this.endpoints.inventory}/products/${id}`);
  }

  checkAvailability(id: string, request: CheckAvailabilityRequest) {
    return this.http.post<ProductAvailabilityResponse>(
      `${this.endpoints.inventory}/products/${id}/availability`,
      request
    );
  }
}
