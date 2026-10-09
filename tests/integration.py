"""Runs against an isolated SQLite database. Python 3 standard library only."""
import concurrent.futures, datetime as dt, http.cookiejar, io, json, os
import pathlib, secrets, socket, subprocess, time, urllib.request, urllib.error, zipfile
import xml.etree.ElementTree as ET
import sqlite3, tempfile, shutil, base64

ROOT = pathlib.Path(__file__).resolve().parents[1]
DOTNET = os.environ.get('DOTNET') or next((str(p) for p in [ROOT/'.runtime/dotnet/dotnet.exe',ROOT.parent/'.runtime/dotnet/dotnet.exe'] if p.exists()),'dotnet')
(ROOT/'test-results').mkdir(exist_ok=True)
data=pathlib.Path(tempfile.mkdtemp(prefix='integration-',dir=ROOT/'test-results'))
build=data/'build'
subprocess.run([DOTNET,'build',str(ROOT/'backend/Gochs.csproj'),'-c','Release','-o',str(build)],check=True)
dll=build/'Gochs.dll'
with socket.socket() as s:
    s.bind(('127.0.0.1', 0)); port = s.getsockname()[1]
base = f'http://127.0.0.1:{port}'
password = secrets.token_urlsafe(24)
env = dict(os.environ, DataPath=str(data), OperatorPassword=password, ASPNETCORE_URLS=base, TZ="America/Los_Angeles")
log = open(data / 'server.log', 'w', encoding='utf-8')
jar = http.cookiejar.CookieJar()
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
count = 0
def call(path, method='GET', body=None, status=200, raw=False, anonymous=False):
    global count
    request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(),
                                    headers=dict({'Content-Type':'application/json'}, **({'X-CSRF-TOKEN':call('/api/auth/token')['token']} if method not in ['GET','HEAD'] and not anonymous else {})), method=method)
    try: response = (urllib.request.urlopen if anonymous else client.open)(request, timeout=20)
    except urllib.error.HTTPError as e: response = e
    payload = response.read(); actual = response.status
    assert actual == status, (method, path, actual, status, payload.decode(errors='replace')[:700])
    count += 1
    return payload if raw else (json.loads(payload) if payload else None)
def start():
    p = subprocess.Popen([DOTNET, str(dll)], cwd=ROOT/'backend', env=env, stdout=log, stderr=log)
    for _ in range(100):
        if p.poll() is not None: raise RuntimeError('Server exited; see ' + str(data/'server.log'))
        try: call('/api/health'); return p
        except (OSError, AssertionError): time.sleep(.15)
    raise RuntimeError('Server startup timeout')
def stop(p):
    p.terminate(); p.wait(timeout=15)
