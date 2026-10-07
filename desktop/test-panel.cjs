const fs = require('fs'), vm = require('vm'), assert = require('assert/strict');
const nodes = new Map();
const node = id => { if (!nodes.has(id)) nodes.set(id, {style: {setProperty() {}}, classList: {add() {}, remove() {}}}); return nodes.get(id); };
let now = 1000, tick;
const rateNode = node('net-download');
rateNode.dataset = {key:'netDownload',format:'rate',unit:'download-unit'};
const fpsNode = node('game-fps'); fpsNode.dataset = {key:'gameFps'};
const frameTime = node('game-frame-time'); frameTime.dataset = {key:'gameFrameTime',decimals:'1'};
const lowFps = node('game-fps-low'); lowFps.dataset = {key:'gameFpsLow'};
const context = {
  document: {getElementById: node, querySelectorAll: () => [rateNode,fpsNode,frameTime,lowFps], body: node('body')},
  window: {}, performance: {now: () => now}, Date, innerWidth: 1200, innerHeight: 2608,
  addEventListener() {}, setInterval(callback) { tick = callback; }, location: {protocol: 'file:'}
};
vm.runInNewContext(fs.readFileSync(require('path').join(__dirname, '../panel/panel.js'), 'utf8'), context);
const data = {timestamp: 1, sampleAgeMs: 50, metrics: {}, screen: {reason: 'awake'}, cpu: 'CPU', gpu: 'GPU'};
context.window.PanelDeck.update(data);
assert.equal(node('freshness').textContent, 'LIVE'); // Phone wall clock and PC timestamp intentionally disagree.
context.window.PanelDeck.update({...data, sampleAgeMs: 30000});
assert.equal(node('freshness').textContent, '等待新采样');
assert.equal(node('status').textContent, '●  已连接 · 硬件数据暂未更新');
now += 13000; tick();
assert.equal(node('status').textContent, '○  电脑连接已中断');
context.window.PanelDeck.update(data);
assert.equal(node('freshness').textContent, 'LIVE');
// Explicit disconnect must win over an old hardware sample. This can happen when
// a cached snapshot is shown during theme preview immediately before network loss.
context.window.PanelDeck.update({...data, sampleAgeMs: 30000});
context.window.PanelDeck.offline();
assert.equal(node('status').textContent, '○  电脑连接已中断');
now += 1000; tick();
assert.equal(node('status').textContent, '○  电脑连接已中断');
assert.equal(node('freshness').textContent, '数据已过期');
context.window.PanelDeck.pause(true);
now += 1000;
context.window.PanelDeck.pause(false);
assert.equal(node('status').textContent, '○  电脑连接已中断');
// A new snapshot alone restores the connection, even if its hardware sample is old.
context.window.PanelDeck.update({...data, sampleAgeMs: 30000});
assert.equal(node('status').textContent, '●  已连接 · 硬件数据暂未更新');
context.window.PanelDeck.update(data);
assert.equal(node('freshness').textContent, 'LIVE');
for (const [value,text,unit] of [[0,'0','B/s'],[850,'850','B/s'],[24600000,'24.6','MB/s'],[999960,'1.0','MB/s'],[1500000000,'1.5','GB/s'],[null,'—','B/s'],[-1,'—','B/s'],[Infinity,'—','B/s']]) {
  context.window.PanelDeck.update({...data,metrics:{netDownload:{value}}});
  assert.equal(rateNode.textContent,text); assert.equal(node('download-unit').textContent,unit);
}
console.log('PASS: panel connection/stale/recovery and network rate units, zero/missing/invalid values (27 checks)');

context.window.PanelDeck.update({...data,metrics:{gameFps:{value:144.2,source:'test-game · 提交 FPS'}}});
assert.equal(fpsNode.textContent,'144'); assert.equal(node('fps-source').textContent,'test-game · 提交 FPS');
context.window.PanelDeck.offline(); assert.equal(fpsNode.textContent,'—');
context.window.PanelDeck.update({...data,metrics:{gameFps:{value:null,source:'等待全屏游戏'}}});
assert.equal(fpsNode.textContent,'—'); assert.equal(node('fps-source').textContent,'等待全屏游戏');
context.window.PanelDeck.update(data); assert.equal(fpsNode.textContent,'—');
assert.equal(node('fps-source').textContent,'当前电脑版本未提供帧率');
context.window.PanelDeck.update({...data,sampleAgeMs:30000,metrics:{gameFps:{value:144}}});
assert.equal(fpsNode.textContent,'—');
console.log('PASS: FPS live, missing, offline, stale and old-PC compatibility');
const pacing = {...data,metrics:{gameFps:{value:144},gameFrameTime:{value:6.944},gameFpsLow:{value:98.7}}};
context.window.PanelDeck.update(pacing);
assert.equal(frameTime.textContent,'6.9'); assert.equal(lowFps.textContent,'99');
assert.equal(node('fps-low-window').textContent,'30s');
context.window.PanelDeck.update({...pacing,metrics:{...pacing.metrics,gameFpsLow:{value:null}}});
assert.equal(lowFps.textContent,'—'); assert.equal(node('fps-low-window').textContent,'统计中');
context.window.PanelDeck.offline();
assert.equal(frameTime.textContent,'—'); assert.equal(lowFps.textContent,'—');
context.window.PanelDeck.update({...pacing,sampleAgeMs:30000});
assert.equal(frameTime.textContent,'—'); assert.equal(lowFps.textContent,'—');
context.window.PanelDeck.update({...data,metrics:{gameFps:{value:60}}});
assert.equal(frameTime.textContent,'—'); assert.equal(lowFps.textContent,'—');
assert.equal(node('fps-low-window').textContent,'30s');
console.log('PASS: frame pacing, warmup, expiry, disconnect and older PC compatibility');
