// Browser-only review of repository previews; does not prove Windows input behavior.
const { chromium } = require('../artifacts/preview-tools/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const assert = require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 const results=[];
 try {
  for(const [file,tabs] of [['context-editors.html',10],['pdf-toolkit.html',8]]){
   const page=await browser.newPage({viewport:{width:1200,height:950}});
   const errors=[];page.on('pageerror',e=>errors.push(e.message));
   await page.goto(pathToFileURL(path.resolve(__dirname,'../preview',file)).href);
   const buttons=page.locator('nav button');assert.equal(await buttons.count(),tabs);
   for(let i=0;i<tabs;i++){
    await buttons.nth(i).click();
    await page.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0));
   }
   await page.screenshot({path:path.resolve(__dirname,'../artifacts',file+'.png'),fullPage:true});
   await page.setViewportSize({width:390,height:844});
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'No document overflow at 390px');
   assert.deepEqual(errors,[]);
   results.push({File:file,LoadedStates:tabs,Desktop:true,Mobile390NoDocumentOverflow:true,Errors:errors});
   await page.close();
  }
  const page=await browser.newPage();
  await page.goto(pathToFileURL(path.resolve(__dirname,'../preview/tools-pagination.html')).href);
  const before=await page.locator('#caption-a').textContent();
  assert(await page.locator('#a .sector').count()<=6);
  await page.keyboard.press('Tab');assert.notEqual(await page.locator('#caption-a').textContent(),before);
  await page.keyboard.press('Shift+Tab');assert.equal(await page.locator('#caption-a').textContent(),before);
  results.push({File:'tools-pagination.html',ForwardBackward:true,NativeWindowsInputVerified:false});
  fs.writeFileSync(path.resolve(__dirname,'../artifacts/editor-preview-verification.json'),JSON.stringify(results,null,2));
  console.log(JSON.stringify(results,null,2));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
