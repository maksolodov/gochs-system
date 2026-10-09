const {chromium}=require('playwright');
const fs=require('fs'),path=require('path'),assert=require('assert'),net=require('net'),crypto=require('crypto'),{spawn,execFileSync}=require('child_process');
(async()=>{
 const root=path.resolve(__dirname,'..');fs.mkdirSync(path.join(root,'test-results'),{recursive:true});
 const data=fs.mkdtempSync(path.join(root,'test-results','ui-')),build=path.join(data,'build');
 const localDotnet=[path.join(root,'.runtime/dotnet/dotnet.exe'),path.join(root,'../.runtime/dotnet/dotnet.exe')].find(p=>fs.existsSync(p));
 const dotnet=process.env.DOTNET||(localDotnet||'dotnet');
 execFileSync(dotnet,['build',path.join(root,'backend/Gochs.csproj'),'-c','Release','-o',build],{stdio:'inherit'});
 const port=await new Promise(resolve=>{const s=net.createServer();s.listen(0,'127.0.0.1',()=>{const port=s.address().port;s.close(()=>resolve(port));});});
 const base='http://127.0.0.1:'+port,password=crypto.randomBytes(24).toString('base64url');
 const logfile=fs.openSync(path.join(data,'server.log'),'a');
 function launch(){return spawn(dotnet,[path.join(build,'Gochs.dll')],{cwd:path.join(root,'backend'),env:{...process.env,DataPath:data,OperatorPassword:password,ASPNETCORE_URLS:base},stdio:['ignore',logfile,logfile]});}
 let server=launch();
 async function ready(){for(let i=0;i<100;i++){if(server.exitCode!==null)throw Error('Test server exited');try{if((await fetch(base+'/api/health')).ok)return;}catch{}await new Promise(r=>setTimeout(r,150));}throw Error('Startup timeout');}
 async function stop(){if(server.exitCode===null){const exited=new Promise(r=>server.once('exit',r));server.kill();await exited;}}
 let browser;
 try{
 await ready();
 browser=await chromium.launch({channel:process.env.BROWSER_CHANNEL||'msedge',headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1000},timezoneId:'America/New_York'});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const output=path.join(data,'screenshots');fs.mkdirSync(output,{recursive:true});
  await page.goto(base);await page.locator('#main h1').waitFor();
  await page.locator('#modal').waitFor({state:'visible'});
  await page.locator('[name=name]').fill('UI Test');
  await page.locator('[name=password]').fill(password);
  await page.locator('#edit-form button[type=submit]').click();
  await page.locator('#modal').waitFor({state:'hidden'});
  const pages=['home','employees','siz','cards','issues','writeoffs','disposals','evacuation','nfgo','units','norms','shelters','training','orders','plan','reports','wartime','analytics','settings','training-summary','change-requests','notifications','types','warehouses','contractors','vehicles','points','routes'];
  for(const section of pages){
   await page.goto(base+'/#'+section);
   await page.waitForFunction(section=>document.querySelector('#main').dataset.page===section,section);
   await page.waitForTimeout(250);
   assert(!await page.locator('#main').innerText().then(t=>t.includes('Не удалось загрузить данные')),section);
   await page.screenshot({path:path.join(output,section+'.png'),fullPage:true});
  }
  await page.goto(base+'/#employees');await page.getByRole('button',{name:'Добавить сотрудника',exact:true}).click();
  for(const [name,value] of Object.entries({personnelNumber:'UI-'+Date.now(),fullName:'Проверка Интерфейса',department:'Тест',position:'Проверяющий'}))await page.locator(`[name=${name}]`).fill(value);
  await page.locator('#edit-form button[type=submit]').click();await page.locator('#modal').waitFor({state:'hidden'});
  await page.getByText('Проверка Интерфейса',{exact:true}).last().waitFor();
  const read=async url=>(await page.request.get(base+url)).json();
  const go=async section=>{await page.goto(base+'/#'+section);await page.waitForFunction(s=>document.querySelector('#main').dataset.page===s,section);};
  const submit=async()=>{await page.locator('#edit-form button[type=submit]').click();await page.locator('#modal').waitFor({state:'hidden'});};
  for(let i=1;i<=2;i++){
   await go('siz');await page.getByRole('button',{name:'Принять партию',exact:true}).click();
   for(const [name,value] of Object.entries({batchNumber:'UI-PART-'+i,manufacturer:'Учебный завод',size:'2',shelf:'А-1',expiryDate:'2035-12-31',quantity:'3',inventoryPrefix:'UI-PPE'}))await page.locator(`[name=${name}]`).fill(value);
   await submit();
  }
  const received=(await read('/api/siz/cards')).filter(c=>c.inventoryNumber.startsWith('UI-PPE-'));assert.strictEqual(received.length,6);
  await page.getByRole('button',{name:'Документы',exact:true}).first().click();
  await page.locator('#upload').setInputFiles({name:'bad.exe',mimeType:'image/png',buffer:Buffer.from('MZ invalid')});
  await page.getByRole('button',{name:'Загрузить',exact:true}).click();await page.locator('#toast').filter({hasText:'Расширение'}).waitFor();
  const image=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=','base64');
  await page.locator('#upload').setInputFiles({name:'test.png',mimeType:'image/png',buffer:image});await page.getByRole('button',{name:'Загрузить',exact:true}).click();await page.getByRole('link',{name:'test.png'}).waitFor();await page.locator('#modal').evaluate(e=>e.close());
  await go('issues');await page.getByRole('button',{name:'Выбрать вручную',exact:true}).click();
  await page.locator('#issue-form [name=department]').fill('Охрана');await page.locator('#issue-form [name=department]').blur();
  await page.locator('[data-line="0"] [data-employee]').selectOption('1');await page.locator('[data-line="0"] [data-card]').selectOption(String(received[0].id));
  await page.locator('#add-line').click();await page.locator('[data-line="1"] [data-employee]').selectOption('2');await page.locator('[data-line="1"] [data-card]').selectOption(String(received[1].id));
  await page.locator('#add-line').click();await page.locator('[data-line="2"] [data-remove]').click();assert.strictEqual(await page.locator('[data-line]').count(),2);
  await page.screenshot({path:path.join(output,'issue-multiple.png')});
  await page.getByRole('button',{name:'Сохранить проект'}).click();await page.locator('#modal').waitFor({state:'hidden'});
  const statement=(await read('/api/siz/statements')).at(-1);assert.strictEqual(statement.lines.length,2);
  await page.locator('tbody tr').filter({hasText:statement.number}).getByRole('button',{name:'Изменить',exact:true}).click();
  assert.strictEqual(await page.locator('[data-line]').count(),2);await page.locator('#issue-form [name=basis]').fill('Изменено через браузер');
  await page.getByRole('button',{name:'Сохранить проект'}).click();await page.locator('#modal').waitFor({state:'hidden'});
  await page.locator('tbody tr').filter({hasText:statement.number}).getByRole('button',{name:'Подтвердить',exact:true}).click();await submit();
  assert((await read('/api/siz/cards')).filter(c=>received.slice(0,2).some(r=>r.id===c.id)).every(c=>c.status==='Issued'));
  await go('writeoffs');await page.getByRole('button',{name:'Выбрать все просроченное'}).click();await submit();
  await page.getByRole('button',{name:'Подписать',exact:true}).last().click();await submit();
  const act=(await read('/api/siz/writeoffs')).at(-1);
  await go('disposals');await page.getByRole('button',{name:'Оформить утилизацию'}).click();await page.locator('[name=writeOffActId]').selectOption(String(act.id));
  await page.locator('[name=contractNumber]').fill('UI-КОНТРАКТ');await page.locator('[name=transferDocument]').fill('UI-ПЕРЕДАЧА');await page.locator('[name=method]').fill('По учебному договору');await submit();
  const disposed=(await read('/api/siz/disposals')).at(-1);const href=await page.getByRole('link',{name:'Word',exact:true}).last().getAttribute('href');assert(href.includes('/disposal/'+disposed.id+'/'));
  const downloadPromise=page.waitForEvent('download');await page.getByRole('link',{name:'Word',exact:true}).last().click();const download=await downloadPromise;await download.saveAs(path.join(output,'disposal.docx'));
  await go('orders');await page.getByRole('button',{name:'Приказ о режиме',exact:true}).click();
  await page.locator('[name=orderNumber]').fill('UI-NOON');await page.locator('[name=effectiveFrom]').fill('2030-01-02T12:00');await submit();
  const timed=(await read('/api/wartime/orders')).find(o=>o.orderNumber==='UI-NOON');assert.strictEqual(timed.effectiveFrom,'2030-01-02T09:00:00Z');
  await page.locator('tbody tr').filter({hasText:'UI-NOON'}).getByRole('button',{name:'Изменить',exact:true}).click();assert.strictEqual(await page.locator('[name=effectiveFrom]').inputValue(),'2030-01-02T12:00');await page.locator('#cancel').click();
  const html=await (await page.request.get(base+`/api/wartime/orders/${timed.id}/document`)).text();assert(html.includes('12:00 (Москва)'));
  await page.getByRole('button',{name:'Приказ о режиме',exact:true}).click();
  await stop();server=launch();await ready();
  await page.locator('#edit-form button[type=submit]').click();await page.locator('[name=password]').waitFor();
  await page.locator('[name=password]').fill(password);await submit();await page.waitForFunction(()=>document.querySelector('#main').dataset.page==='orders');
  await page.locator('#logout').click();await page.locator('[name=password]').waitFor();assert.strictEqual((await page.request.get(base+'/api/employees')).status(),401);
  await page.locator('[name=password]').fill(password);await submit();
  await page.setViewportSize({width:390,height:844});await page.goto(base+'/#home');await page.waitForTimeout(500);
  await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});
  assert.deepStrictEqual(errors,[]);console.log(JSON.stringify({result:'PASS',pages:pages.length,employeeCreated:true,jsErrors:errors}));
 }finally{if(browser)await browser.close();await stop();fs.closeSync(logfile);}
})().catch(e=>{console.error(e);process.exit(1)});
