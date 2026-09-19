(() => {
  const names = {classic:'经典 · 仪表盘',material:'Material · 安卓',winui:'WinUI · 微软',flutter:'Flutter · 清新卡片',glass:'macOS · 玻璃质感',editorial:'纸页 · 数据周刊',ambient:'静夜 · 桌面时钟',telemetry:'遥测 · 性能座舱',studio:'拼贴 · 硬件工作室'};
  const query = new URLSearchParams(location.search), frame = document.getElementById('panel-frame');
  const device = document.getElementById('device'), mode = document.getElementById('mode');
  const requested = query.get('theme');
  const theme = Object.hasOwn(names, requested) ? requested : null;
  let landscape = query.get('orientation') === 'landscape';
  function fit() {
    const main = document.querySelector('main');
    const availableWidth = Math.max(1, main.clientWidth - 56);
    const chrome = document.querySelector('header').offsetHeight + document.querySelector('footer').offsetHeight;
    const availableHeight = Math.max(180, innerHeight - chrome - 56);
    const ratio = landscape ? 2608 / 1200 : 1200 / 2608;
    const width = Math.min(availableWidth, availableHeight * ratio);
    device.style.width = width + 'px'; device.style.height = width / ratio + 'px';
    document.getElementById('portrait').setAttribute('aria-pressed', String(!landscape));
    document.getElementById('landscape').setAttribute('aria-pressed', String(landscape));
  }
  function follow() { frame.src = '/'; mode.textContent = '跟随已保存主题 · 与手机同步'; }
  if (theme) { frame.src = '/#theme=' + theme; mode.textContent = names[theme] + ' · 仅预览，尚未保存'; } else follow();
  document.getElementById('portrait').onclick = () => { landscape = false; fit(); };
  document.getElementById('landscape').onclick = () => { landscape = true; fit(); };
  document.getElementById('follow').onclick = follow;
  addEventListener('resize', fit); fit();
})();
