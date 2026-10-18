/* Exercise the shipped file:// HTML in a real offline Chromium context. */
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require('C:/Users/wang/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname,'..');
const folder = path.join(root,'Logs/SealValidation/Mobile');
fs.mkdirSync(folder,{recursive:true});
function requireTrue(value, message) { if(!value) throw Error(message); }
let browser;
(async () => {
  browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
  const context=await browser.newContext({viewport:{width:844,height:390},deviceScaleFactor:1,hasTouch:true,isMobile:true,offline:true});
  const page=await context.newPage(), errors=[], network=[];
  page.on('pageerror',error=>errors.push(String(error)));
  page.on('request',request=>{if(/^https?:/i.test(request.url()))network.push(request.url());});
  await page.goto(pathToFileURL(path.join(root,'Builds/H5/SealRhythm.html')).href);
  await page.waitForFunction(()=>window.__sealReady && window.SEAL_GAME);
  requireTrue(await page.evaluate(()=>window.__sealReady),'Embedded images loaded');
  const layouts=[];
  for(const viewport of [{width:844,height:390},{width:390,height:844},{width:1280,height:720}]) {
    await page.setViewportSize(viewport); await page.waitForTimeout(150);
    const result=await page.evaluate(()=>{
      const box=id=>{const r=document.getElementById(id).getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height,right:r.right,bottom:r.bottom};};
      return {viewport:{width:innerWidth,height:innerHeight},canvas:box('scene'),header:box('header'),controls:box('controls'),buttons:[...document.querySelectorAll('.action')].map(b=>({width:b.clientWidth,height:b.clientHeight,x:b.getBoundingClientRect().x,right:b.getBoundingClientRect().right})),card:box('ready'),dialogue:box('dialogue'),scroll:[document.documentElement.scrollWidth,document.documentElement.scrollHeight]};
    });
    requireTrue(result.canvas.width===viewport.width&&result.canvas.height===viewport.height,'Canvas covers full viewport');
    requireTrue(result.controls.width>=viewport.width*.90,'Controls span at least 90% viewport');
    requireTrue(result.buttons.every(b=>b.width>=95&&b.height>=85&&b.x>=0&&b.right<=viewport.width),'Large safe touch targets');
    requireTrue(result.card.x>=0&&result.card.right<=viewport.width&&result.card.y>=0,'Menu inside phone viewport');
    requireTrue(result.card.y>=result.header.bottom&&result.card.bottom<result.controls.y,'Menu clears beat lane and touch controls');
    requireTrue(result.dialogue.x>=0&&result.dialogue.right<=viewport.width&&result.dialogue.y>=0&&result.dialogue.bottom<result.controls.y,'Chinese speech clears touch controls');
    requireTrue(result.scroll[0]===viewport.width&&result.scroll[1]===viewport.height,'No page scrolling');
    layouts.push(result);await page.screenshot({path:path.join(folder,`h5-ready-${viewport.width}x${viewport.height}.png`)});
  }
  if(process.argv.includes('--layout-only')) {
    requireTrue(errors.length===0&&network.length===0,'Final layout has no errors or network requests');
    fs.writeFileSync(path.join(folder,'h5-final-layout.json'),JSON.stringify({pass:true,layouts,errors,network},null,2));
    console.log('PASS: final offline file layouts, clear header and controls, no requests/errors');
    await browser.close();return;
  }
  await page.setViewportSize({width:844,height:390});
  await page.locator('#practice').tap();
  await page.waitForFunction(()=>window.SEAL_GAME.state==='countin');
  const audio=await page.evaluate(()=>({state:SEAL_GAME._context.state,rate:SEAL_GAME._buffer.sampleRate,length:SEAL_GAME._buffer.length,duration:SEAL_GAME._buffer.duration}));
  requireTrue(audio.state==='running'&&Math.abs(audio.duration-4687488/44100)<.001,'Embedded MP3 decodes with exact song length');
  const installAutoplay=()=>page.evaluate(()=>{
    if(window.__autoPress)clearInterval(window.__autoPress);
    window.__inputSent=new Set();
    window.__autoPress=setInterval(()=>{
      const g=window.SEAL_GAME;g.tick();if(g.state!=='playing')return;
      const note=g.notes[g.judge.nextNoteIndex];if(!note||window.__inputSent.has(note.sample))return;
      if(g.time>=note.time-.007){window.__inputSent.add(note.sample);const b=document.querySelector(`.action[data-input="${note.input}"]`);
        b.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true,pointerType:'touch',pointerId:1,button:0}));
        b.dispatchEvent(new PointerEvent('pointerup',{bubbles:true,pointerType:'touch',pointerId:1,button:0}));}
    },4);
  });
  await installAutoplay();
  await page.waitForFunction(()=>SEAL_GAME.judge.resolvedCount>=5,{},{timeout:15000});
  requireTrue(await page.evaluate(()=>SEAL_GAME.judge.perfect===5&&SEAL_GAME.judge.stray===0),'All five opening nods score without stray input');
  await page.locator('#pause').tap();const paused=await page.evaluate(()=>SEAL_GAME.time);await page.waitForTimeout(250);
  requireTrue(await page.evaluate(value=>SEAL_GAME.state==='paused'&&SEAL_GAME.time===value,paused),'Audio clock freezes while paused');
  await page.locator('#resume').tap();
  await page.waitForFunction(()=>SEAL_GAME.state==='results',{},{timeout:20000});
  const practice=await page.evaluate(()=>SEAL_GAME.snapshot());
  requireTrue(practice.perfect===23&&practice.miss===0&&practice.stray===0,'Practice scores 23/23 through physical touch buttons');
  await page.screenshot({path:path.join(folder,'h5-practice-results.png')});
  await page.locator('#back-results').tap();await page.locator('#start').tap();await installAutoplay();
  await page.waitForFunction(()=>SEAL_GAME.state==='playing'&&SEAL_GAME.time>12.8,{},{timeout:20000});
  requireTrue(await page.evaluate(()=>SEAL_GAME.dialogue==='准备，翻滚一次！'),'Variation instruction timed to audio');
  await page.screenshot({path:path.join(folder,'h5-playing-android.png')});
  fs.writeFileSync(path.join(folder,'h5-browser-progress.txt'),'RUNNING full song; offline file, phone layouts, embedded audio, first five Down inputs, pause/resume and 23/23 practice passed.');
  await page.waitForFunction(()=>SEAL_GAME.state==='results',{},{timeout:110000});
  const full=await page.evaluate(()=>SEAL_GAME.snapshot());
  requireTrue(full.total===235&&full.perfect===235&&full.good===0&&full.miss===0&&full.stray===0,'Full song scores 235/235 through touch buttons');
  requireTrue(full.captain.beatCueCount===235,'Captain receives 235 cues exactly once');
  await page.screenshot({path:path.join(folder,'h5-full-results.png')});
  requireTrue(errors.length===0,'No browser runtime errors: '+errors.join('\n'));
  requireTrue(network.length===0,'No HTTP(S) requests');
  const report={pass:true,mode:'offline file://, Chromium Android touch emulation',layouts,audio,practice,full,network,errors};
  fs.writeFileSync(path.join(folder,'h5-browser-results.json'),JSON.stringify(report,null,2));
  fs.writeFileSync(path.join(folder,'h5-browser-progress.txt'),'PASS: offline file, phone layouts, audio decode, first five nods, pause/resume, practice 23/23 and full 235/235 through touch buttons; zero network requests or errors.');
  console.log('PASS: file:// offline, mobile layouts, 23/23 practice, 235/235 full, no requests/errors');
  await browser.close();
})().catch(async error=>{console.error(error);fs.writeFileSync(path.join(folder,'h5-browser-progress.txt'),'FAIL: '+error.stack);if(browser)await browser.close();process.exitCode=1;});
