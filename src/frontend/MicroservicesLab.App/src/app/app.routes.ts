import { Routes } from '@angular/router';
import { AuditPageComponent } from './pages/audit/audit-page.component';
import { DashboardPageComponent } from './pages/dashboard/dashboard-page.component';
import { OrdersPageComponent } from './pages/orders/orders-page.component';
import { ObservabilityPageComponent } from './pages/observability/observability-page.component';
import { PricesPageComponent } from './pages/prices/prices-page.component';
import { ProductsPageComponent } from './pages/products/products-page.component';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard'
  },
  {
    path: 'dashboard',
    component: DashboardPageComponent
  },
  {
    path: 'produtos',
    component: ProductsPageComponent
  },
  {
    path: 'pedidos',
    component: OrdersPageComponent
  },
  {
    path: 'precos',
    component: PricesPageComponent
  },
  {
    path: 'auditoria',
    component: AuditPageComponent
  },
  {
    path: 'observabilidade',
    component: ObservabilityPageComponent
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
