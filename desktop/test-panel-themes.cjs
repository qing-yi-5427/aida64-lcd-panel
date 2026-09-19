const fs = require('fs'), vm = require('vm'), assert = require('assert/strict'), path = require('path');
const source = fs.readFileSync(path.join(__dirname, '../panel/panel.js'), 'utf8');
const html = fs.readFileSync(path.join(__dirname, '../panel/index.html'), 'utf8');
const css = [...html.matchAll(/<link rel="stylesheet" href="([^"]+)"/g)].map(match => fs.readFileSync(path.join(__dirname, '../panel', match[1]), 'utf8')).join('\n');
const themeContract = fs.readFileSync(path.join(__dirname, '../app/src/main/java/com/paneldeck/aida/PanelTheme.java'), 'utf8');
const themes = themeContract.match(/String\[\] IDS = \{([^}]+)\}/)[1].match(/"[^"]+"/g).map(value => JSON.parse(value));
let checks = 0;
function equal(actual, expected) { assert.equal(actual, expected); checks++; }
function boot(hash, width = 1200, height = 2608, visualViewport = undefined) {
  const nodes = new Map(), classes = new Set();
  const node = id => {
    if (!nodes.has(id)) nodes.set(id, { dataset: {}, style: {setProperty() {}}, classList: {add: x => classes.add(x), remove: x => classes.delete(x)} });
    return nodes.get(id);
  };
  const context = { document: { getElementById: node, querySelectorAll: () => [], body: node('body') },
    window: {visualViewport}, performance: {now: () => 100}, Date, innerWidth: width, innerHeight: height,
    addEventListener() {}, setInterval() {}, location: {protocol: 'file:', hash} };
  vm.runInNewContext(source, context);
  return { api: context.window.PanelDeck, node, classes };
}
for (const theme of themes) {
  const {api, node} = boot(`#theme=${theme}`);
  equal(node('body').dataset.theme, theme);
  equal(api.setTheme('unknown'), 'classic');
  equal(node('body').dataset.theme, 'classic');
  equal(api.setTheme(theme), theme);
  api.update({cpu: 'CPU', gpu: 'GPU', screen: {reason: '在线'}, metrics: {cpuFan: {value: 2300, label: 'CPU FAN · 暂定'}}});
  equal(node('cpu-fan-label').textContent, 'FAN 1');
  api.offline(); api.pause(true); api.pause(false); api.battery(82, true);
  equal(node('body').dataset.theme, theme);
  equal(node('phone').textContent, 'PHONE 82% ⚡');
  assert.ok(css.includes(`[data-theme="${theme}"]`)); checks++;
}
for (const hash of ['', '#theme=__proto__', '#theme=glass%22%3E', '#theme=constructor', '#theme=Material'])
  equal(boot(hash).node('body').dataset.theme, 'classic');
const short = boot('#theme=winui', 1080, 1920);
equal(short.classes.has('compact'), true);
equal(short.node('panel').style.height, '2200px');
equal(boot('#theme=glass', 1200, 3000).node('panel').style.height, '3000px');
assert.ok(html.includes('id="cpu-fan-label">FAN 1</label>')); checks++;
assert.ok(!/(?:https?:\/\/|@import|url\()/i.test(css)); checks++;
assert.ok(!/(?:backdrop-filter\s*:|animation\s*:|transition\s*:)/i.test(css)); checks++;
// Exercise the actual fit formula against normal, short, wide and zoomed viewports.
// Every transformed corner must remain within the visible viewport, not merely
// the unzoomed layout viewport (which caused a cropped left edge on WebView).
for (const [width, height, viewport] of [
  [1200, 2608], [1080, 1920], [797, 1509], [393, 852], [1600, 900],
  [1200, 2608, {width: 600, height: 1304, offsetLeft: 173, offsetTop: 81, addEventListener() {}}],
  [980, 1900, {width: 393, height: 740, offsetLeft: 0, offsetTop: 0, addEventListener() {}}]
]) {
  const {node} = boot('#theme=classic', width, height, viewport);
  const fit = node('panel').style.transform.match(/^translate\(([^,]+)px,([^,]+)px\) scale\(([^)]+)\)$/);
  assert.ok(fit, 'explicit origin transform'); checks++;
  const left = Number(fit[1]), top = Number(fit[2]), scale = Number(fit[3]);
  const visibleWidth = viewport?.width || width, visibleHeight = viewport?.height || height;
  const originX = viewport?.offsetLeft || 0, originY = viewport?.offsetTop || 0;
  assert.ok(left >= originX - 0.01 && left + 1200 * scale <= originX + visibleWidth + 0.01, 'horizontal corners fit'); checks++;
  assert.ok(top >= originY - 0.01 && top + parseFloat(node('panel').style.height) * scale <= originY + visibleHeight + 0.01, 'vertical corners fit'); checks++;
}
// All large metrics, including initially missing values, use the same baseline wrapper.
equal((html.match(/<strong data-key=/g) || []).length, (html.match(/<div class="(?:used )?reading"><strong data-key=/g) || []).length);
assert.ok(!html.includes('class="vertical"')); checks++;
assert.ok(css.includes('.power,.processors,.memory{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:var(--gap)')); checks++;
assert.ok(css.includes('transform-origin:0 0')); checks++;
for (const theme of ['editorial','ambient','telemetry','studio']) {
  const {api,node,classes} = boot('#theme='+theme,1600,900);
  equal(node('panel').style.width,'2000px'); equal(classes.has('landscape'),true); equal(classes.has('compact'),false);
  api.setTheme('classic'); equal(node('panel').style.width,'1200px'); equal(classes.has('landscape'),false);
  api.setTheme(theme); equal(node('panel').style.width,'2000px'); equal(classes.has('landscape'),true);
}
console.log(`PASS: ${checks} panel theme checks (Android/JS theme contract, baseline wrappers, portrait/landscape switching, zoomed viewport bounds and static assets)`);
