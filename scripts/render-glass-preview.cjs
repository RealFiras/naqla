// Show the slim selector in the same five palettes as its wheel.
const { chromium } = require('../artifacts/preview-tools/node_modules/playwright');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1840,height:650},deviceScaleFactor:2});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.resolve(__dirname,'../preview/index.html')).href);
  await page.locator('#convert .sector').first().waitFor();
  await page.evaluate(()=>{
   document.querySelector('main').style.display='none';
   const style=document.createElement('style');style.textContent=`
    .glass-sheet{width:1840px;max-width:none;height:650px;padding:30px;position:relative;overflow:hidden;background:radial-gradient(ellipse at 10% 82%,#91b8d216,transparent 38%),radial-gradient(ellipse at 90% 84%,#c2baaf12,transparent 42%),linear-gradient(160deg,#101721,#233241);direction:rtl}
    .mist{position:absolute;z-index:0;pointer-events:none;border-radius:50%;filter:blur(30px)}
    .mist.one{width:730px;height:75px;left:-140px;top:510px;background:#cce2ef;opacity:.16;transform:rotate(-12deg)}
    .mist.two{width:650px;height:70px;right:-120px;top:487px;background:#e3dac9;opacity:.12;transform:rotate(8deg)}
    .glass-heading{position:relative;z-index:1;display:flex;justify-content:space-between;align-items:start;margin-bottom:24px}
    .glass-heading h1{font-size:27px;margin:0 0 7px}
    .glass-heading p{font-size:12px;color:#bdcddd;margin:0}
    .glass-brand{font:700 20px Consolas,monospace;color:#b7c4d6;direction:ltr;letter-spacing:1px}
    .glass-pair{position:relative;z-index:1;display:grid;grid-template-columns:repeat(5,1fr);gap:16px;direction:ltr}
    .glass-example{text-align:center;position:relative;padding:6px 4px 0}
    .glass-example .category-capsule{direction:rtl;background:#14213366;border-color:#b5d3ec3b;box-shadow:inset 0 1px 0 #ffffff20;font-size:12px}
    .glass-example .wheel-stage{margin:11px auto 0}
    .glass-example .mode{top:340px}
    .glass-label{color:#d2dbe4;font-size:11px;direction:rtl;margin-top:8px}
    .glass-foot{position:absolute;bottom:22px;left:30px;right:30px;border-top:1px solid #e2f0ff24;padding-top:12px;font-size:11px;color:#a6b4c5}
    .glass-sheet .spark{animation:none;opacity:.65}
   `;document.head.append(style);
   const sheet=document.createElement('main');sheet.className='glass-sheet';sheet.innerHTML='<div class="mist one"></div><div class="mist two"></div><div class="glass-heading"><div><h1>كل عجلة، ومبدّل بنفس لونها.</h1><p>زجاج مطفي أنحف، زوايا أهدأ، وأيقونات أبسط — اللون من العجلة إلى التحديد.</p></div><div class="glass-brand">NAQLA / DESIGN 04</div></div><div class="glass-pair"></div><div class="glass-foot">معاينة تصميم فقط — الكهرماني للصور، المرجاني للفيديو، البنفسجي للصوت، الأزرق للـPDF، والأخضر للأدوات. بانتظار الموافقة.</div>';
   document.body.append(sheet);
   for(const [category,mode,label] of [['image','convert','صور · كهرماني'],['video','convert','فيديو · مرجاني'],['audio','convert','صوت · بنفسجي'],['pdf','convert','PDF · أزرق'],['image','tools','أدوات · أخضر']]){
    const card=document.createElement('section');card.className='glass-example';
    const title=document.createElement('span');title.className='category-capsule';capsule(title,mode==='tools'?'crop':files[category].icon,mode==='tools'?'أدوات الصورة':files[category].title);
    const stage=document.createElement('div');stage.className='wheel-stage';stage.id='glass-'+category+'-'+mode;
    const caption=document.createElement('p');caption.className='glass-label';caption.textContent=label;
    card.append(title,stage,caption);sheet.querySelector('.glass-pair').append(card);new Wheel(stage.id,category,mode,false);
   }
  });
  await page.locator('.glass-sheet').screenshot({path:path.resolve(__dirname,'../preview/themed-selector.png'),animations:'disabled'});
  if(errors.length)throw new Error(errors.join('\n'));
  console.log('Rendered preview/themed-selector.png (3680 × 1300).');
 }finally{await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
