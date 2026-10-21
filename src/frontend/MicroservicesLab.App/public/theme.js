(function (window, document) {
  'use strict';
  var key = 'microserviceslab.appearance';
  var defaults = { mode: 'auto', accent: '#0f766e' };

  function normalize(value) {
    value = value || {};
    return {
      mode: ['light', 'dark', 'auto'].includes(value.mode) ? value.mode : defaults.mode,
      accent: typeof value.accent === 'string' && /^#[0-9a-f]{6}$/i.test(value.accent)
        ? value.accent.toLowerCase() : defaults.accent
    };
  }

  function load() {
    try { return normalize(JSON.parse(window.localStorage.getItem(key))); }
    catch (_) { return normalize(defaults); }
  }

  function rgb(hex) {
    return [1, 3, 5].map(function (offset) { return parseInt(hex.slice(offset, offset + 2), 16); });
  }

  function mix(color, target, weight) {
    var destination = rgb(target);
    return '#' + rgb(color).map(function (channel, i) {
      return Math.round(channel + (destination[i] - channel) * weight).toString(16).padStart(2, '0');
    }).join('');
  }

  function luminance(color) {
    var channels = rgb(color).map(function (channel) {
      var s = channel / 255;
      return s <= 0.04045 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
    });
    return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
  }

  function contrast(a, b) {
    var l1 = luminance(a), l2 = luminance(b);
    return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
  }

  function apply(value, systemDark) {
    var preferences = normalize(value);
    var dark = preferences.mode === 'dark' || (preferences.mode === 'auto' && systemDark);
    var surface = dark ? '#202225' : '#ffffff';
    var background = dark ? '#161819' : '#f6f7f9';
    var soft = !dark && preferences.accent === defaults.accent ? '#e7f4f2'
      : mix(surface, preferences.accent, dark ? 0.18 : 0.10);
    var text = !dark && preferences.accent === defaults.accent ? '#115e59' : preferences.accent;
    // Keep the chosen fill; adjust its text variant independently for readable links and focus rings.
    for (var step = 0; step <= 100; step++) {
      if ([surface, background, soft].every(function (bg) { return contrast(text, bg) >= 4.5; })) break;
      text = mix(preferences.accent, dark ? '#ffffff' : '#000000', step / 100);
    }
    var root = document.documentElement;
    root.dataset.theme = dark ? 'dark' : 'light';
    root.style.setProperty('--color-primary', preferences.accent);
    root.style.setProperty('--color-primary-strong', text);
    root.style.setProperty('--color-accent', text);
    root.style.setProperty('--color-primary-soft', soft);
    root.style.setProperty('--color-on-primary', contrast(preferences.accent, '#ffffff') >= contrast(preferences.accent, '#000000') ? '#ffffff' : '#000000');
    return preferences;
  }

  function save(value) {
    try { window.localStorage.setItem(key, JSON.stringify(normalize(value))); }
    catch (_) { /* Storage may be disabled; the current session still works. */ }
  }

  // Synchronous head script: the same normalization and palette run before Angular and on every change.
  window.mlabTheme = { load: load, apply: apply, save: save };
  apply(load(), !!window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
})(window, document);
