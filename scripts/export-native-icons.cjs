// Rasterize the existing approved vector pixel icons. No generated artwork.
const {chromium}=require('../artifacts/preview-tools/node_modules/playwright');
const {pathToFileURL}=require('node:url');
const path=require('node:path');
const fs=require('node:fs');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:96,height:96},deviceScaleFactor:1});
  await page.goto(pathToFileURL(path.resolve(__dirname,'../preview/index.html')).href);
  const ids=await page.evaluate(()=>[...document.querySelectorAll('#shared-defs symbol')].map(x=>x.id));
  await page.evaluate(()=>{
   const defs=document.querySelector('#shared-defs').outerHTML;
   document.body.innerHTML=`<svg id="export" width="64" height="64" viewBox="0 0 32 32" style="position:absolute;left:0;top:0;color:#d4f7e8;shape-rendering:crispEdges">${defs}<use id="glyph" width="32" height="32"/></svg>`;
   document.documentElement.style.background='transparent';document.body.style.cssText='margin:0;background:transparent';
  });
  const dir=path.resolve(__dirname,'../src/Naqla.Wheel/Assets');fs.mkdirSync(dir,{recursive:true});
  for(const id of ids){
   await page.evaluate(id=>document.querySelector('#glyph').setAttribute('href','#'+id),id);
   await page.locator('#export').screenshot({path:path.join(dir,id+'.png'),omitBackground:true});
  }
  console.log(`Exported ${ids.length} approved pixel icons.`);
 } finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
