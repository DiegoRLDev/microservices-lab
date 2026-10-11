import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_ENDPOINTS } from './api-endpoints';

export type OrderStatus = number | string;

export interface OrderResponse {
  id: string;
  productId: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  status: OrderStatus;
  createdAt: string;
}

export interface CreateOrderRequest {
  productId: string;
  quantity: number;
}

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoints = inject(API_ENDPOINTS);

  listOrders() {
    return this.http.get<OrderResponse[]>(`${this.endpoints.order}/orders`);
  }

  getOrder(id: string) {
    return this.http.get<OrderResponse>(`${this.endpoints.order}/orders/${id}`);
  }

  createOrder(request: CreateOrderRequest) {
    return this.http.post<OrderResponse>(`${this.endpoints.order}/orders`, request);
  }
}
