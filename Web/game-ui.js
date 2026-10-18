/* Canvas artwork and touch UI. This file only observes SealGame's audio clock. */
(function () {
  'use strict';
  const $ = id => document.getElementById(id);
  const app = $('game'), canvas = $('scene'), context = canvas.getContext('2d', {alpha:false});
  const data = window.SEAL_DATA;
  const game = new window.SealGame(data);
  window.SEAL_GAME = game;
  const ink = '#233e49', coral = '#c96956';
  const font = '-apple-system,BlinkMacSystemFont,"PingFang SC","Microsoft YaHei",sans-serif';
  const assets = {}, icons = {};
  let artReady = false, assetError = '', layout = null, lastState = '', lastJudgement = null, judgementAt = -100;
  const controls = Array.from(document.querySelectorAll('.action'));
  const panels = ['ready','paused','results','error'];
  const clamp = (v,a,b) => Math.min(b,Math.max(a,v));
  const smooth = t => { t = clamp(t,0,1); return t*t*(3-2*t); };
  const setText = (id,text) => { const node = $(id); text = String(text); if (node.textContent !== text) node.textContent = text; };
  const clockLabel = time => { time = Math.max(0,Math.floor(time || 0)); return Math.floor(time/60)+':'+String(time%60).padStart(2,'0'); };
  const scoreLabel = score => String(Math.max(0,score || 0)).padStart(6,'0');
  const show = (id,value) => { const node=$(id); if (node.hidden === value) node.hidden=!value; };

  function crop(image, rect) {
    const result = document.createElement('canvas');
    result.width = Math.ceil(rect[2]); result.height = Math.ceil(rect[3]);
    const draw = result.getContext('2d');
    draw.drawImage(image,rect[0],rect[1],rect[2],rect[3],0,0,result.width,result.height);
    return result;
  }
  function loadImage(name) {
    return new Promise((resolve,reject) => {
      const image = new Image();
      image.onload = () => { assets[name]=image; resolve(image); };
      image.onerror = () => reject(new Error('游戏图片未能打开，请重新打开游戏文件。'));
      image.src = data.assets[name];
    });
  }
  window.__sealReady = Promise.all(['background','seals','captain','ui','beatBar'].map(loadImage)).then(() => {
    for (const name of ['seal','ring']) icons[name]=crop(assets.ui,data.spriteRects.ui[name]);
    icons.beatBar=crop(assets.beatBar,data.spriteRects.beatBar);
    for (const image of document.querySelectorAll('img[data-icon]')) {
      const icon=icons[image.dataset.icon];
      if (!icon) continue;
      image.src=icon.toDataURL(); image.style.display='block';
    }
    artReady=true; updateDom(); resize(); return true;
  }).catch(error => { assetError=error.message; updateDom(); return false; });

  function resize() {
    const bounds=app.getBoundingClientRect();
    const dpr=Math.min(window.devicePixelRatio || 1,2);
    canvas.width=Math.round(bounds.width*dpr); canvas.height=Math.round(bounds.height*dpr);
    context.setTransform(dpr,0,0,dpr,0,0);
    const header=$('header').getBoundingClientRect();
    const controlsBounds=$('controls').getBoundingClientRect();
    const top=header.bottom-bounds.top, bottom=controlsBounds.top-bounds.top-13;
    const stageHeight=Math.max(70,bottom-top), portrait=bounds.height>bounds.width;
    const unit=Math.min(bounds.width/5.3,stageHeight*.49,176);
    const ground=top+stageHeight*(portrait?.80:.94);
    layout={width:bounds.width,height:bounds.height,header:top,stageBottom:bottom,stageHeight,unit,ground,
      captainX:bounds.width*.5-unit*.2,captainGround:ground-unit*.26,
      laneY:(header.height-(parseFloat(getComputedStyle($('header')).paddingTop)||0))*.65+(parseFloat(getComputedStyle($('header')).paddingTop)||0),
      laneExtent:Math.min(300,bounds.width*.41),portrait};
    positionDialogue();
    window.SEAL_UI_LAYOUT={width:layout.width,height:layout.height,header:top,controlsTop:controlsBounds.top-bounds.top,controlsHeight:controlsBounds.height,unit};
  }
  function positionDialogue() {
    if (!layout) return;
    const bubble=$('dialogue'), {width,height,unit,captainX,captainGround,portrait,header,stageHeight}=layout;
    const compact=height<500 && !portrait;
    const size=portrait?20:compact?17:22;
    let bubbleWidth=Math.min(330,portrait?width-52:Math.max(190,width*.29));
    let left=captainX+unit*.65+14;
    let top=captainGround-unit*1.48;
    let stacked=portrait;
    if (portrait) { left=(width-bubbleWidth)/2; top=header+Math.max(19,stageHeight*.10); }
    if (game.state==='ready') {
      const card=$('ready').getBoundingClientRect(), appRect=app.getBoundingClientRect();
      const spare=width-(card.right-appRect.left)-20;
      if (spare>=220) { bubbleWidth=Math.min(310,spare-12); left=card.right-appRect.left+16; top=Math.max(header+8,card.top-appRect.top+15); stacked=false; }
      else {
        bubbleWidth=Math.min(400,width-40); left=(width-bubbleWidth)/2;
        // On short landscape screens the instruction sits in the spare strip above the controls.
        top=card.bottom-appRect.top+12; stacked=true;
        if (top+size*2.8+24>layout.stageBottom) top=Math.max(header+4,card.top-appRect.top-size*2.8-24);
      }
    }
    left=clamp(left,18,width-bubbleWidth-18);
    bubble.style.width=bubbleWidth+'px'; bubble.style.left=left+'px'; bubble.style.top=top+'px';
    bubble.style.fontSize=size+'px';
    bubble.classList.toggle('stacked',stacked);
    bubble.classList.toggle('in-front',game.state==='ready');
  }

  function updateDom() {
    const state=game.state, judge=game.judge;
    const errorState=!!assetError || state==='error';
    for (const id of panels) show(id,errorState?id==='error':id===state);
    show('shade',errorState || state==='ready' || state==='paused' || state==='results');
    setText('score',scoreLabel(judge.score)); setText('combo',judge.combo+' 连击');
    const liveDenominator=judge.resolvedCount+judge.stray*.25;
    setText('accuracy',liveDenominator?((judge.perfect+judge.good*.65)/liveDenominator*100).toFixed(1)+'%':'—');
    setText('time',clockLabel(game.time)+' / '+clockLabel(game.duration));
    $('pause').disabled=!(state==='playing'||state==='countin') || game.loading;
    $('start').disabled=$('practice').disabled=!artReady||game.loading;
    $('resume').disabled=$('restart-paused').disabled=$('replay').disabled=game.loading;
    setText('loading',game.loading?'准备音乐…':!artReady?'准备中…':'');
    setText('assist','节拍强调　'+(game.assist?'开':'关')); $('assist').setAttribute('aria-pressed',String(game.assist));
    setText('offset','校准 '+game.offset+' ms');
    setText('pause-error',game.error||''); show('pause-error',state==='paused'&&!!game.error);
    if (errorState) setText('error-message',assetError||game.error||'游戏暂时无法开始，请重试。');
    for (const button of controls) button.disabled=!artReady||game.loading||!['ready','playing'].includes(state);
    const dialogue=!errorState && ['ready','countin','playing'].includes(state)?game.dialogue:'';
    setText('dialogue',dialogue); show('dialogue',!!dialogue);
    if (state==='results') {
      setText('results-title',game.practice?'练习完成':judge.miss===0&&judge.stray===0?'完美合拍':'演出完成');
      setText('result-score',scoreLabel(judge.score));
      for(const key of ['perfect','good','miss','stray']) setText('result-'+key,judge[key]);
      setText('result-detail','最大连击 '+judge.maxCombo+'　·　准确率 '+(judge.accuracy*100).toFixed(1)+'%'+(game.practice?'':'　·　最佳 '+scoreLabel(game.best)));
    }
    if (lastState!==state) { lastState=state; positionDialogue(); }
    if (game.judgement!==lastJudgement) { lastJudgement=game.judgement; judgementAt=performance.now()/1000; }
  }

  function drawCover(image,width,height) {
    const scale=Math.max(width/image.width,height/image.height);
    const w=image.width*scale,h=image.height*scale;
    context.drawImage(image,(width-w)*.5,(height-h)*.5,w,h);
  }
  function sprite(image,rect,x,y,w,h) { context.drawImage(image,rect[0],rect[1],rect[2],rect[3],x,y,w,h); }
  function renderSeal(actor,index) {
    const unit=layout.unit, duration=Math.max(1,(actor.duration || 1)*60), frame=clamp(actor.frame || 0,0,duration);
    const motion=actor.motion; let pose=0,rotation=actor.upside?180:0,x=0,lift=0,sx=1,sy=1;
    if (motion===1||motion===12) {
      if(frame<9) { pose=1; const press=Math.sin(Math.PI*frame/9); sx+=.025*press; sy-=.035*press; }
    } else if ([2,3,13,14].includes(motion)) {
      const direction=motion===2||motion===13?-1:1;
      if(frame<9) pose=1; else if(frame<42) {pose=direction<0?2:3;rotation-=direction*3;}
    } else if ([4,5,15,16].includes(motion)) {
      const direction=motion===4||motion===15?-1:1;
      if(frame>=3&&frame<38){pose=direction<0?2:3;const rock=Math.sin(Math.PI*(frame-3)/35);rotation-=direction*5*rock;x=direction*.045*rock;lift=.018*rock;}
    } else if ([8,9,19,20].includes(motion)) {
      const direction=motion===8||motion===19?1:-1;
      if(frame<9){const t=smooth(frame/9);rotation=(actor.upside?0:180)-direction*180*t;x=direction*.10*Math.sin(Math.PI*t);lift=.08*Math.sin(Math.PI*t);sx=sy=1-.025*Math.sin(Math.PI*t);}
      else if(frame<duration-8){pose=direction<0?2:3;const settle=Math.sin(Math.PI*(frame-9)/Math.max(1,duration-17));rotation-=direction*3*settle;}
    }
    const rect=data.spriteRects.sealFrames[actor.isPlayer?'player':'partner'][pose];
    const pixel=unit/341,px=layout.width*.5+(index-1.5)*unit*1.16+x*280*pixel;
    const centre=layout.ground-156.8*pixel-lift*280*pixel;
    context.save();context.translate(px,centre);context.rotate(-rotation*Math.PI/180);context.scale(sx,sy);
    sprite(assets.seals,rect,-rect[2]*pixel*.5,-(rect[3]-156.8)*pixel,rect[2]*pixel,rect[3]*pixel);
    context.restore();
  }
  function renderCaptain() {
    const captain=game.captain,rect=data.spriteRects.captainFrames[captain.pose || 0];
    const pixel=layout.unit*1.65/422;
    const elapsed=game.time-captain.startTime;
    const wave=captain.startTime>=0&&elapsed>=0&&elapsed<.2?Math.sin(elapsed/.2*Math.PI):0;
    const bob=wave*(captain.input===0?-.025:.018)*148*pixel;
    const squash=captain.input===0?1-.015*wave:1;
    sprite(assets.captain,rect,layout.captainX-rect[2]*pixel*.5,layout.captainGround-rect[3]*pixel*squash-bob,rect[2]*pixel,rect[3]*pixel*squash);
  }
  function renderLane() {
    const {width,header,laneY,laneExtent}=layout;
    context.fillStyle='rgba(252,248,238,.97)';context.fillRect(0,0,width,header);
    context.strokeStyle='rgba(35,62,73,.2)';context.lineWidth=1;
    context.beginPath();context.moveTo(width*.5-laneExtent,laneY);context.lineTo(width*.5+laneExtent,laneY);context.stroke();
    const active=['ready','countin','playing'].includes(game.state);
    if(active){
      const lookAhead=game.beatDuration*3;
      const adjusted=game.time-game.offset/1000;
      const barWidth=layout.width<600?14:16,barHeight=layout.width<600?44:48;
      for(let i=game.judge.nextNoteIndex;i<game.notes.length;i++) {
        const note=game.notes[i],delta=note.time-adjusted;
        if(delta>lookAhead) break;
        if(game.judge.resolved[i]) continue;
        const fraction=clamp(delta/lookAhead,0,1),distance=fraction*laneExtent;
        context.globalAlpha=1-smooth((fraction-.88)/.12);
        context.drawImage(icons.beatBar,width*.5-distance-barWidth*.5,laneY-barHeight*.5,barWidth,barHeight);
        context.drawImage(icons.beatBar,width*.5+distance-barWidth*.5,laneY-barHeight*.5,barWidth,barHeight);
      }
    }
    context.globalAlpha=1;
    const beat=game.time/game.beatDuration,phase=beat-Math.floor(beat),pulse=1-smooth(phase/.36);
    if(game.assist&&active&&game.state!=='ready'){
      const ringSize=58*(1+.22*(1-pulse));context.globalAlpha=.6*pulse;
      context.drawImage(icons.ring,width*.5-ringSize*.5,laneY-ringSize*.5,ringSize,ringSize);context.globalAlpha=1;
    }
    const centrePulse=1+(game.assist?.055:.02)*pulse;
    const centreSize=(layout.width<600?42:46)*centrePulse;
    context.drawImage(icons.seal,width*.5-centreSize*.5,laneY-centreSize*.5,centreSize,centreSize);
    if(game.judgement&&active&&game.state==='playing') {
      const age=performance.now()/1000-judgementAt;
      if(age>=0&&age<.5){
        context.globalAlpha=1-smooth((age-.28)/.22);context.fillStyle=game.judgement.success?ink:coral;
        context.font='650 '+(layout.width<600?14:16)+'px '+font;context.textAlign='center';context.textBaseline='middle';
        context.fillText(game.judgement.label,width*.5,laneY+32-age*8);context.globalAlpha=1;
      }
    }
    context.fillStyle='rgba(35,62,73,.12)';context.fillRect(width*.03,header-7,width*.94,1);
    context.fillStyle=coral;context.fillRect(width*.03,header-7,width*.94*clamp(game.progress,0,1),1.5);
  }
  function render() {
    if(!layout) return;
    const {width,height}=layout;
    context.clearRect(0,0,width,height);
    context.fillStyle='#e5f0ef';context.fillRect(0,0,width,height);
    if(!artReady) return;
    // The stage image covers the complete viewport, including the large touch controls.
    drawCover(assets.background,width,height);
    for(let i=0;i<4;i++){
      context.fillStyle='rgba(35,62,73,.08)';context.beginPath();
      context.ellipse(width*.5+(i-1.5)*layout.unit*1.16,layout.ground,layout.unit*.39,layout.unit*.045,0,0,Math.PI*2);context.fill();
    }
    renderCaptain();game.actors.forEach(renderSeal);renderLane();
    if(game.state==='countin'){
      const x=layout.width*.5,y=layout.header+layout.stageHeight*.36,r=Math.min(38,layout.stageHeight*.18);
      context.fillStyle='rgba(252,248,238,.96)';context.strokeStyle=ink;context.lineWidth=2;
      context.beginPath();context.arc(x,y,r,0,Math.PI*2);context.fill();context.stroke();
      context.fillStyle=ink;context.font='600 '+(r*1.15)+'px '+font;context.textAlign='center';context.textBaseline='middle';context.fillText(game.countdown,x,y+1);
    }
  }

  function invoke(action) { try { const promise=action(); if(promise&&promise.catch) promise.catch(error=>{game.error=error.message;game.state='error';updateDom();}); } catch(error){game.error=error.message;game.state='error';updateDom();} }
  $('start').addEventListener('click',()=>invoke(()=>game.start(false)));
  $('practice').addEventListener('click',()=>invoke(()=>game.start(true)));
  $('pause').addEventListener('click',()=>invoke(()=>game.togglePause()));
  $('resume').addEventListener('click',()=>invoke(()=>game.resume()));
  for(const id of ['restart-paused','replay']) $(id).addEventListener('click',()=>invoke(()=>game.restart()));
  for(const id of ['back-paused','back-results']) $(id).addEventListener('click',()=>game.back());
  $('assist').addEventListener('click',()=>game.toggleAssist());
  $('offset-minus').addEventListener('click',()=>game.setOffset(game.offset-10));
  $('offset-plus').addEventListener('click',()=>game.setOffset(game.offset+10));
  $('retry').addEventListener('click',()=>{if(assetError)location.reload();else game.back();});
  for(const button of controls){
    button.addEventListener('pointerdown',event=>{
      if(event.button!==0||button.disabled) return;
      event.preventDefault();
      // Capture can fail for synthetic or already-ended pointers. The press still counts.
      if(typeof button.setPointerCapture==='function') {try{button.setPointerCapture(event.pointerId);}catch(_){}}
      button.classList.add('pressed');
      game.pressBeat();
    },{passive:false});
    for(const type of ['pointerup','pointercancel','lostpointercapture']) button.addEventListener(type,()=>button.classList.remove('pressed'));
    // Pointer input is submitted on down only. No synthetic click can submit it twice.
    button.addEventListener('click',event=>event.preventDefault());
  }
  window.addEventListener('keydown',event=>{
    if(event.altKey||event.ctrlKey||event.metaKey) return;
    if(event.code==='Space'){
      event.preventDefault();if(event.repeat||!artReady) return;
      if(controls[0].disabled) return;
      game.pressBeat();controls[0].classList.add('pressed');
    } else if(event.code==='Escape') {event.preventDefault();if(!event.repeat)invoke(()=>game.togglePause());}
    else if(event.code==='Enter'&&!event.repeat&&event.target===document.body){
      event.preventDefault();if(game.state==='ready'&&artReady)invoke(()=>game.start(false));else if(game.state==='paused')invoke(()=>game.resume());
    }
  },{passive:false});
  window.addEventListener('keyup',event=>{if(event.code==='Space')controls[0].classList.remove('pressed');});
  window.addEventListener('blur',()=>controls.forEach(button=>button.classList.remove('pressed')));
  window.addEventListener('resize',resize);
  if(window.visualViewport)window.visualViewport.addEventListener('resize',resize);
  game.subscribe(updateDom);
  resize();updateDom();
  function frame(){game.tick();updateDom();render();requestAnimationFrame(frame);}
  requestAnimationFrame(frame);
})();
