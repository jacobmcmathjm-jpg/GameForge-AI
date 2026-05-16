(function() {
  try {
    const btn = document.createElement('button');
    btn.className = 'simple-mode-toggle';
    btn.textContent = 'Simple Mode';
    btn.addEventListener('click', () => {
      document.body.classList.toggle('gf-simple-mode');
      document.body.classList.toggle('gf-advanced-mode');
      btn.textContent = document.body.classList.contains('gf-simple-mode') ? 'Advanced Mode' : 'Simple Mode';
    });
    document.addEventListener('DOMContentLoaded', () => {
      document.body && document.body.appendChild(btn);
    });
  } catch(e) {}
})();
