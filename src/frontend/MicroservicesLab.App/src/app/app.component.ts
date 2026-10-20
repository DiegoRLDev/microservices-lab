import { Component, ElementRef, HostListener, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

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
  protected readonly menuOpen = signal(false);
  @ViewChild('menuButton') private menuButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('mainContent') private mainContent?: ElementRef<HTMLElement>;

  constructor() {
    inject(Router).events.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event instanceof NavigationEnd && this.menuOpen()) {
        this.menuOpen.set(false);
        this.mainContent?.nativeElement.focus();
      }
    });
  }

  protected toggleMenu() {
    this.menuOpen.update((open) => !open);
  }

  protected skipToContent(event: Event) {
    event.preventDefault();
    this.menuOpen.set(false);
    this.mainContent?.nativeElement.focus();
  }

  @HostListener('keydown.escape')
  protected closeMenu() {
    if (this.menuOpen()) {
      this.menuOpen.set(false);
      this.menuButton?.nativeElement.focus();
    }
  }

  protected readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/dashboard' },
    { label: 'Produtos', route: '/produtos' },
    { label: 'Pedidos', route: '/pedidos' },
    { label: 'Precos', route: '/precos' },
    { label: 'Auditoria', route: '/auditoria' },
    { label: 'Observabilidade', route: '/observabilidade' }
  ];
}
