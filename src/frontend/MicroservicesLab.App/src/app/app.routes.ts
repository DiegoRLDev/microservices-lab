import { Routes } from '@angular/router';
import { PlaceholderPageComponent } from './pages/placeholder/placeholder-page.component';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard'
  },
  {
    path: 'dashboard',
    component: PlaceholderPageComponent,
    data: {
      title: 'Dashboard',
      description: 'Visao inicial do laboratorio e status geral dos servicos.',
      phase: 'Fase 24'
    }
  },
  {
    path: 'produtos',
    component: PlaceholderPageComponent,
    data: {
      title: 'Produtos e estoque',
      description: 'Area preparada para cadastro, consulta e disponibilidade de produtos.',
      phase: 'Fase 25'
    }
  },
  {
    path: 'pedidos',
    component: PlaceholderPageComponent,
    data: {
      title: 'Pedidos e pagamentos',
      description: 'Area preparada para criacao de pedidos e acompanhamento do status de pagamento.',
      phase: 'Fase 26'
    }
  },
  {
    path: 'precos',
    component: PlaceholderPageComponent,
    data: {
      title: 'Precos realtime',
      description: 'Area preparada para observar precos via SSE e APIs de pricing.',
      phase: 'Fase 27'
    }
  },
  {
    path: 'auditoria',
    component: PlaceholderPageComponent,
    data: {
      title: 'Auditoria',
      description: 'Area reservada para eventos auditados quando a API de consulta existir.',
      phase: 'Fase 28'
    }
  },
  {
    path: 'observabilidade',
    component: PlaceholderPageComponent,
    data: {
      title: 'Observabilidade',
      description: 'Area preparada para atalhos e leituras basicas de operacao do laboratorio.',
      phase: 'Fase 28'
    }
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
