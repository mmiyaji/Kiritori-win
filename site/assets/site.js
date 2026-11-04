// Language menu, light/dark toggle, demo video switch and GLightbox for the Kiritori intro page.
(function () {
  var root = document.documentElement;
  var english = new URLSearchParams(location.search).get('lang') === 'en';
  var EN = window.KR_EN || {};

  if (english) {
    root.lang = 'en';
    if (EN['meta.title']) document.title = EN['meta.title'];
    var description = document.querySelector('meta[name="description"]');
    if (description && EN['meta.description']) description.content = EN['meta.description'];
    document.querySelectorAll('[data-i18n]').forEach(function (el) {
      var text = EN[el.getAttribute('data-i18n')];
      if (typeof text === 'string') el.innerHTML = text;
    });
    // Images: swap -ja. for -en. and take alt / lightbox description from the dictionary.
    document.querySelectorAll('[data-img]').forEach(function (el) {
      var key = 'img.' + el.getAttribute('data-img');
      var img = el.tagName === 'IMG' ? el : el.querySelector('img');
      if (el.tagName === 'A') {
        el.href = el.getAttribute('href').replace('-ja.', '-en.');
        if (EN[key]) el.setAttribute('data-description', EN[key]);
      }
      if (img) {
        img.src = img.getAttribute('src').replace('-ja.', '-en.');
        if (EN[key]) img.alt = EN[key];
      }
    });
    document.querySelectorAll('[data-en-src]').forEach(function (img) { img.src = img.getAttribute('data-en-src'); });
  }
  root.classList.remove('en-pending');

  // Language menu (same behaviour as the Chat Agent Bridge site)
  var current = english ? 'en' : 'ja';
  var names = { ja: '日本語', en: 'English' };
  var langButton = document.getElementById('lang');
  var langLabel = document.getElementById('lang-label');
  var langList = document.getElementById('lang-list');
  if (langButton && langLabel && langList) {
    langLabel.textContent = names[current];
    langLabel.lang = current;
    langButton.setAttribute('aria-label', (english ? 'Language: ' : '言語: ') + names[current]);
    langList.querySelectorAll('a[data-lang]').forEach(function (link) {
      var code = link.getAttribute('data-lang');
      link.href = location.pathname + (code === 'ja' ? '' : '?lang=' + code) + location.hash;
      if (code === current) link.setAttribute('aria-current', 'true');
    });
    var links = function () { return Array.prototype.slice.call(langList.querySelectorAll('a')); };
    var setOpen = function (open, focusFirst) {
      langList.hidden = !open;
      langButton.setAttribute('aria-expanded', open ? 'true' : 'false');
      if (open && focusFirst) (langList.querySelector('[aria-current]') || links()[0]).focus();
    };
    langButton.addEventListener('click', function () { setOpen(langList.hidden, false); });
    langButton.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown' || e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setOpen(true, true); }
    });
    langList.addEventListener('keydown', function (e) {
      var items = links(), i = items.indexOf(document.activeElement);
      if (e.key === 'Escape') { setOpen(false); langButton.focus(); }
      else if (e.key === 'ArrowDown') { e.preventDefault(); items[(i + 1) % items.length].focus(); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); items[(i - 1 + items.length) % items.length].focus(); }
    });
    document.addEventListener('click', function (e) {
      if (!langList.hidden && !e.target.closest('.langmenu')) setOpen(false);
    });
    langList.addEventListener('focusout', function (e) {
      if (!e.relatedTarget || !e.relatedTarget.closest('.langmenu')) setOpen(false);
    });
  }

  // Light / dark toggle. Default follows the OS (light when unknown).
  var themeButton = document.getElementById('theme');
  var labels = english ? ['Switch to light mode', 'Switch to dark mode'] : ['ライトモードに切り替え', 'ダークモードに切り替え'];
  function theme() { return root.dataset.theme || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'); }
  function label() { themeButton.setAttribute('aria-label', theme() === 'dark' ? labels[0] : labels[1]); }
  if (themeButton) {
    label();
    themeButton.addEventListener('click', function () {
      root.dataset.theme = theme() === 'dark' ? 'light' : 'dark';
      try { localStorage.setItem('kiritori-theme', root.dataset.theme); } catch (e) { /* not persisted */ }
      label();
    });
  }

  // Demo video: switch between the capture and live preview clips.
  var video = document.getElementById('demo');
  if (video) {
    var sizeTag = document.getElementById('video-size');
    if (sizeTag) video.addEventListener('loadedmetadata', function () {
      sizeTag.textContent = video.videoWidth + ' × ' + video.videoHeight;
    });
    var tabs = Array.prototype.slice.call(document.querySelectorAll('[data-demo]'));
    var caption = document.getElementById('demo-caption');
    var active = 'capture';
    var select = function (tab) {
      tabs.forEach(function (t) {
        var on = t === tab;
        t.setAttribute('aria-selected', String(on));
        t.tabIndex = on ? 0 : -1;
      });
      var next = tab.getAttribute('data-demo');
      if (caption) caption.innerHTML = tab.getAttribute('data-caption' + (english ? '-en' : ''));
      if (next === active) return;
      var base = next === 'live' ? 'kiritori_demo_live' : 'kiritori_demo';
      var sources = video.querySelectorAll('source');
      video.pause();
      sources[0].src = 'assets/video/' + base + '.webm';
      sources[1].src = 'assets/video/' + base + '.mp4';
      video.poster = 'assets/video/demo-' + next + '-poster.jpg';
      video.load();
      active = next;
    };
    tabs.forEach(function (tab, i) {
      tab.addEventListener('click', function () { select(tab); });
      tab.addEventListener('keydown', function (e) {
        var n;
        if (e.key === 'ArrowRight') n = (i + 1) % tabs.length;
        else if (e.key === 'ArrowLeft') n = (i - 1 + tabs.length) % tabs.length;
        else return;
        e.preventDefault(); select(tabs[n]); tabs[n].focus();
      });
    });
    if (english && caption && tabs[0]) caption.innerHTML = tabs[0].getAttribute('data-caption-en');
  }

  var year = document.getElementById('year');
  if (year) year.textContent = new Date().getFullYear();

  if (window.GLightbox) GLightbox({ selector: '.glightbox', touchNavigation: true, loop: true });
})();
