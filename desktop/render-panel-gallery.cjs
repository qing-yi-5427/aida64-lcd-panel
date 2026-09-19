// A standalone, local review page. Images contain fixture data, never live PC data.
const fs=require('fs'),path=require('path');
const {pathToFileURL}=require('url');
const {chromium}=require('playwright');
const out=path.resolve(__dirname,'../artifacts/ui-2.5.0');
const themes=[
  ['editorial','01','纸页','数据周刊','暖纸色、章节排版、细分隔线。适合喜欢清晰阅读层次的桌面。'],
  ['ambient','02','静夜','桌面时钟','时间成为主视觉，硬件以紧凑条目排列。适合晚间常驻。'],
  ['telemetry','03','遥测','性能座舱','全宽数据轨道、精确网格与高对比提示。适合快速比较硬件状态。'],
  ['studio','04','拼贴','硬件工作室','GPU 主块、CPU 辅助条与独立功耗瓷砖。适合更有层次的展示。']
];
const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>曜屏 · 四种面板布局</title>
<style>*{box-sizing:border-box}body{margin:0;background:#eeeee8;color:#242824;font-family:"Segoe UI","Microsoft YaHei UI",sans-serif}main{max-width:1600px;margin:auto;padding:44px 36px}header{display:flex;justify-content:space-between;align-items:end;gap:30px;margin-bottom:30px}.kicker{font-size:12px;letter-spacing:3px;color:#667063;font-weight:700}h1{font-size:36px;margin:12px 0 10px;letter-spacing:-1px;font-weight:600}p{margin:0;color:#62695f;line-height:1.7;font-size:14px}.controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap;font-size:13px}button,select{background:#fafaf6;border:1px solid #c9d0c3;border-radius:9px;padding:10px 14px;color:#283326;font:inherit;cursor:pointer}button.active{background:#26392c;color:#fff;border-color:#26392c}button:focus-visible,select:focus-visible{outline:3px solid #688d75;outline-offset:3px}.grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:22px}article{min-width:0}.caption{display:flex;align-items:baseline;gap:10px;margin:0 0 13px}.number{font-size:12px;font-weight:600;color:#788371}h2{margin:0;font-size:22px;font-weight:600}.subtitle{margin-left:auto;font-size:12px;color:#667063}.preview{background:#d5d9cf;border:1px solid #d0d4ca;border-radius:16px;padding:7px;box-shadow:0 7px 20px #1b2e2010}.preview img{display:block;width:100%;aspect-ratio:1200/2608;border-radius:10px;background:#111;object-fit:contain}article p{font-size:12px;margin-top:14px;line-height:1.8}footer{border-top:1px solid #c9d0c3;margin-top:28px;padding-top:18px;display:flex;justify-content:space-between;gap:24px;color:#667063;font-size:12px;line-height:1.7}body.landscape .grid{grid-template-columns:repeat(2,minmax(0,1fr))}body.landscape img{aspect-ratio:1600/900}@media(max-width:900px){header{align-items:start;flex-direction:column}.grid{grid-template-columns:repeat(2,minmax(0,1fr))}main{padding:26px 18px}h1{font-size:28px}}@media(max-width:500px){.grid,body.landscape .grid{grid-template-columns:1fr}footer{flex-direction:column}}</style>
<main><header><div><div class="kicker">PANELDECK / LAYOUT COLLECTION</div><h1>同一台电脑，四种桌面表达。</h1><p>完整时间、14 项硬件读数与上传/下载网速。以下为示例数据，不是实时硬件状态。</p></div><div class="controls"><button id="portrait" class="active" aria-pressed="true">竖屏</button><button id="landscape" aria-pressed="false">横屏</button><select id="state" aria-label="数据状态"><option value="normal">完整数据</option><option value="missing">部分数据缺失</option><option value="maximum">极值布局检查</option></select></div></header>
<section class="grid">${themes.map(([id,number,title,subtitle,description])=>`<article><div class="caption"><span class="number">${number}</span><h2>${title}</h2><span class="subtitle">${subtitle}</span></div><div class="preview"><img data-theme="${id}" src="phone-${id}-1200x2608-normal.png" alt="${title}主题预览"></div><p>${description}</p></article>`).join('')}</section>
<footer><span>手机：设置 → 面板外观 → 选择主题 → 全屏预览 → 保存设置。<br>原有经典、Material、WinUI、Flutter、玻璃主题继续保留。</span><span>本地系统字体 · 无背景动画 · 数值单位紧邻<br>四套新增布局支持横竖屏分别重排</span></footer></main>
<script>let orientation='portrait';function update(){document.body.className=orientation==='landscape'?'landscape':'';for(const mode of ['portrait','landscape']){const b=document.getElementById(mode);b.classList.toggle('active',mode===orientation);b.setAttribute('aria-pressed',String(mode===orientation));}for(const image of document.querySelectorAll('img'))image.src='phone-'+image.dataset.theme+'-'+(orientation==='landscape'?'1600x900':'1200x2608')+'-'+document.getElementById('state').value+'.png';}document.getElementById('portrait').onclick=()=>{orientation='portrait';update();};document.getElementById('landscape').onclick=()=>{orientation='landscape';update();};document.getElementById('state').onchange=update;</script></html>`;
(async()=>{
  fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'index.html'),html);
  const browser=await chromium.launch({headless:true,executablePath:process.env.PANELDECK_BROWSER||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
  try {
    const page=await browser.newPage({viewport:{width:1520,height:1050},deviceScaleFactor:1});
    await page.goto(pathToFileURL(path.join(out,'index.html')).href);
    await page.evaluate(()=>Promise.all([...document.images].map(image=>image.decode())));
    await page.screenshot({path:path.join(out,'four-layouts.png'),fullPage:true});
    await page.locator('#landscape').click();
    await page.evaluate(()=>Promise.all([...document.images].map(image=>image.decode())));
    await page.screenshot({path:path.join(out,'four-layouts-landscape.png'),fullPage:true});
    for(const mode of ['portrait','landscape']) {
      await page.locator('#'+mode).click();
      for(const state of ['normal','missing','maximum']) {
        await page.locator('#state').selectOption(state);
        await page.evaluate(()=>Promise.all([...document.images].map(image=>image.decode())));
      }
    }
    console.log('PASS: 4 layout previews, both orientations and 3 data states; '+path.join(out,'index.html'));
  } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
