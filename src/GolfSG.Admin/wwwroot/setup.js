(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const root = $('setup-editor');
  if (!root) return;
  const preview = root.dataset.preview === 'true';
  const svg = $('course-map'), ns = 'http://www.w3.org/2000/svg';
  let courseId = crypto.randomUUID(), gameId = crypto.randomUUID(), shots = [], editing = null;
  let from = null, target = null, mode = 'from', calibration = [], metres = 0, aspect = 1;
  let imageBytes = '', imageFile = '', imageUrl = '', busy = false, dirty = false, dragging = null;
  let loadedCourses = [], lastGame = null, scaleNote = '';
  function status(message, error = false) { $('setup-status').textContent = message; $('setup-status').dataset.error = String(error); }
  const pointValid = p => p && Number.isFinite(p.x) && Number.isFinite(p.y) && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1;
  const clamp = n => Math.max(0, Math.min(1, n));
  const length = (a,b) => Math.hypot(a.x-b.x,(a.y-b.y)/aspect);
  const distance = shot => shot.measuredDistanceMeters ?? length(shot.from, shot.target) * metres;
  function changed() { dirty = true; }
  function syncCoordinates() {
    for (const [key,p] of [['from',from],['target',target]])
      for (const axis of ['x','y']) $(key+'-'+axis).value = p ? (p[axis]*100).toFixed(1) : '';
    const measured = Number($('measured').value);
    $('shot-distance').textContent = from && target ? (measured > 0 ? `${measured.toFixed(1)} m measured` : metres > 0 ? `${(length(from,target)*metres).toFixed(1)} m estimated` : 'Calibrate the picture or enter a measured distance.') : 'Place both markers to see the distance.';
  }
  function element(tag, attrs) { const e = document.createElementNS(ns,tag); for (const [k,v] of Object.entries(attrs)) e.setAttribute(k,String(v)); return e; }
  function drawPair(group,a,b,color,active = false) {
    const h = 1000/aspect;
    if (a && b) group.append(element('line',{x1:a.x*1000,y1:a.y*h,x2:b.x*1000,y2:b.y*h,stroke:color,'stroke-width':active?5:3,opacity:active?1:.5}));
    for (const [key,p] of [['from',a],['target',b]]) if (p) {
      const circle = element('circle',{cx:p.x*1000,cy:p.y*h,r:active?14:8,fill:key==='from'?color:'#eab04b',stroke:'white','stroke-width':3});
      if (active) {
        circle.dataset.marker = key; circle.classList.add('map-point'); circle.setAttribute('tabindex','0'); circle.setAttribute('role','button'); circle.setAttribute('aria-label',`${key === 'from' ? 'Start' : 'Target'} marker. Use arrow keys to move.`);
        circle.addEventListener('keydown',event => {
          if (!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(event.key)) return;
          event.preventDefault(); const step=event.shiftKey?.01:.001;
          move(key,{x:clamp(p.x+(event.key==='ArrowRight'?step:event.key==='ArrowLeft'?-step:0)),y:clamp(p.y+(event.key==='ArrowDown'?step:event.key==='ArrowUp'?-step:0))});
          svg.querySelector(`[data-marker="${key}"]`)?.focus();
        });
      }
      group.append(circle);
    }
  }
  function draw() {
    const group=$('map-shots'); group.replaceChildren();
    for (const shot of shots) if (shot.id!==editing) drawPair(group,shot.from,shot.target,'#48746b');
    drawPair(group,from,target,'#16764b',true);
    if (calibration.length) drawPair(group,calibration[0],calibration[1],'#724bb9');
    syncCoordinates();
  }
  function move(key,p) { if (busy) return; if (key==='from') from=p; else target=p; $('measured').value=''; changed(); draw(); }
  function mapPoint(event) { const rect=svg.getBoundingClientRect(); return {x:clamp((event.clientX-rect.left)/rect.width),y:clamp((event.clientY-rect.top)/rect.height)}; }
  svg.addEventListener('pointerdown',event => {
    if (!imageBytes || busy) return;
    if (event.target.dataset.marker) { dragging=event.target.dataset.marker; svg.setPointerCapture(event.pointerId); event.preventDefault(); return; }
    if (mode==='calibration') { if (calibration.length===2) calibration=[]; calibration.push(mapPoint(event)); draw(); $('map-help').textContent=calibration.length===1?'Click the second reference point.':'Enter the known distance, then Apply calibration.'; }
    else { move(mode,mapPoint(event)); if(mode==='from') mode='target'; }
  });
  svg.addEventListener('pointermove',event => { if(dragging) move(dragging,mapPoint(event)); });
  svg.addEventListener('pointerup',() => { dragging=null; });
  svg.addEventListener('pointercancel',() => { dragging=null; });
  $('place-start').onclick=()=>{mode='from';$('map-help').textContent='Click the start position (green).';};
  $('place-target').onclick=()=>{mode='target';$('map-help').textContent='Click the target (gold).';};
  $('calibrate').onclick=()=>{mode='calibration';calibration=[];draw();$('map-help').textContent='Click two reference points whose distance you know.';};
  $('apply-scale').onclick=()=>{
    const known=Number($('reference-distance').value);
    if(calibration.length!==2 || !Number.isFinite(known) || known<=0 || length(...calibration)<.0001) return status('Choose two different reference points and enter their distance.',true);
    const scale=known/length(...calibration);
    if(scale>10000) return status('This calibration exceeds the supported picture scale. Use reference points farther apart.',true);
    metres=scale;scaleNote=`Calibrated from a ${known} m reference`;$('scale-status').textContent=scaleNote;calibration=[];mode='from';changed();renderList();status('Picture calibrated. Add your shots.');
  };
  function setImage(bytes,file) {
    return new Promise((resolve,reject)=>{
      const binary=atob(bytes),data=Uint8Array.from(binary,c=>c.charCodeAt(0));
      const url=URL.createObjectURL(new Blob([data],{type:file.endsWith('.png')?'image/png':'image/jpeg'}));
      const img=new Image();img.onload=()=>{
        if(imageUrl)URL.revokeObjectURL(imageUrl);imageUrl=url;imageBytes=bytes;imageFile=file;aspect=img.naturalWidth/img.naturalHeight;
        if(!Number.isFinite(aspect)||aspect<=0){reject(new Error('Invalid picture dimensions.'));return;}
        svg.setAttribute('viewBox',`0 0 1000 ${1000/aspect}`);$('map-picture').setAttribute('height',1000/aspect);$('map-picture').setAttribute('href',url);$('picture-empty').hidden=true;draw();resolve();
      };img.onerror=()=>{URL.revokeObjectURL(url);reject(new Error('This picture cannot be opened. Choose a JPEG or PNG.'));};img.src=url;
    });
  }
  $('picture').onchange=async event=>{
    const file=event.target.files[0];if(!file)return;
    if(!['image/png','image/jpeg'].includes(file.type)||file.size>10*1024*1024) return status('Choose a JPEG or PNG picture up to 10 MB.',true);
    if(shots.length&&!confirm('Replacing the picture clears the existing shots and calibration. Continue?'))return;
    try {const reader=new FileReader();const bytes=await new Promise((resolve,reject)=>{reader.onload=()=>resolve(String(reader.result).split(',')[1]);reader.onerror=reject;reader.readAsDataURL(file);});
      await setImage(bytes,file.type==='image/png'?'course.png':'course.jpg');shots=[];metres=0;calibration=[];courseId=crypto.randomUUID();gameId=crypto.randomUUID();lastGame=null;scaleNote='';clearShot();renderList();changed();$('scale-status').textContent='Calibrate using two known points.';status('Picture ready. Calibrate, then place start and target.');
    }catch(error){status(error.message,true);}
  };
  function categoryLie() { const category=Number($('category').value); for(const option of $('lie').options) option.disabled=category===0?option.value!=='2':option.value==='2'||(category===2&&option.value==='0'); if($('lie').selectedOptions[0].disabled)$('lie').value=category===0?'2':'1'; }
  $('category').onchange=()=>{categoryLie();changed();};categoryLie();
  for(const id of ['shot-name','lie','attempts','course-name','game-name','share-confirm'])$(id).addEventListener('input',changed);
  $('measured').oninput=()=>{changed();syncCoordinates();};
  for(const key of ['from','target'])for(const axis of ['x','y'])$(key+'-'+axis).oninput=()=>{
    const x=Number($(key+'-x').value)/100,y=Number($(key+'-y').value)/100;
    if($(key+'-x').value!==''&&$(key+'-y').value!==''&&pointValid({x,y}))move(key,{x,y});
  };
  function clearShot(){editing=null;from=null;target=null;mode='from';$('shot-name').value='';$('measured').value='';$('attempts').value='1';$('save-shot').textContent='Add shot';$('shot-name').placeholder=`Shot ${shots.length+1}`;draw();}
  $('new-shot').onclick=()=>{if(!busy)clearShot();};
  function commitShot(){
    if(!imageBytes||!pointValid(from)||!pointValid(target))throw new Error('Choose a picture and place both start and target.');
    const attempts=Number($('attempts').value), measured=$('measured').value===''?null:Number($('measured').value);
    if(!Number.isInteger(attempts)||attempts<1||attempts>100)throw new Error('Attempts must be a whole number between 1 and 100.');
    if(measured!==null&&(!Number.isFinite(measured)||measured<.1||measured>1000))throw new Error('Measured distance must be 0.1–1000 metres.');
    const shot={id:editing||crypto.randomUUID(),name:$('shot-name').value.trim()||`Shot ${shots.length+1}`,from:{...from},target:{...target},lie:Number($('lie').value),category:Number($('category').value),measuredDistanceMeters:measured,attempts};
    const d=distance(shot);if(!Number.isFinite(d)||d<.1||d>1000)throw new Error('Calibrate the picture or enter a measured distance of 0.1–1000 metres.');
    const next=shots.filter(s=>s.id!==shot.id);next.push(shot);
    if(next.length>100||next.reduce((sum,s)=>sum+s.attempts,0)>1000)throw new Error('A course supports up to 100 positions and 1000 attempts.');
    const index=shots.findIndex(s=>s.id===shot.id);if(index<0)shots.push(shot);else shots[index]=shot;changed();renderList();clearShot();
  }
  $('save-shot').onclick=()=>{try{commitShot();status('Shot saved in this setup. Add another or save the course.');}catch(error){status(error.message,true);}};
  function renderList(){
    $('shot-list').replaceChildren();$('shot-count').textContent=`${shots.length} positions · ${shots.reduce((sum,s)=>sum+s.attempts,0)} attempts`;
    for(const shot of shots){const li=document.createElement('li'),text=document.createElement('span');text.textContent=`${shot.name} · ${['Putting','Approach','Around green'][shot.category]} · ${distance(shot).toFixed(1)} m · ${shot.attempts} attempts`;li.append(text);
      for(const [label,action] of [['Edit',()=>{editing=shot.id;from={...shot.from};target={...shot.target};$('shot-name').value=shot.name;$('category').value=shot.category;categoryLie();$('lie').value=shot.lie;$('attempts').value=shot.attempts;$('measured').value=shot.measuredDistanceMeters??'';$('save-shot').textContent='Update shot';draw();}],['Delete',()=>{shots=shots.filter(s=>s.id!==shot.id);if(editing===shot.id)clearShot();changed();renderList();draw();}]]){const button=document.createElement('button');button.type='button';button.textContent=label;button.onclick=()=>{if(!busy)action();};li.append(button);}$('shot-list').append(li);}
    draw();
  }
  function packageData(){
    if(from||target)throw new Error('Add or update the current shot before saving, or choose New shot to discard it.');
    const name=$('course-name').value.trim();if(!name||!imageBytes||shots.length===0)throw new Error('Add a course name, a picture and at least one shot.');
    if(metres<=0)throw new Error('Calibrate the picture before saving your course.');
    if(shots.some(s=>distance(s)<.1||distance(s)>1000))throw new Error('Calibration puts a shot outside 0.1–1000 m. Review the shots.');
    return {course:{id:courseId,name,imageFile,imageAspectRatio:aspect,metresPerImageWidth:metres,scaleNote,shots},image:imageBytes};
  }
  async function request(path,body){
    const options=body?{method:'POST',headers:{'Content-Type':'application/json','RequestVerificationToken':$('setup-token').querySelector('input').value},body:JSON.stringify(body)}:{};
    const response=await fetch(path,options);if(response.redirected)throw new Error('Please sign in again in another tab, then return to save this setup.');
    const json=await response.json().catch(()=>({}));if(!response.ok)throw new Error(json.message||'The request failed. Check your sign-in and try again.');return json;
  }
  function setBusy(value){busy=value;for(const node of root.querySelectorAll('button,input,select'))node.disabled=value;if(!value)categoryLie();}
  async function save(game){
    if(busy)return;
    try{const pack=packageData();const gameName=$('game-name').value.trim();if(game&&(!gameName||!$('share-confirm').checked))throw new Error('Name the game and confirm sharing the picture and shot setup.');
      const fingerprint=game?JSON.stringify({name:gameName,package:pack}):'';
      if(game&&lastGame===fingerprint){status('This game has already been created. Open My games to view it.');return;}
      if(game&&lastGame&&lastGame!==fingerprint)gameId=crypto.randomUUID();
      setBusy(true);status(game?'Creating game…':'Saving private course…');
      if(preview){status(game?'Sample game created · join code DEMO42. No real game was saved.':'Preview saved. No real course was saved.');dirty=false;return;}
      const result=await request(game?'/my/setup/game':'/my/setup/course',game?{id:gameId,name:gameName,package:pack}:{package:pack});
      dirty=false;if(game){lastGame=fingerprint;status(`Game created. Join code: ${result.code}\nShare the code with players using the GolfSG app.`);$('game-result').hidden=false;}else{status('Private course saved. It is available in My courses and the app’s owned course library.');await loadCourses();}
    }catch(error){status(error.message||'Saving failed. Your setup is still on this page.',true);}finally{setBusy(false);}
  }
  $('save-course').onclick=()=>save(false);$('create-game').onclick=()=>save(true);
  async function loadCourses(){if(preview)return;loadedCourses=await request('/my/setup/courses');const select=$('saved-course');select.replaceChildren(new Option('Choose a saved course',''));for(const course of loadedCourses)select.add(new Option(course.name,course.id));}
  if(!preview){$('load-course').onclick=async()=>{
    const selected=loadedCourses.find(c=>c.id===$('saved-course').value);if(!selected)return status('Choose a saved course first.',true);
    if(dirty&&!confirm('Replace the current unsaved setup?'))return;
    try{setBusy(true);const pack=await request(`/my/setup/courses/${selected.id}/${selected.revisionId}`);await setImage(pack.image,pack.course.imageFile);const course=pack.course;courseId=course.id;shots=course.shots;metres=course.metresPerImageWidth;scaleNote=course.scaleNote;$('course-name').value=course.name;$('scale-status').textContent=scaleNote;gameId=crypto.randomUUID();lastGame=null;calibration=[];clearShot();renderList();dirty=false;status('Course loaded. Edit shots or create a game from this setup.');}catch(error){status(error.message,true);}finally{setBusy(false);}
    };loadCourses().catch(error=>status(error.message,true));}
  window.addEventListener('beforeunload',event=>{if(dirty){event.preventDefault();event.returnValue='';}});
})();