today = dt.datetime.now(dt.timezone(dt.timedelta(hours=3))).date()
sig = {'signedBy':'Тестовый оператор'}
p = start()
try:
    assert b'<!doctype html>' in call('/',raw=True).lower()
    call('/app.js',raw=True)
    call('/style.css',raw=True)
    call('/api/employees','POST',{},401,anonymous=True)
    call('/api/wartime/orders',status=401,anonymous=True)
    call('/api/auth/login','POST',{'name':'Проверка','password':password})
    ET.fromstring(call('/api/siz/cards/1/barcode',raw=True))
    routes=['/api/dashboard','/api/employees','/api/siz/types','/api/siz/warehouses','/api/siz/batches','/api/siz/cards',
            '/api/siz/statements','/api/siz/writeoffs','/api/siz/disposals','/api/nfgo/formations','/api/nfgo/units',
            '/api/nfgo/norms','/api/protective-structures','/api/protective-structures/inspections','/api/evacuation/vehicles',
            '/api/evacuation/points','/api/evacuation/routes','/api/evacuation/summary','/api/training/groups',
            '/api/training/sessions','/api/training/reminders','/api/reports/nfgo','/api/reports/procurement',
            '/api/notifications','/api/activities','/api/audit','/swagger/v1/swagger.json']
    for route in routes:
        call(route,status=401,anonymous=True);call(route)
    employees = sorted(call('/api/employees'),key=lambda e:e['id']); employee = employees[0]
    anonymous_paths=call('/swagger/v1/swagger.json')['paths']
    import re
    for route, methods in anonymous_paths.items():
        if 'get' in methods and route not in ['/api/auth/session','/api/auth/token','/api/health']:
            resolved=re.sub(r'\{[^}]+\}','1',route)
            call(resolved,status=401,anonymous=True)
    assert call('/api/health',anonymous=True)=={'status':'ok'}
    def send_raw(path,body,headers,expected,method='POST'):
        req=urllib.request.Request(base+path,data=body,headers=headers,method=method)
        try:r=client.open(req)
        except urllib.error.HTTPError as e:r=e
        payload=r.read();assert r.status==expected,(path,r.status,expected,payload[:300]);return payload
    send_raw('/api/siz/statements',b'{}',{'Content-Type':'application/json'},400)
    assert len(call('/api/siz/statements'))==1
    template=call('/api/siz/batches')[-1]
    before_cards=call('/api/siz/cards');before_batches=call('/api/siz/batches')
    def receipt(i):
        return call('/api/siz/batches','POST',{'batch':dict(template,id=0,batchNumber='REG-'+str(i),sizTypeId=1,size='2'),'quantity':2,'inventoryPrefix':'REG'},201)
    receipt(1);receipt(2)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(receipt,[3,4]))
    received=[c for c in call('/api/siz/cards') if c['inventoryNumber'].startswith('REG-')]
    assert sorted(c['inventoryNumber'] for c in received)==[f'REG-{i:04}' for i in range(1,9)]
    assert len(call('/api/siz/batches'))==len(before_batches)+4
    call('/api/siz/batches','POST',{'batch':dict(template,id=0,batchNumber='REG-1',sizTypeId=1,size='2'),'quantity':2,'inventoryPrefix':'REG'},409)
    assert len(call('/api/siz/cards'))==len(before_cards)+8
    multi={'department':employee['department'],'basis':'Многострочный тест','lines':[{'employeeId':employees[i]['id'],'sizCardId':received[i]['id']} for i in range(2)]}
    draft=call('/api/siz/statements','POST',multi,201)
    assert all(call('/api/siz/cards/'+str(c['id']))['status']=='InStock' for c in received[:2])
    edited=call(f"/api/siz/statements/{draft['id']}",'PUT',dict(multi,basis='Измененный проект'))
    assert len(edited['lines'])==2 and edited['basis']=='Измененный проект'
    call(f"/api/siz/statements/{draft['id']}/confirm",'POST',sig)
    call(f"/api/siz/statements/{draft['id']}/confirm",'POST',sig,409)
    assert [call('/api/siz/cards/'+str(c['id']))['employeeId'] for c in received[:2]]==[employees[0]['id'],employees[1]['id']]
    wrong=call('/api/employees','POST',{'personnelNumber':'SIZE-4','fullName':'Размер четыре','department':'Размеры','gasMaskSize':4},201)
    call('/api/siz/statements','POST',{'department':'Размеры','basis':'Проверка размера','lines':[{'employeeId':wrong['id'],'sizCardId':received[2]['id']}]},409)
    call('/api/employees/'+str(wrong['id']),'DELETE',status=204)
    def upload(filename,content,mime,expected=409,csrf=True):
        boundary='gochs-test-boundary';body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{filename}"\r\nContent-Type: {mime}\r\n\r\n').encode()+content+f'\r\n--{boundary}--\r\n'.encode()
        headers={'Content-Type':'multipart/form-data; boundary='+boundary}
        if csrf:headers['X-CSRF-TOKEN']=call('/api/auth/token')['token']
        return send_raw('/api/attachments/batch/1',body,headers,expected)
    png=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=')
    upload('bad.exe',png,'image/png');upload('bad.png',b'MZ executable','image/png');upload('bad.png',png,'application/pdf')
    upload('large.png',png+b'a'*(10*1024*1024),'image/png')
    upload('good.png',png,'image/png',400,False)
    attachment=json.loads(upload('good.png',png,'image/png',201))
    listing=call('/api/attachments?resource=batch&resourceId=1')
    assert all('content' not in x for x in listing)
    assert call('/api/attachments/'+str(attachment['id']),raw=True)==png
    call('/api/attachments/'+str(attachment['id']),status=401,anonymous=True)
    call('/api/employees','POST',dict(employee,id=0),409)
    cards=call('/api/siz/cards'); batches={b['id']:b for b in call('/api/siz/batches')}
    eligible=[c for c in cards if c['status']=='InStock' and batches[c['sizBatchId']]['expiryDate']>str(today)]
    card=eligible[0]
    request={'department':employee['department'],'basis':'Проверка выдачи','lines':[{'employeeId':employee['id'],'sizCardId':card['id']}]}
    a=call('/api/siz/statements','POST',request,201); b=call('/api/siz/statements','POST',request,201)
    call(f"/api/siz/statements/{a['id']}/confirm",'POST',sig)
    call(f"/api/siz/statements/{b['id']}/confirm",'POST',sig,409)
    call(f"/api/siz/statements/{a['id']}",'PUT',request,409)
    request['lines'][0]['sizCardId']=eligible[1]['id']
    race=[call('/api/siz/statements','POST',request,201) for _ in range(2)]
    race_token=call('/api/auth/token')['token']
    def confirm_race(order):
        cookie='; '.join(c.name+'='+c.value for c in jar)
        req=urllib.request.Request(base+f"/api/siz/statements/{order['id']}/confirm",data=json.dumps(sig).encode(),headers={'Content-Type':'application/json','Cookie':cookie,'X-CSRF-TOKEN':race_token},method='POST')
        try: return urllib.request.urlopen(req).status
        except urllib.error.HTTPError as e: return e.code
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        assert sorted(pool.map(confirm_race,race))==[200,409]
    expired=next(c for c in cards if batches[c['sizBatchId']]['expiryDate']<=str(today))
    request['lines'][0]['sizCardId']=expired['id']
    call('/api/siz/statements','POST',request,409)
    other_expired=next(c for c in cards if c['id']!=expired['id'] and batches[c['sizBatchId']]['expiryDate']<=str(today))
    call('/api/siz/writeoffs','POST',{'commission':'Резервная комиссия','reason':'Проект','cardIds':[other_expired['id']]},201)
    act=call('/api/siz/writeoffs','POST',{'commission':'Комиссия 1, 2, 3','reason':'Истек срок','cardIds':[expired['id']]},201)
    disposal={'writeOffActId':act['id'],'contractorId':1,'contractNumber':'ДОГОВОР-42','transferDocument':'ПЕРЕДАЧА-77','date':str(today),'method':'Разборка по договору'}
    call('/api/siz/disposals','POST',disposal,409)
    call(f"/api/siz/writeoffs/{act['id']}/sign",'POST',sig)
    disposed=call('/api/siz/disposals','POST',disposal,201)
    assert disposed['id']!=act['id']
    for kind,identity in [('disposal',disposed['id']),('writeoff',act['id'])]:
        for format in ['html','docx','xlsx']:
            payload=call(f'/api/documents/{kind}/{identity}/{format}',raw=True)
            if format=='html':content=payload.decode()
            else:
                z=zipfile.ZipFile(io.BytesIO(payload));assert z.testzip() is None
                content=' '.join(''.join(ET.fromstring(z.read(n)).itertext()) for n in z.namelist() if n.endswith('.xml'))
            assert expired['inventoryNumber'] in content
            if kind=='disposal':assert all(x in content for x in ['ДОГОВОР-42','ПЕРЕДАЧА-77','Учебный переработчик','Разборка по договору'])
            else:assert 'Комиссия 1, 2, 3' in content and 'ПЕРЕДАЧА-77' not in content
    call('/api/siz/disposals','POST',disposal,409)
    assert call(f"/api/siz/cards/{expired['id']}")['status']=='Disposed'
    group=call('/api/training/groups')[0]
    summary=call(f"/api/training/summary?groupId={group['id']}&year={today.year}")
    assert summary['requiredAnnualHours']==15 and summary['protocolHours']==12 and summary['conductedHours']==12
    assert call(f"/api/training/protocol?groupId={group['id']}&year={today.year}")['ready']
    zipfile.ZipFile(io.BytesIO(call(f"/api/training/protocol/export/docx?groupId={group['id']}&year={today.year}",raw=True))).testzip()
    session={'trainingGroupId':group['id'],'topicNumber':9,'topicName':'Проверка','date':str(today),'hours':3,'classType':'Практика','leaderName':'Тест'}
    lesson=call('/api/training/sessions','POST',session,201); sid=lesson['id']
    mark={'employeeId':employee['id'],'status':'Present','testResult':'Passed','testScore':90}
    call(f'/api/training/sessions/{sid}/attendance','PUT',mark)
    call(f'/api/training/sessions/{sid}/sign','POST',sig)
    call(f'/api/training/sessions/{sid}/attendance','PUT',mark,409)
    call(f'/api/training/sessions/{sid}','PUT',session,409)
    summary=call(f"/api/training/summary?groupId={group['id']}&year={today.year}")
    assert next(e for e in summary['employees'] if e['employeeId']==employee['id'])['attendedHours']==15
    change=call(f'/api/training/sessions/{sid}/change-requests','POST',{'requestedBy':'Проверка','reason':'Исправление учебной записи'})
    call(f"/api/training/change-requests/{change['id']}/resolve",'POST',{'approved':True,'resolvedBy':'Руководитель'})
    call(f'/api/training/sessions/{sid}','PUT',dict(session,topicName='Исправленная тема'))
    call(f'/api/training/sessions/{sid}/sign','POST',sig)
    assert len(call(f'/api/training/sessions/{sid}/audit'))>=3
    leave={'employeeId':employees[1]['id'],'startDate':str(today),'endDate':str(today+dt.timedelta(days=5)),'kind':'Vacation'}
    absence=call('/api/wartime/leaves','POST',leave,201)
    lesson=call('/api/training/sessions','POST',dict(session,topicNumber=10),201)
    call(f"/api/training/sessions/{lesson['id']}/attendance",'PUT',dict(mark,employeeId=employees[1]['id']),409)
    call('/api/evacuation/auto-plan','POST',{'shift':None})
    assert call('/api/evacuation/summary')['assigned']==len(employees)
    vehicle=call('/api/evacuation/vehicles')[0]
    call('/api/evacuation/reset','POST',status=204)
    call('/api/evacuation/vehicles/'+str(vehicle['id']),'PUT',dict(vehicle,ready=False))
    indefinite=call('/api/evacuation/vehicles','POST',{'name':'Бессрочный','capacity':2,'contracted':True,'contractNumber':'ТР-001','contractValidUntil':None},201)
    assert indefinite['available']
    expired_vehicle=call('/api/evacuation/vehicles','POST',{'name':'Просроченный','capacity':20,'contracted':True,'contractNumber':'ТР-002','contractValidUntil':str(today-dt.timedelta(days=1))},201)
    assert not expired_vehicle['available']
    plan_evac=call('/api/evacuation/auto-plan','POST',{'shift':None})
    assert plan_evac['assigned']==2 and plan_evac['deficit']==len(employees)-2
    call('/api/evacuation/vehicles/'+str(vehicle['id']),'PUT',vehicle)
    call('/api/evacuation/auto-plan','POST',{'shift':None})
    route=call('/api/evacuation/routes','POST',{'name':'Проверенный маршрут','receptionPointId':1,'startPoint':'Вход','distanceKm':12,'travelTimeMinutes':30,'description':'Путь к пункту'},201)
    assert next(r for r in call('/api/evacuation/routes') if r['id']==route['id'])['description']=='Путь к пункту'
    order=call('/api/nfgo/orders','POST',{'number':'ТЕСТ-НФГО','basis':'Учебная проверка'},201)
    call('/api/employees/'+str(employee['id']),'PUT',dict(employee,position='Новая должность'))
    call(f"/api/nfgo/orders/{order['id']}/sign",'POST',sig,409)
    call(f"/api/nfgo/orders/{order['id']}/refresh",'POST')
    call(f"/api/nfgo/orders/{order['id']}/sign",'POST',sig)
    call(f"/api/nfgo/orders/{order['id']}/refresh",'POST',status=409)
    plan=call('/api/reports/procurement'); assert sorted(set(r['year'] for r in plan))==list(range(today.year,today.year+5))
    for kind in ['nfgo','procurement']:
        for format in ['xlsx','docx','html']:
            export=call(f'/api/reports/{kind}/export/{format}',raw=True)
            if format!='html':
                z=zipfile.ZipFile(io.BytesIO(export)); assert z.testzip() is None
            else: assert b'<table' in export
    ET.fromstring(call('/api/reports/nfgo/export/xml',raw=True))
    call('/api/reports/nfgo/export/json')
    call('/api/wartime/protected-forms',status=409)
    future={'orderNumber':'TEST-FUTURE','organizationName':'Учебный музей','orderDate':str(today),
            'effectiveFrom':(dt.datetime.now(dt.timezone.utc)+dt.timedelta(days=1)).isoformat(),
            'basis':'Учебная проверка','responsiblePerson':'Проверка','createdBy':'Проверка'}
    future_order=call('/api/wartime/orders','POST',future,201)
    hostile=call('/api/wartime/orders','POST',dict(future,orderNumber="'; DROP TABLE Employees;--",basis='<script>alert(1)</script>'),201)
    hostile_html=call(f"/api/wartime/orders/{hostile['id']}/document",raw=True).decode()
    assert '<script>alert(1)</script>' not in hostile_html and '&lt;script&gt;' in hostile_html
    assert len(call('/api/employees'))==len(employees)
    call(f"/api/wartime/orders/{future_order['id']}/sign",'POST',sig)
    call(f"/api/wartime/orders/{future_order['id']}/activate",'POST',status=409)
    noon=f'{today+dt.timedelta(days=1)}T12:00:00+03:00'
    timed=call('/api/wartime/orders','POST',dict(future,orderNumber='MOSCOW-NOON',effectiveFrom=noon),201)
    assert timed['effectiveFrom'].endswith('09:00:00Z'),timed
    call('/api/wartime/orders','POST',dict(future,orderNumber='NO-OFFSET',effectiveFrom=noon[:-6]),400)
    loaded=call('/api/wartime/orders/'+str(timed['id']))
    assert loaded['effectiveFrom']==timed['effectiveFrom']
    call('/api/wartime/orders/'+str(timed['id']),'PUT',dict(future,orderNumber='MOSCOW-NOON',effectiveFrom=loaded['effectiveFrom']))
    document=call(f"/api/wartime/orders/{timed['id']}/document",raw=True).decode()
    assert '12:00' in document and '(Москва)' in document
    call(f"/api/wartime/orders/{timed['id']}/sign",'POST',sig)
    stop(p);p=start();call('/api/auth/login','POST',{'name':'Проверка','password':password})
    assert call('/api/wartime/orders/'+str(timed['id']))['effectiveFrom']==timed['effectiveFrom']
    call(f"/api/wartime/orders/{timed['id']}/activate",'POST',status=409)
    past=today-dt.timedelta(days=1)
    war=call('/api/wartime/orders','POST',dict(future,orderNumber='MOSCOW-PAST',orderDate=str(past),effectiveFrom=f'{past}T12:00:00+03:00'),201)
    call(f"/api/wartime/orders/{war['id']}/activate",'POST',status=409)
    call(f"/api/wartime/orders/{war['id']}/sign",'POST',sig)
    call(f"/api/wartime/orders/{war['id']}/activate",'POST')
    assert call('/api/wartime/orders/state')['mode']=='Wartime'
    call('/api/wartime/leaves','POST',dict(leave,employeeId=employees[2]['id']),409)
    assert next(l for l in call('/api/wartime/leaves') if l['id']==absence['id'])['status']=='Suspended'
    assert len(call('/api/wartime/schedules'))==len(employees)
    call('/api/wartime/protected-forms')
    started=time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(lambda _: call('/api/dashboard'), range(100)))
    elapsed=time.perf_counter()-started
    stop(p); p=start()
    call('/api/auth/login','POST',{'name':'Проверка','password':password})
    assert call('/api/wartime/orders/state')['mode']=='Wartime'
    assert call(f"/api/siz/cards/{expired['id']}")['status']=='Disposed'
    # Build a database with exactly the previous schema and copy the test records into it.
    stop(p)
    source=sqlite3.connect(data/'gochs.db');legacy_path=data/'legacy.db';legacy=sqlite3.connect(legacy_path)
    legacy.executescript((ROOT/'backend/Data/Migrations/001_initial.sql').read_text(encoding='utf-8'))
    tables=[r[0] for r in legacy.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")]
    old_rows={}
    for table_name in tables:
        columns=[r[1] for r in legacy.execute(f'PRAGMA table_info("{table_name}")')]
        names=','.join('"'+c+'"' for c in columns)
        rows=source.execute(f'SELECT {names} FROM "{table_name}" ORDER BY Id').fetchall();old_rows[table_name]=rows
        legacy.executemany(f'INSERT INTO "{table_name}" ({names}) VALUES ({",".join("?" for _ in columns)})',rows)
    legacy.commit();legacy.close();source.close()
    shutil.copy2(legacy_path,data/'gochs.db')
    for suffix in ['-wal','-shm']:
        file=data/('gochs.db'+suffix)
        if file.exists():file.unlink()
    p=start();call('/api/auth/login','POST',{'name':'Проверка','password':password})
    assert len(list((data/'backups').glob('before-upgrade-*.db')))==1
    upgraded=sqlite3.connect(data/'gochs.db')
    for table_name in tables:
        columns=[r[1] for r in upgraded.execute(f'PRAGMA table_info("{table_name}")') if r[1]!='Method']
        names=','.join('"'+c+'"' for c in columns)
        assert upgraded.execute(f'SELECT {names} FROM "{table_name}" ORDER BY Id').fetchall()==old_rows[table_name],table_name
    assert upgraded.execute('SELECT count(*) FROM __GochsMigrations').fetchone()[0]==2;upgraded.close()
    assert call('/api/wartime/orders/'+str(timed['id']))['effectiveFrom']==timed['effectiveFrom']
    backup_path=data/'manual-backup.db'
    subprocess.run([DOTNET,str(dll),'--backup',str(backup_path)],cwd=ROOT/'backend',env=env,check=True)
    assert sqlite3.connect(backup_path).execute('PRAGMA integrity_check').fetchone()[0]=='ok'
    cookie='; '.join(c.name+'='+c.value for c in jar)
    call('/api/auth/logout','POST',status=204)
    send_raw('/api/employees',None,{'Cookie':cookie},401,'GET')
    for _ in range(5):call('/api/auth/login','POST',{'name':'Проверка','password':'wrong'},401)
    call('/api/auth/login','POST',{'name':'Проверка','password':password},429)
    stop(p)
    subprocess.run([DOTNET,str(dll),'--restore',str(backup_path)],cwd=ROOT/'backend',env=env,check=True)
    p=start();call('/api/auth/login','POST',{'name':'Проверка','password':password})
    assert call('/api/wartime/orders/state')['mode']=='Wartime'
    stop(p)
    env.update(ServerMode='true',TrustedProxy='127.0.0.1',SwaggerEnabled='false')
    p=start()
    call('/api/auth/session',status=400,anonymous=True)
    # Emulate one trusted proxy. Secure cookies are inspected, not sent over public HTTP.
    def forwarded(path,body=None,cookie=None,token=None):
        headers={'X-Forwarded-Proto':'https','Content-Type':'application/json'}
        if cookie:headers['Cookie']=cookie
        if token:headers['X-CSRF-TOKEN']=token
        req=urllib.request.Request(base+path,headers=headers,data=None if body is None else json.dumps(body).encode())
        try:return urllib.request.urlopen(req)
        except urllib.error.HTTPError as e:return e
    csrf_response=forwarded('/api/auth/token');token=json.loads(csrf_response.read())['token']
    cookies=csrf_response.headers.get_all('Set-Cookie');assert any('secure' in c.lower() and 'httponly' in c.lower() for c in cookies)
    login_response=forwarded('/api/auth/login',{'name':'Прокси','password':password},'; '.join(c.split(';')[0] for c in cookies),token)
    assert login_response.status==200
    session_cookies=login_response.headers.get_all('Set-Cookie');assert any('Gochs.Session=' in c and 'secure' in c.lower() and 'samesite=strict' in c.lower() for c in session_cookies)
    assert forwarded('/swagger/v1/swagger.json',cookie='; '.join(c.split(';')[0] for c in session_cookies)).status==404
    stop(p);env['TrustedProxy']='192.0.2.10';p=start()
    assert forwarded('/api/auth/session').status==400
    result={'checks':count,'loadRequests':100,'loadWorkers':8,'loadSeconds':round(elapsed,2),'result':'PASS'}
    (data/'result.json').write_text(json.dumps(result,indent=2),encoding='utf-8'); print(json.dumps(result))
finally:
    if p.poll() is None: stop(p)
    log.close()
