// Runs before the page draws: applies the saved light/dark choice and hides the page until English text is in place.
(function () {
  var root = document.documentElement;
  try {
    var saved = localStorage.getItem('kiritori-theme');
    if (saved === 'light' || saved === 'dark') root.dataset.theme = saved;
  } catch (e) { /* storage may be unavailable */ }
  if (new URLSearchParams(location.search).get('lang') === 'en') root.classList.add('en-pending');
})();
