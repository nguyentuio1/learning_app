import json, pathlib, threading, functools, http.server, sys
from playwright.sync_api import sync_playwright

ROOT=pathlib.Path(sys.argv[2] if len(sys.argv)>2 else pathlib.Path(__file__).resolve().parents[2]/'artifacts'/'la365-runtime').resolve()
ORDER='e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33'
USER={'id':'11111111-1111-4111-8111-111111111111','email':'runtime@example.invalid',
      'fullName':'Runtime Customer','phoneNumber':'0900000000','status':'ACTIVE',
      'role':{'id':1,'code':'CUSTOMER','name':'Customer'},'createdAt':'2026-11-18T00:00:00Z'}

class Quiet(http.server.SimpleHTTPRequestHandler):
    def log_message(self,*args): pass

def run(variant,port):
    server=http.server.ThreadingHTTPServer(('127.0.0.1',port),functools.partial(Quiet,directory=str(ROOT/('web-'+variant))))
    threading.Thread(target=server.serve_forever,daemon=True).start()
    events=[];mode=['503'];errors=[]
    with sync_playwright() as p:
        browser=p.chromium.launch(headless=True,channel='chrome')
        context=browser.new_context(viewport={'width':390,'height':844},device_scale_factor=1)
        def api(route):
            req=route.request;path=req.url.split('/api')[-1].split('?')[0]
            events.append({'method':req.method,'path':path,'mode':mode[0]})
            data={};status=200
            if path=='/auth/login':
                data={'accessToken':'runtime-fixture-access','refreshToken':'runtime-fixture-refresh','expiresIn':900,'user':USER}
            elif path=='/users/me': data=USER
            elif path.endswith('/ai-assistant/sessions'):data={'items':[],'page':1,'pageSize':20,'totalCount':0}
            elif path=='/ai-assistant/messages':
                if mode[0]=='503':status=503;data={'detail':'The assistant is temporarily unavailable.'}
                elif mode[0]=='timeout':route.abort('timedout');return
                else:data={'outcome':'HELP','sessionId':None,'userMessageId':None,'reply':{'id':None,'senderType':'BOT','messageText':'Fixture reply: select a delivery to view its status.','createdAt':'2026-11-18T00:00:00Z'}}
            elif path=='/notifications/unread-count':data={'count':0}
            elif path=='/auth/logout':status=204
            else:data={'items':[],'page':1,'pageSize':20,'totalCount':0}
            route.fulfill(status=status,json=data,headers={'Access-Control-Allow-Origin':'*'})
        context.route('http://127.0.0.1:5055/**',api)
        page=context.new_page();page.on('pageerror',lambda e:errors.append(str(e)));page.on('console',lambda e:print('CONSOLE',e.type,e.text,flush=True) if e.type in ['error','warning'] else None)
        page.goto(f'http://127.0.0.1:{port}/#/login')
        page.locator('flt-semantics-placeholder').wait_for(state='attached',timeout=60000)
        page.locator('flt-semantics-placeholder').evaluate('(e)=>e.click()')
        page.wait_for_timeout(1500)
        page.screenshot(path=str(ROOT/f'{variant}-login.png'))
        page.get_by_role('textbox',name='Email').fill('runtime@example.invalid')
        page.get_by_label('Password',exact=True).fill('Runtime123!')
        page.get_by_role('button',name='Continue',exact=True).click()
        page.wait_for_timeout(1000)
        page.goto(f'http://127.0.0.1:{port}/#/customer/assistant?orderId={ORDER}')
        page.wait_for_timeout(1500)
        if page.locator('flt-semantics-placeholder').count():page.locator('flt-semantics-placeholder').evaluate('(e)=>e.click()')
        page.wait_for_timeout(500)
        page.wait_for_timeout(500)
        snapshot=page.locator('body').aria_snapshot()
        (ROOT/f'{variant}-initial-aria.txt').write_text(snapshot)
        page.screenshot(path=str(ROOT/f'{variant}-assistant.png'))
        post_count=lambda:len([e for e in events if e['method']=='POST' and e['path']=='/ai-assistant/messages'])
        outcomes=[]
        if variant=='disabled':
            assert post_count()==0
            assert 'Chat is not available in this app.' in snapshot
            assert page.get_by_role('button',name='Send message',exact=True).is_disabled()
            page.get_by_role('button',name='Track this delivery',exact=True).click()
            page.wait_for_timeout(500)
            assert '/customer/orders/tracking/'+ORDER in page.url
            assert post_count()==0
            outcomes=['Default-off notice rendered','Send disabled; zero assistant POSTs','Tracking fallback navigated to selected delivery']
        else:
            box=page.get_by_role('textbox',name='Ask about your deliveries...',exact=True)
            assert box.count()==1
            box.click();page.keyboard.type('Any update?')
            page.get_by_role('button',name='Send message',exact=True).click()
            page.wait_for_timeout(700)
            snapshot=page.locator('body').aria_snapshot()
            assert 'The assistant is temporarily unavailable.' in snapshot
            assert 'Check sessions' not in snapshot and 'Reload history' not in snapshot
            assert post_count()==1
            assert page.locator('input,textarea').last.input_value()=='Any update?'
            assert page.get_by_role('button',name='Send message',exact=True).is_enabled()
            page.screenshot(path=str(ROOT/'enabled-503.png'))
            outcomes.append('Definite503: draft retained; retry enabled; no history-recovery block or automatic POST')
            mode[0]='help'
            page.get_by_role('button',name='Send message',exact=True).click()
            page.wait_for_timeout(700)
            assert post_count()==2
            (ROOT/'enabled-retry-aria.txt').write_text(page.locator('body').aria_snapshot())
            page.screenshot(path=str(ROOT/'enabled-retry-debug.png'))
            assert page.get_by_role('textbox',name='Help',exact=True).is_visible()
            # Flutter SelectableText exposes a disabled semantics textbox without its value.
            # Assert the HELP outcome exists; review the captured reply screenshot as well.
            page.screenshot(path=str(ROOT/'enabled-retry-success.png'))
            outcomes.append('Deliberate retry: supported transient HELP response rendered')
            mode[0]='timeout'
            box=page.get_by_role('textbox',name='Ask about your deliveries...',exact=True);box.click();page.keyboard.type('Timeout question')
            page.get_by_role('button',name='Send message',exact=True).click()
            page.wait_for_timeout(700)
            assert post_count()==3
            assert page.get_by_role('button',name='Send message',exact=True).is_disabled()
            recovery=page.get_by_role('button',name='Check sessions',exact=True)
            assert recovery.is_visible()
            page.screenshot(path=str(ROOT/'enabled-timeout-recovery.png'))
            recovery.click();page.wait_for_timeout(700)
            assert page.get_by_role('button',name='Send message',exact=True).is_enabled()
            assert post_count()==3
            outcomes.append('Transport timeout: send blocked, session recovery required; successful recovery re-enables send without auto retry')
        assert not errors, errors
        print(variant, json.dumps(outcomes),flush=True)
        (ROOT/f'{variant}-events.json').write_text(json.dumps({'browser':browser.version,'viewport':{'width':390,'height':844},'httpMode':'Synthetic fixtures; no live AI','outcomes':outcomes,'events':events,'pageErrors':errors},indent=2))
        browser.close()
    server.shutdown()

if __name__=='__main__':
    variant=sys.argv[1] if len(sys.argv)>1 else 'disabled'
    assert variant in ('disabled','enabled')
    run(variant,18651 if variant=='disabled' else 18652)
