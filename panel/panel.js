(() => {
  const panel = document.getElementById('panel');
  const themes = ['classic', 'material', 'winui', 'flutter', 'glass', 'editorial', 'ambient', 'telemetry', 'studio'];
  const layoutThemes = ['editorial', 'ambient', 'telemetry', 'studio'];
  const fit = () => {
    // A fixed origin avoids the unscaled canvas affecting percentage centering.
    // visualViewport also covers a WebView with page zoom or a resized keyboard.
    const viewport = window.visualViewport;
    const width = Math.max(1, viewport?.width || innerWidth);
    const availableHeight = Math.max(1, viewport?.height || innerHeight);
    const landscape = width > availableHeight && layoutThemes.includes(document.body.dataset?.theme);
    const canvasWidth = landscape ? 2000 : 1200;
    const height = landscape ? Math.max(1040, Math.min(1700, canvasWidth * availableHeight / width)) :
      Math.max(2200, Math.min(3200, canvasWidth * availableHeight / width));
    const scale = Math.min(width / canvasWidth, availableHeight / height);
    const left = (viewport?.offsetLeft || 0) + (width - canvasWidth * scale) / 2;
    const top = (viewport?.offsetTop || 0) + (availableHeight - height * scale) / 2;
    panel.style.width = `${canvasWidth}px`;
    panel.style.height = `${height}px`;
    panel.style.transform = `translate(${left}px,${top}px) scale(${scale})`;
    document.body.classList[!landscape && height < 2400 ? 'add' : 'remove']('compact');
    document.body.classList[landscape ? 'add' : 'remove']('landscape');
  };
  addEventListener('resize', fit);
  window.visualViewport?.addEventListener('resize', fit);
  window.visualViewport?.addEventListener('scroll', fit);
  fit();
  let paused = false, connected = false, receivedAt = 0, sampleAge = 0, poll;
  const el = id => document.getElementById(id);
  function formatRate(value) {
    if (!Number.isFinite(value) || value < 0) return ['—','B/s'];
    const units = ['B/s','KB/s','MB/s','GB/s'];
    let scaled = value, unit = 0;
    while (scaled >= 1000 && unit < units.length - 1) { scaled /= 1000; unit++; }
    if (unit > 0 && Number(scaled.toFixed(1)) >= 1000 && unit < units.length - 1) { scaled /= 1000; unit++; }
    return [scaled.toFixed(unit === 0 ? 0 : 1),units[unit]];
  }
  function tick() {
    if (paused) return;
    const d = new Date();
    const pad = n => String(n).padStart(2, '0');
    el('clock').innerHTML = `${pad(d.getHours())}:${pad(d.getMinutes())}<span>:${pad(d.getSeconds())}</span>`;
    el('date').textContent = `${d.getFullYear()} / ${pad(d.getMonth()+1)} / ${pad(d.getDate())}`;
    el('weekday').textContent = ['SUNDAY','MONDAY','TUESDAY','WEDNESDAY','THURSDAY','FRIDAY','SATURDAY'][d.getDay()];
    if (connected && performance.now() - receivedAt > 12000) window.PanelDeck.offline();
    else if (connected && sampleAge + performance.now() - receivedAt > 20000) staleSample();
  }
  function staleSample() {
    document.body.classList.add('stale');
    el('freshness').textContent = '等待新采样';
    el('status').textContent = '●  已连接 · 硬件数据暂未更新';
  }
  window.PanelDeck = {
    setTheme(id) {
      const theme = themes.includes(id) ? id : 'classic';
      if (document.body.dataset) document.body.dataset.theme = theme;
      fit();
      return theme;
    },
    update(data) {
      // File-based Android previews are owned by the app. Browser drafts with
      // an explicit hash stay isolated from the saved, synchronised theme.
      if (location.protocol !== 'file:' && !/(?:^#|&)theme=/.test(location.hash || '') && themes.includes(data.panelTheme) && document.body.dataset?.theme !== data.panelTheme) {
        window.PanelDeck.setTheme(data.panelTheme);
      }
      connected = true; receivedAt = performance.now(); sampleAge = Math.max(0, Number(data.sampleAgeMs) || 0);
      document.body.classList.remove('stale');
      el('status').className = ''; el('status').textContent = '●  电脑已连接';
      el('cpu-name').textContent = data.cpu; el('gpu-name').textContent = data.gpu;
      el('reason').textContent = data.screen.reason;
      el('freshness').textContent = 'LIVE';
      el('cpu-fan-label').textContent = 'FAN 1';
      for (const node of document.querySelectorAll('[data-key]')) {
        const metric = data.metrics[node.dataset.key];
        if (node.dataset.format === 'rate') {
          const [value,unit] = formatRate(metric?.value);
          node.textContent = value; el(node.dataset.unit).textContent = unit;
        } else node.textContent = metric && Number.isFinite(metric.value) ? metric.value.toFixed(Number(node.dataset.decimals || 0)) : '—';
        node.title = metric?.source || '暂不可用';
      }
      const value = key => Math.max(0, Math.min(100, data.metrics[key]?.value || 0));
      el('cpu-ring').style.setProperty('--value', value('cpuLoad'));
      el('gpu-ring').style.setProperty('--value', value('gpuLoad'));
      el('ram-bar').style.width = value('ramLoad') + '%'; el('vram-bar').style.width = value('vramLoad') + '%';
      if (sampleAge > 20000) staleSample();
    },
    offline() { connected = false; document.body.classList.add('stale'); el('status').className = 'offline'; el('status').textContent = '○  电脑连接已中断'; el('freshness').textContent = '数据已过期'; },
    pause(value) { paused = value; if (!value) tick(); },
    battery(level, charging) { el('phone').textContent = `PHONE ${level}%${charging ? ' ⚡' : ''}`; }
  };
  const applyThemeHash = () => window.PanelDeck.setTheme((String(location.hash || '').match(/(?:^#|&)theme=([^&]+)/) || [])[1]);
  addEventListener('hashchange', applyThemeHash);
  applyThemeHash();
  tick(); setInterval(tick, 1000);
  if (location.protocol !== 'file:') {
    const fetchData = async () => { try { const r = await fetch('/api/preview', {cache:'no-store'}); if (!r.ok) throw Error(); window.PanelDeck.update(await r.json()); } catch { window.PanelDeck.offline(); } finally { poll = setTimeout(fetchData, document.hidden ? 15000 : 1000); } };
    fetchData();
  }
})();
