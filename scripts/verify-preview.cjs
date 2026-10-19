// Repository-local browser verification; does not drive the user's open browser.
const { chromium } = require('../artifacts/preview-tools/node_modules/playwright');
const path = require('node:path');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const { pathToFileURL } = require('node:url');
(async () => {
 const browser = await chromium.launch({channel:'msedge',headless:true});
 try {
  const page = await browser.newPage({viewport:{width:1224,height:1120},deviceScaleFactor:1});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.resolve(__dirname,'../preview/index.html')).href);
  await page.locator('#convert .sector').first().waitFor();
  await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>a.effect.getTiming().iterations!==Infinity).map(a=>a.finished)));
  assert.equal(await page.locator('#convert .sector').count(),6);
  assert.equal(await page.locator('#tools .sector').count(),5);
  assert.deepEqual(await page.locator('#convert .sector text').allTextContents(),['JPG','WEBP','AVIF','TIFF','HEIC','PDF']);
  assert.equal(await page.locator('.family-card').count(),5);
  const themeColors = await page.locator('.family-card').evaluateAll(cards=>cards.map(c=>getComputedStyle(c).getPropertyValue('--accent')));
  assert.equal(new Set(themeColors).size,5,'Category colors must be distinct');
  assert.equal(await page.locator('#paper-HEIC path').nth(1).getAttribute('fill'),'#f281bb');
  assert.equal(await page.locator('#paper-WEBP path').nth(1).getAttribute('fill'),'#8bd461');
  const checkSelectorColor=async()=>{
   const colors=await page.locator('#convert').evaluate(w=>{
    const ctx=document.createElement('canvas').getContext('2d');
    ctx.fillStyle=getComputedStyle(w).getPropertyValue('--bright').trim();const expected=ctx.fillStyle;
    ctx.fillStyle=getComputedStyle(w.querySelector('.mode button[aria-pressed=true]')).color;
    return{expected,actual:ctx.fillStyle};
   });
   assert.equal(colors.actual,colors.expected,'Selector must use its current wheel palette');
  };
  for (const [category,source,theme] of [['video','MP4','video'],['audio','MP3','audio'],['pdf','PDF','pdf'],['image','PNG','image']]) {
   await page.locator(`.category-tab[data-category=${category}]`).click();
   assert.equal(await page.locator('#convert').getAttribute('data-theme'),theme);
   assert.equal(await page.locator('#convert .hub-type').textContent(),source);
   assert.equal((await page.locator('#convert .sector text').allTextContents()).includes(source),false);
   await page.waitForTimeout(220);
   await checkSelectorColor();
  }
  await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>a.effect.getTiming().iterations!==Infinity).map(a=>a.finished)));
  const rect=await page.locator('#convert .wheel-svg').boundingBox(); assert.equal(rect.width,320);assert.equal(rect.height,320);
  const hub=await page.locator('#convert .hub').boundingBox();
  await page.keyboard.down('Control');
  await page.waitForFunction(()=>document.getElementById('convert').dataset.mode==='tools');
  assert.equal(await page.locator('#convert .sector text').first().textContent(),'قص');
  assert.equal(await page.locator('#convert').getAttribute('data-theme'),'tools');
  await page.waitForTimeout(220);
  await checkSelectorColor();
  assert.deepEqual(await page.locator('#convert .hub').boundingBox(),hub);
  await page.keyboard.up('Control');
  await page.waitForFunction(()=>document.getElementById('convert').dataset.mode==='convert');
  assert.equal(await page.locator('#convert .sector text').first().textContent(),'JPG');
  await page.locator('#convert [data-mode=tools]').click();
  await page.waitForFunction(()=>document.getElementById('convert').dataset.mode==='tools');
  await page.locator('#convert [data-mode=convert]').click();
  await page.waitForFunction(()=>document.getElementById('convert').dataset.mode==='convert');
  await page.keyboard.press('Escape'); assert.equal(await page.locator('.wheel-stage.closed').count(),2);
  await page.locator('#reopen').click();assert.equal(await page.locator('.wheel-stage.closed').count(),0);
  await page.locator('#replay').click();
  await page.waitForFunction(()=>document.getElementById('convert').dataset.mode==='tools');
  await page.waitForFunction(()=>document.getElementById('convert').dataset.mode==='convert');
  await page.waitForTimeout(230);
  await page.mouse.move(20,20);
  await page.screenshot({path:path.resolve(__dirname,'../preview/approval-preview.png'),fullPage:true});
  await page.locator('.family-section').screenshot({path:path.resolve(__dirname,'../preview/approval-family.png')});
  await page.locator('#convert .sector').nth(2).hover();
  assert.equal(await page.locator('#convert .sector.active text').textContent(),'AVIF');
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,'Mobile overflow');
  assert.equal(errors.length,0,errors.join('\n'));
  const report={revision:4,passed:true,checks:['Six contiguous image sectors, five tools','Five category palettes and capsule labels','Per-format colored pixel icons','All category capsule switches and source omission','Selector color matches every wheel palette, including Tools','320 CSS px at 1×','Ctrl hold → teal Tools; release → Convert','Hub position unchanged on mode switch','Both redesigned frosted selector controls','Escape close and opening replay','Animated round trip','Hover highlight','390 px responsive layout','No JS page errors'],screenshot:'preview/approval-preview.png',family:'preview/approval-family.png'};
  fs.writeFileSync(path.resolve(__dirname,'../artifacts/preview-tests.json'),JSON.stringify(report,null,2));
  console.log(JSON.stringify(report,null,2));
 } finally {await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
