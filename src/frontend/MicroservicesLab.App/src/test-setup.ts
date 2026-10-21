import '@angular/compiler';
import 'zone.js';
import 'zone.js/testing';
import { getTestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { afterEach } from 'vitest';
import themeBootstrap from '../public/theme.js?raw';

new Function('window', 'document', themeBootstrap)(window, document);

getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting(), {
  teardown: { destroyAfterEach: true }
});

afterEach(() => {
  getTestBed().resetTestingModule();
});
