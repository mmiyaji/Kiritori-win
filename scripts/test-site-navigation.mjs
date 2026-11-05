// Focused regression checks for the Kiritori intro site's header navigation state.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const listeners = {}, timers = new Map();
let timerId = 0, frame;
const positions = { features: 600, usage: 1400, tips: 2100, faq: 2600 };
const links = Object.keys(positions).map(id => ({
  id, events: {}, attrs: {}, selected: false,
  getAttribute(name) { return name === 'href' ? '#' + id : this.attrs[name] || null; },
  hasAttribute(name) { return Object.hasOwn(this.attrs, name); },
  classList: { toggle(name, value) { if (name === 'on') links.find(link => link.id === id).selected = value; } },
  setAttribute(name, value) { this.attrs[name] = value; },
  removeAttribute(name) { delete this.attrs[name]; },
  addEventListener(name, fn) { this.events[name] = fn; }
}));
let scrollY = 0, headerHeight = 57;
const root = { classList: { remove() {} }, scrollHeight: 3000 };
const context = {
  URLSearchParams, location: { search: '', hash: '' },
  document: {
    documentElement: root,
    querySelectorAll: selector => selector.startsWith('header nav') ? links : [],
    querySelector: selector => selector === 'header' ? { getBoundingClientRect: () => ({ bottom: headerHeight }) } : null,
    getElementById(id) {
      if (id in positions) return { id, getBoundingClientRect: () => ({ top: positions[id] - scrollY }) };
      return null;
    }
  },
  innerHeight: 600,
  getComputedStyle: () => ({ scrollPaddingTop: '64px' }),
  get scrollY() { return scrollY; },
  addEventListener: (name, fn) => { listeners[name] = fn; },
  setTimeout: fn => { timers.set(++timerId, fn); return timerId; },
  clearTimeout: id => timers.delete(id),
  requestAnimationFrame: fn => { frame = fn; }
};
context.window = context;
vm.runInNewContext(fs.readFileSync('site/assets/site.js', 'utf8'), context);

const selected = () => links.filter(link => link.selected).map(link => link.id);
const flush = () => { const fn = frame; frame = null; fn?.(); };
const click = (id, overrides = {}) => links.find(link => link.id === id).events.click({ button: 0, ...overrides });

assert.deepEqual(selected(), [], 'hero/top area has no current section');
scrollY = 500; listeners.scroll(); flush();
assert.deepEqual(selected(), [], 'first section is not current before it reaches the header');
scrollY = 550; listeners.scroll(); flush();
assert.deepEqual(selected(), ['features'], 'first section becomes current at the sticky header edge');
headerHeight = 72;
scrollY = 530; listeners.resize(); flush();
assert.deepEqual(selected(), ['features'], 'current section follows the actual sticky header height');

click('usage');
scrollY = 1336.25; listeners.scrollend();
assert.deepEqual(selected(), ['usage'], 'anchor landing at 63.75px respects 64px scroll padding with a 57px header');

click('faq');
assert.deepEqual(selected(), ['faq'], 'click selects the destination immediately');
scrollY = 1200; listeners.scroll(); flush();
assert.deepEqual(selected(), ['faq'], 'smooth scroll keeps the clicked destination selected');
scrollY = 1200; listeners.scrollend();
assert.deepEqual(selected(), ['features'], 'scrollend releases the click hold and follows the visible section');

click('tips', { ctrlKey: true });
click('tips', { button: 1 });
links.find(link => link.id === 'tips').attrs.target = '_blank';
click('tips');
delete links.find(link => link.id === 'tips').attrs.target;
links.find(link => link.id === 'tips').attrs.download = '';
click('tips');
delete links.find(link => link.id === 'tips').attrs.download;
assert.deepEqual(selected(), ['features'], 'modifier, middle, target, and download clicks do not change selection');

click('faq');
scrollY = 2400; listeners.scrollend();
assert.deepEqual(selected(), ['faq'], 'short final section is selected at page bottom');
context.location.hash = '#features'; listeners.hashchange();
assert.deepEqual(selected(), ['features'], 'back/forward hash navigation selects the destination');

click('usage');
scrollY = 800; listeners.wheel(); listeners.scroll(); flush();
assert.deepEqual(selected(), ['features'], 'manual scrolling interrupts a click hold');
headerHeight = 56;
scrollY = 2100; listeners.resize(); flush();
assert.deepEqual(selected(), ['tips'], 'resize recalculates the visible section');
assert.equal(links.filter(link => link.attrs['aria-current'] === 'location').length, 1);
assert.equal(links.find(link => link.selected).attrs['aria-current'], 'location');
console.log('Site navigation regression checks passed.');
