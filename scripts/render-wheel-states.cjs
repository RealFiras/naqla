// Export an exact contact sheet of the current UI proposal; no generated substitute art.
const { chromium } = require('../artifacts/preview-tools/node_modules/playwright');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

(async () => {
 const browser = await chromium.launch({channel:'msedge',headless:true});
 try {
  const page = await browser.newPage({viewport:{width:2250,height:1700},deviceScaleFactor:1});
  const errors=[]; page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.resolve(__dirname,'../preview/index.html')).href);
  await page.locator('#convert .sector').first().waitFor();
  await page.evaluate(() => {
   document.querySelector('main').style.display='none';
   const style=document.createElement('style');
   style.textContent=`
    .state-sheet{width:2250px;max-width:none;padding:30px 32px 25px;margin:0;direction:rtl}
    .sheet-header{display:flex;justify-content:space-between;align-items:center;border-bottom:1px solid #ffffff20;padding-bottom:20px;margin-bottom:22px}
    .sheet-title{font-size:29px;font-weight:650;margin:0 0 6px}
    .sheet-subtitle{font-size:13px;color:#aab5c4;margin:0}
    .sheet-brand{direction:ltr;font:700 26px Consolas,monospace;letter-spacing:2px;color:#ffcb7e}
    .sheet-section{margin-top:20px}
    .sheet-section h2{font-size:16px;font-weight:600;margin:0 0 11px}
    .state-grid{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:10px;direction:ltr}
    .state-card{background:radial-gradient(ellipse at 50% 43%,var(--glow),transparent 65%),linear-gradient(135deg,#23272e,#141c25);border:1px solid #37414f;border-radius:24px;padding:12px 12px 7px;overflow:hidden}
    .state-label{display:flex;align-items:center;justify-content:space-between;direction:rtl;min-height:32px}
    .state-label small{font-size:11px;color:#aeb9c8;direction:rtl}
    .state-card .wheel-stage{margin:7px auto 0;height:384px}
    .state-card .mode{top:331px}
    .sheet-foot{font-size:12px;color:#9aa5b6;border-top:1px solid #ffffff18;margin:23px 0 0;padding-top:14px}
    .sheet-foot strong{font-weight:500;color:#dfbf86}
    .state-sheet .spark{animation:none;opacity:.6}
   `;
   document.head.append(style);
   const main=document.createElement('main');main.className='state-sheet';
   main.innerHTML='<div class="sheet-header"><div><h1 class="sheet-title">العجلة بكل وضعياتها</h1><p class="sheet-subtitle">أنواع الملفات، جميع اختيارات تحويل الصور، وجميع أدوات الصورة — بالحجم الفعلي 320px</p></div><div class="sheet-brand">NAQLA / نقلة <span style="font-size:12px;color:#91a0b4;letter-spacing:1px">DESIGN 04 · MATCHED COLORS</span></div></div>';
   document.body.append(main);
   let sequence=0;
   function section(title,states){
    const wrapper=document.createElement('section');wrapper.className='sheet-section';
    const heading=document.createElement('h2');heading.textContent=title;
    const grid=document.createElement('div');grid.className='state-grid';
    wrapper.append(heading,grid);main.append(wrapper);
    for(const state of states){
     const f=files[state.category||'image'];const mode=state.mode||'convert';
     const card=document.createElement('article');card.className='state-card';setTheme(card,mode==='tools'?'tools':f.theme);
     const label=document.createElement('div');label.className='state-label';
     const title=document.createElement('span');title.className='category-capsule';capsule(title,mode==='tools'?'crop':f.icon,state.title||(mode==='tools'?'أدوات الصورة':f.title));
     const stateLabel=document.createElement('small');stateLabel.textContent=state.note||'وضع التحويل';label.append(title,stateLabel);
     const stage=document.createElement('div');stage.className='wheel-stage';stage.id='state-'+sequence++;
     card.append(label,stage);grid.append(card);
     new Wheel(stage.id,state.category||'image',mode,false);
     if(state.active===null) stage.querySelectorAll('.sector').forEach(s=>s.classList.remove('active'));
     else if(state.active) stage.querySelectorAll('.sector').forEach(s=>s.classList.toggle('active',s.dataset.format===state.active));
    }
   }
   section('01 / حسب نوع الملف',[
    {category:'image',note:'PNG · صور'},
    {category:'video',note:'MP4 · فيديو'},
    {category:'audio',note:'MP3 · صوت'},
    {category:'pdf',note:'PDF · مستند'},
    {mode:'tools',note:'وضع الأدوات'},
    {active:null,note:'بدون تحديد قطاع'}
   ]);
   section('02 / تحويل الصور — كل صيغة عند التحديد',files.image.options.map(format=>({active:format,title:format,note:'قطاع محدد'})));
   section('03 / أدوات الصورة — كل أداة عند التحديد',[
    ...tools.map(([label])=>({mode:'tools',active:label,title:label,note:'أداة محددة'})),
    {mode:'tools',active:null,note:'بدون تحديد أداة'}
   ]);
   const footer=document.createElement('p');footer.className='sheet-foot';footer.innerHTML='<strong>معاينة تصميم فقط — بانتظار الموافقة.</strong> أمثلة مرئية؛ لا تنفّذ تحويلًا. وضع تحويل مفعّل بالكبسولة اليمنى، ووضع أدوات بالكبسولة اليسرى. المركز ثابت عند تبديل الوضع.';main.append(footer);
  });
  await page.locator('.state-card').last().waitFor();
  if(await page.locator('.state-card').count()!==18) throw new Error('Expected all 18 preview states.');
  const dimensions=await page.locator('.state-sheet').boundingBox();
  await page.locator('.state-sheet').screenshot({path:path.resolve(__dirname,'../preview/wheel-all-states.png'),animations:'disabled'});
  if(errors.length)throw new Error(errors.join('\n'));
  console.log(JSON.stringify({image:'preview/wheel-all-states.png',states:18,width:dimensions.width,height:dimensions.height}));
 } finally {await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
