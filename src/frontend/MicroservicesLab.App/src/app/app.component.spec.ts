import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AppComponent } from './app.component';
import template from './app.component.html?raw';

@Component({ standalone: true, template: '<p>Pagina do laboratorio</p>' })
class TestPage {}

describe('Responsive navigation', () => {
  let fixture: ComponentFixture<AppComponent>;
  let button: HTMLButtonElement;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideRouter([
        { path: 'dashboard', component: TestPage },
        { path: 'produtos', component: TestPage }
      ])]
    }).overrideComponent(AppComponent, {
      set: { template, templateUrl: undefined, styleUrl: undefined, styleUrls: [], styles: [] }
    }).compileComponents();
    fixture = TestBed.createComponent(AppComponent);
    router = TestBed.inject(Router);
    fixture.detectChanges();
    await router.navigateByUrl('/dashboard');
    fixture.detectChanges();
    button = fixture.nativeElement.querySelector('.menu-toggle');
    document.body.appendChild(fixture.nativeElement);
  });

  it('toggles the disclosure and exposes all six routes with accessible state', () => {
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(button.getAttribute('aria-controls')).toBe('main-navigation');
    expect(fixture.nativeElement.querySelectorAll('nav a')).toHaveLength(6);
    button.click();
    fixture.detectChanges();
    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(button.getAttribute('aria-label')).toBe('Fechar menu');
    button.click();
    fixture.detectChanges();
    expect(button.getAttribute('aria-expanded')).toBe('false');
  });

  it('closes with Escape and restores focus to the menu button', () => {
    button.click();
    fixture.detectChanges();
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('nav a');
    link.focus();
    link.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(button);
  });

  it('closes after navigation and moves focus to the content', async () => {
    button.click();
    fixture.detectChanges();
    await router.navigateByUrl('/produtos');
    fixture.detectChanges();
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('main'));
    expect(fixture.nativeElement.querySelector('a[href="/produtos"]').getAttribute('aria-current')).toBe('page');
  });

  it('skips to content without the base href sending the user to another route', async () => {
    await router.navigateByUrl('/produtos');
    fixture.detectChanges();
    const event = new MouseEvent('click', { bubbles: true, cancelable: true });
    fixture.nativeElement.querySelector('.skip-link').dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
    expect(router.url).toBe('/produtos');
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('main'));
  });

  it.each([
    ['Amarelo', '#eab308'], ['Rosa', '#ec4899'], ['Grafite', '#374151']
  ])('selects the %s swatch and persists it', (name, color) => {
    const root: HTMLElement = fixture.nativeElement;
    root.querySelector('details')!.open = true;
    expect(root.querySelectorAll('.swatch')).toHaveLength(9);
    const swatch = root.querySelector<HTMLButtonElement>(`button[aria-label="${name}"]`)!;
    swatch.click();
    fixture.detectChanges();
    expect(swatch.getAttribute('aria-pressed')).toBe('true');
    expect(root.querySelectorAll('.swatch[aria-pressed="true"]')).toHaveLength(1);
    expect(root.querySelector<HTMLInputElement>('input[type="color"]')!.value).toBe(color);
    expect(JSON.parse(localStorage.getItem('microserviceslab.appearance')!).accent).toBe(color);
    localStorage.removeItem('microserviceslab.appearance');
  });

  it('exposes labeled appearance controls and applies theme, preset and custom color', () => {
    const root: HTMLElement = fixture.nativeElement;
    const disclosure = root.querySelector('details')!;
    disclosure.open = true;
    const dark = root.querySelector<HTMLInputElement>('input[value="dark"]')!;
    dark.click();
    fixture.detectChanges();
    expect(dark.checked).toBe(true);
    expect(document.documentElement.dataset['theme']).toBe('dark');
    const blue = root.querySelector<HTMLButtonElement>('button[aria-label="Azul"]')!;
    blue.click();
    fixture.detectChanges();
    expect(blue.getAttribute('aria-pressed')).toBe('true');
    expect(root.querySelectorAll('.swatch[aria-pressed="true"]')).toHaveLength(1);
    const picker = root.querySelector<HTMLInputElement>('input[type="color"]')!;
    picker.value = '#ffffff';
    picker.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(root.querySelector<HTMLInputElement>('input[aria-label="Cor hexadecimal"]')!.value).toBe('#ffffff');
    expect(root.querySelectorAll('.swatch[aria-pressed="true"]')).toHaveLength(0);
    const hex = root.querySelector<HTMLInputElement>('input[aria-label="Cor hexadecimal"]')!;
    hex.value = '#abcdef';
    hex.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(picker.value).toBe('#abcdef');
    expect(document.documentElement.style.getPropertyValue('--color-primary')).toBe('#abcdef');
    localStorage.removeItem('microserviceslab.appearance');
  });
});
