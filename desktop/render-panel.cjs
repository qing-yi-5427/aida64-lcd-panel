// Isolated headless rendering. Never connects to the user's browser or live PC service.
const {chromium} = require('playwright');
const path = require('path'), fs = require('fs'), assert = require('assert/strict');
const {pathToFileURL} = require('url');
const legacy = ['classic','material','winui','flutter','glass'];
const allThemes = [...legacy,'editorial','ambient','telemetry','studio'];
const selected = process.env.PANELDECK_THEMES?.split(',') || allThemes;
const sizes = [[1200,2608],[1080,1920],[393,852],[800,1500],[1600,900],[800,600]];
const baseMetrics = {gameFps:144,gameFrameTime:6.9,gameFpsLow:103,cpuPower:105.8,gpuPower:288.6,cpuLoad:42,gpuLoad:98,cpuTemp:67,gpuTemp:73,cpuFan:1840,gpuFan:2150,cpuClock:5250,gpuClock:2820,ramLoad:48,ramUsed:30.7,vramLoad:72,vramUsed:22.4,netDownload:24600000,netUpload:3200000};
const metrics = values => Object.fromEntries(Object.entries(values).map(([key,value])=>[key,{value,source:key==="gameFps"?"示例游戏 · 显示 FPS":undefined}]));
const snapshot = values => ({cpu:'AMD Ryzen 7 9800X3D',gpu:'NVIDIA GeForce RTX 5090 D',metrics:metrics(values),sampleAgeMs:0,screen:{reason:'电脑运行中'}});

