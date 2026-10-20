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
});
