// Wiki AXIN — lógica compartida entre AxinMenuGui y AXINServerBridge.
// Cada HTML define window.WIKI_DEFAULT_PAGE con la página inicial (por defecto 'home').

function showPage(id) {
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.nav-link').forEach(l => l.classList.remove('active'));
  const page = document.getElementById('page-' + id);
  if (page) { page.classList.add('active'); window.scrollTo(0, 0); }
  document.querySelectorAll('.nav-link').forEach(l => {
    const oc = l.getAttribute('onclick');
    if (oc && oc.includes("'" + id + "'")) l.classList.add('active');
  });
  document.body.classList.toggle('gen-mode', id === 'generator');
  buildTOC();
}

function buildTOC() {
  const toc = document.getElementById('toc-links');
  if (!toc) return;
  toc.innerHTML = '';
  const activePage = document.querySelector('.page.active');
  if (!activePage) return;
  activePage.querySelectorAll('h2,h3').forEach((h, i) => {
    if (!h.id) h.id = 'toc-h-' + i;
    const a = document.createElement('a');
    a.className = 'toc-item';
    if (h.tagName === 'H3') a.style.paddingLeft = '20px';
    a.textContent = h.textContent.replace(/[^\w\s\-áéíóúñÁÉÍÓÚÑ]/g, '').trim();
    a.href = '#' + h.id;
    a.onclick = e => { e.preventDefault(); document.getElementById(h.id).scrollIntoView({ behavior: 'smooth' }); };
    toc.appendChild(a);
  });
}

function handleSearch(e) {
  if (e.key !== 'Enter') return;
  const q = e.target.value.toLowerCase().trim();
  if (!q) return;
  for (const page of document.querySelectorAll('.page')) {
    if (page.textContent.toLowerCase().includes(q)) {
      showPage(page.id.replace('page-', ''));
      e.target.value = '';
      return;
    }
  }
}

document.addEventListener('keydown', e => {
  if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
    e.preventDefault();
    const input = document.getElementById('searchInput');
    if (input) input.focus();
  }
});

document.addEventListener('DOMContentLoaded', () => {
  const def = (window.WIKI_DEFAULT_PAGE || 'home');
  showPage(def);
});
