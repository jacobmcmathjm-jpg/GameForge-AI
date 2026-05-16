(function() {
  try {
    document.documentElement.classList.add('gf-performance-mode');
    document.body && document.body.classList.add('gf-performance-mode');
  } catch(e) {}
})();