async function inspect(page, classic) {
  return page.evaluate(classic=> {
    const problems=[], rect=node=>node.getBoundingClientRect();
    const scale=rect(document.querySelector('#panel')).width / parseFloat(document.querySelector('#panel').style.width);
    for(const node of document.querySelectorAll('[data-key],.fps-detail-label,.stat label,#status,#phone,#reason,#freshness,#clock,#clock span,#weekday,#date')) {
      const r=rect(node), style=getComputedStyle(node);
      if(r.width<=0 || r.height<=0 || style.visibility==='hidden' || style.display==='none') problems.push('hidden: '+(node.dataset.key||node.id||node.textContent));
      if(r.left < -1 || r.top < -1 || r.right > innerWidth+1 || r.bottom > innerHeight+1) problems.push('outside viewport: '+(node.dataset.key||node.id||node.textContent));
      if(node.scrollWidth>node.clientWidth+1 && style.display!=='inline') problems.push('clipped: '+(node.dataset.key||node.id||node.textContent));
      for(let parent=node.parentElement;parent&&parent.id!=='panel';parent=parent.parentElement) {
        const ps=getComputedStyle(parent), pr=rect(parent);
        if(ps.display!=='contents' && (ps.overflowX==='hidden'||ps.overflowX==='clip') && (r.left<pr.left-1||r.right>pr.right+1)) problems.push('clipped by parent: '+(node.dataset.key||node.id||node.textContent));
        if(ps.display!=='contents' && (ps.overflowY==='hidden'||ps.overflowY==='clip') && (r.top<pr.top-1||r.bottom>pr.bottom+1)) problems.push('clipped vertically: '+(node.dataset.key||node.id||node.textContent));
      }
    }
    for(const selector of ['.power-cell','.stat','.memory-head','.connection','footer','.processor-head','.util-row','.net-cell','.fps-strip','.fps-value','.fps-details','.fps-detail','.footer-status','.time-row','header']) {
      for(const row of document.querySelectorAll(selector)) {
        const children=[...row.children].filter(n=>getComputedStyle(n).display!=='none').map(n=>({text:n.textContent,r:rect(n)}));
        for(let i=0;i<children.length;i++) for(let j=i+1;j<children.length;j++) {
          const a=children[i],b=children[j];
          if(Math.min(a.r.right,b.r.right)-Math.max(a.r.left,b.r.left)>2 && Math.min(a.r.bottom,b.r.bottom)-Math.max(a.r.top,b.r.top)>2) problems.push('overlap: '+a.text+' / '+b.text);
        }
      }
    }
    for(const row of document.querySelectorAll('.reading')) {
      const value=row.querySelector('strong'), unit=row.querySelector('small'), r=rect(row), v=rect(value), u=rect(unit);
      const gap=u.left-v.right;
      if(gap<-.5 || gap>12*scale+.6) problems.push('value-unit separated: '+value.dataset.key+' gap '+gap);
      if(u.right>r.right+1 || v.left<r.left-1) problems.push('reading overflow: '+value.dataset.key);
      if(parseFloat(getComputedStyle(unit).fontSize)>=parseFloat(getComputedStyle(value).fontSize)*.6) problems.push('unit too large: '+value.dataset.key);
      if(getComputedStyle(row).alignItems!=='baseline') problems.push('reading lacks baseline: '+value.dataset.key);
    }
    for(const node of document.querySelectorAll('.fps-detail strong')) if(parseFloat(getComputedStyle(node).fontSize)>=parseFloat(getComputedStyle(document.querySelector('#game-fps')).fontSize)*.75) problems.push('auxiliary frame reading too large');
    const metricNodes=[...document.querySelectorAll('[data-key]')];
    for(let i=0;i<metricNodes.length;i++) for(let j=i+1;j<metricNodes.length;j++) {
      const a=rect(metricNodes[i]),b=rect(metricNodes[j]);
      if(Math.min(a.right,b.right)-Math.max(a.left,b.left)>1 && Math.min(a.bottom,b.bottom)-Math.max(a.top,b.top)>1) problems.push('metric collision: '+metricNodes[i].dataset.key+' / '+metricNodes[j].dataset.key);
    }
    if(classic) {
      for(const column of ['cpu','gpu']) {
        const x=rect(document.querySelector('.processor.'+column)).left;
        for(const section of ['.power-cell.','.memory-cell.']) if(Math.abs(rect(document.querySelector(section+column)).left-x)>.6) problems.push('classic column start mismatch');
      }
      const left=[...document.querySelectorAll('.cpu .reading small')],right=[...document.querySelectorAll('.gpu .reading small')];
      left.forEach((node,i)=>{if(Math.abs(rect(node).bottom-rect(right[i]).bottom)>.6) problems.push('classic unit baseline mismatch '+i);});
    }
    return problems;
  }, classic);
}
(async()=> {
  const output=path.resolve(__dirname,'../artifacts/ui-2.8.0'); fs.mkdirSync(output,{recursive:true});
  const browser=await chromium.launch({headless:true,executablePath:process.env.PANELDECK_BROWSER||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  const errors=[], failures=[]; let checks=0;
  try {
    const page=await browser.newPage(); page.on('pageerror',error=>errors.push(error.message));
    for(const [width,height] of sizes) for(const theme of selected) {
      assert.ok(allThemes.includes(theme),'Unknown test theme');
      await page.setViewportSize({width,height});
      await page.goto(pathToFileURL(path.resolve(__dirname,'../panel/index.html')).href+'#theme='+theme);
      await page.evaluate(data=>{window.PanelDeck.update(data);window.PanelDeck.battery(83,true);window.PanelDeck.pause(true);document.getElementById('clock').innerHTML='21:48<span>:36</span>';},snapshot(baseMetrics));
      assert.equal(await page.locator('body').getAttribute('data-theme'),theme);
      assert.equal(await page.locator('#cpu-fan-label').innerText(),'FAN 1');
      assert.equal(await page.locator('[data-key]').count(),19);
      assert.match(await page.locator('#clock').innerText(),/^21:48/); checks+=4;
      const states={normal:baseMetrics,warmup:{...baseMetrics,gameFpsLow:null},missing:{...baseMetrics,gameFps:null,gameFrameTime:null,gameFpsLow:null,cpuPower:null,cpuTemp:null,cpuFan:null,cpuClock:null,netDownload:null,netUpload:null},maximum:{...baseMetrics,gameFps:9999,gameFrameTime:1999.9,gameFpsLow:9999,cpuLoad:100,gpuLoad:100,cpuFan:9999,gpuFan:9999,cpuClock:6000,gpuClock:6000,cpuTemp:100,gpuTemp:100,cpuPower:199.9,gpuPower:575.5,ramUsed:192.0,vramUsed:96.0,ramLoad:100,vramLoad:100,netDownload:999900000,netUpload:999900000}};
      for(const [state,values] of Object.entries(states)) {
        await page.evaluate(data=>window.PanelDeck.update(data),snapshot(values));
        const problems=await inspect(page,legacy.includes(theme)); checks++;
        if(problems.length) failures.push({theme,width,height,state,problems});
        if(state==='normal' || width===1200 || (!legacy.includes(theme)&&width===1600)) await page.screenshot({path:path.join(output,`phone-${theme}-${width}x${height}-${state}.png`)});
      }
      await page.evaluate(()=>{document.getElementById('cpu-name').textContent='AMD Ryzen Threadripper PRO 7995WX 96-Core Processor';document.getElementById('gpu-name').textContent='NVIDIA GeForce RTX 5090 D Founders Edition';window.PanelDeck.offline();});
      const offline=await inspect(page,legacy.includes(theme)); if(offline.length) failures.push({theme,width,height,state:'offline/long-name',problems:offline}); checks++;
      assert.equal(await page.locator('#freshness').innerText(),'数据已过期'); checks++;
    }
    const cdp=await page.context().newCDPSession(page);
    for(const [width,height] of [[800,1500],[1600,900]]) {
      await page.setViewportSize({width,height});
      for(const theme of selected) for(const factor of [1,1.25,1.5]) {
        await page.evaluate(theme=>window.PanelDeck.setTheme(theme),theme);
        await cdp.send('Emulation.setPageScaleFactor',{pageScaleFactor:factor});
        await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
        const clipped=await page.evaluate(()=>{const r=document.querySelector('#panel').getBoundingClientRect(),v=visualViewport;return r.left<v.offsetLeft-.6||r.top<v.offsetTop-.6||r.right>v.offsetLeft+v.width+.6||r.bottom>v.offsetTop+v.height+.6;});
        if(clipped) failures.push({theme,width,height,state:'zoom '+factor,problems:['canvas outside visual viewport']}); checks++;
      }
      await cdp.send('Emulation.setPageScaleFactor',{pageScaleFactor:1});
    }
    await page.evaluate(()=>window.PanelDeck.setTheme('unknown'));
    assert.equal(await page.locator('body').getAttribute('data-theme'),'classic'); assert.deepEqual(errors,[]); checks+=2;
    fs.writeFileSync(path.join(output,'layout-results.json'),JSON.stringify({checks,failures,errors},null,2));
    assert.deepEqual(failures,[], 'Theme layout failures; see artifacts/ui-2.8.0/layout-results.json');
    console.log(`PASS: ${checks} headless theme, portrait/landscape, baseline, missing/max/offline and zoom checks; screenshots: ${output}`);
  } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
