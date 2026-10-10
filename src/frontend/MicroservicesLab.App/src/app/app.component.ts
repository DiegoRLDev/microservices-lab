import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

interface NavItem {
  label: string;
  route: string;
}

@Component({
  selector: 'mlab-root',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  protected readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/dashboard' },
    { label: 'Produtos', route: '/produtos' },
    { label: 'Pedidos', route: '/pedidos' },
    { label: 'Precos', route: '/precos' },
    { label: 'Auditoria', route: '/auditoria' },
    { label: 'Observabilidade', route: '/observabilidade' }
  ];
}
