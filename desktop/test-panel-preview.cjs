// Isolated browser server and synthetic readings; never opens the user's browser.
const {chromium}=require('playwright');
const http=require('http'),fs=require('fs'),path=require('path'),assert=require('assert/strict');
const root=path.resolve(__dirname,'../panel'),out=path.resolve(__dirname,'../artifacts/ui-theme-sync');
const android=fs.readFileSync(path.resolve(__dirname,'../app/src/main/java/com/paneldeck/aida/PanelTheme.java'),'utf8');
const themes=android.match(/String\[\] IDS = \{([^}]+)\}/)[1].match(/"[^"]+"/g).map(JSON.parse);
const catalog=fs.readFileSync(path.resolve(__dirname,'PanelDeck.Desktop/Models.cs'),'utf8');
for(const theme of themes) assert(catalog.includes(`new("${theme}",`),`PC theme missing: ${theme}`);
let selected='glass',checks=0;
const server=http.createServer((req,res)=>{
  if(req.url==='/api/preview') {res.setHeader('Content-Type','application/json');res.end(JSON.stringify({panelTheme:selected,cpu:'PREVIEW CPU',gpu:'PREVIEW GPU',metrics:{},screen:{reason:'合成测试数据'},sampleAgeMs:0}));return;}
  const pathname=new URL(req.url,'http://localhost').pathname;
  const name=pathname==='/'?'index.html':pathname==='/preview'?'preview.html':pathname.slice(1);
  if(!/^[\w.-]+$/.test(name)||!fs.existsSync(path.join(root,name))){res.writeHead(404);res.end();return;}
  res.setHeader('Content-Type',name.endsWith('.css')?'text/css':name.endsWith('.js')?'text/javascript':'text/html');
  res.end(fs.readFileSync(path.join(root,name)));
});
(async()=>{
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const base=`http://127.0.0.1:${server.address().port}`;
  const browser=await chromium.launch({headless:true,channel:'msedge'});
  fs.mkdirSync(out,{recursive:true});
  try {
    const page=await browser.newPage({viewport:{width:1280,height:900}}),errors=[];
    page.on('pageerror',error=>errors.push(error.message));
    await page.goto(base+'/preview');
    const panel=page.frameLocator('#panel-frame');
    await panel.locator('body[data-theme="glass"]').waitFor();checks++;
    selected='studio';await panel.locator('body[data-theme="studio"]').waitFor();checks++;
    await page.screenshot({path:path.join(out,'follow-phone-portrait.png')});
    for(const theme of themes) {
      await page.goto(base+'/preview?theme='+theme);
      await panel.locator(`body[data-theme="${theme}"]`).waitFor();
      await panel.locator('#freshness').filter({hasText:'LIVE'}).waitFor();checks+=2;
      assert.equal(await panel.locator('body').getAttribute('data-theme'),theme);checks++;
      for(const orientation of ['portrait','landscape']) {
        await page.locator('#'+orientation).click();
        const size=await page.locator('#panel-frame').boundingBox();
        assert(Math.abs(size.width/size.height-(orientation==='portrait'?1200/2608:2608/1200))<.01);checks++;
        await panel.locator('#clock').isVisible().then(value=>assert(value));checks++;
      }
    }
    await page.goto(base+'/preview?theme=editorial');
    await panel.locator('body[data-theme="editorial"]').waitFor();
    selected='ambient';
    // The browser's next real data response cannot overwrite an unsaved draft.
    await page.waitForResponse(r=>r.url().endsWith('/api/preview'));
    assert.equal(await panel.locator('body').getAttribute('data-theme'),'editorial');checks++;
    await page.locator('#follow').click();
    await panel.locator('body[data-theme="ambient"]').waitFor();checks++;
    await page.locator('#landscape').click();
    await page.screenshot({path:path.join(out,'follow-phone-landscape.png')});
    await page.goto(base+'/preview?theme=constructor');
    await panel.locator('body[data-theme="ambient"]').waitFor();checks++;
    await page.setViewportSize({width:540,height:680});
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);checks++;
    assert.deepEqual(errors,[]);checks++;
    fs.writeFileSync(path.join(out,'preview-checks.txt'),`PASS ${checks} browser preview and shared-theme checks.\n`);
    console.log(`PASS ${checks} browser preview and shared-theme checks`);
  } finally {await browser.close();server.close();}
})().catch(error=>{console.error(error);server.close();process.exitCode=1;});
