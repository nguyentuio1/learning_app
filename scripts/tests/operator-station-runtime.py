import json,pathlib,sys
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright

ROOT=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else pathlib.Path(__file__).resolve().parents[2]/'artifacts'/'la367-runtime').resolve()
ROOT.mkdir(parents=True,exist_ok=True)
events=[];errors=[]
with sync_playwright() as p:
    browser=p.chromium.launch(headless=True,channel='chrome')
    context=browser.new_context(viewport={'width':430,'height':932},device_scale_factor=1)
    page=context.new_page()
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.on('response',lambda r:events.append({'method':r.request.method,'path':urlparse(r.url).path,'status':r.status}) if '/api/' in r.url else None)
    page.goto('http://127.0.0.1:18731/#/login',wait_until='domcontentloaded')
    page.locator('flt-semantics-placeholder').wait_for(state='attached',timeout=90000)
    page.locator('flt-semantics-placeholder').evaluate('(e)=>e.click()')
    page.get_by_role('textbox',name='Email').click()
    page.wait_for_timeout(150)
    page.keyboard.type('operator@smartdronedelivery.com',delay=20)
    page.get_by_label('Password',exact=True).click()
    page.wait_for_timeout(150)
    page.keyboard.type('Operator@123456',delay=35)
    page.wait_for_timeout(200)
    page.get_by_role('button',name='Continue',exact=True).click()
    try:
        page.get_by_role('heading',name='Station availability',exact=True).wait_for(timeout=25000)
    except Exception:
        page.screenshot(path=str(ROOT/'login-error.png'))
        print('LOGIN ERROR',page.url,page.locator('body').aria_snapshot(),errors,events,flush=True)
        raise
    page.wait_for_timeout(1500)
    snapshot=page.locator('body').aria_snapshot()
    (ROOT/'home-initial-aria.txt').write_text(snapshot,encoding='utf-8')
    assert 'Assigned station' in snapshot,snapshot
    assert 'Selected Station' not in snapshot,snapshot
    page.screenshot(path=str(ROOT/'01-operator-home.png'),full_page=True)
    page.get_by_role('button',name='Refresh',exact=True).click()
    page.wait_for_timeout(1200)
    snapshot=page.locator('body').aria_snapshot()
    assert 'Assigned station' in snapshot,snapshot
    assert 'Selected Station' not in snapshot,snapshot
    page.screenshot(path=str(ROOT/'02-after-refresh.png'),full_page=True)
    page.get_by_role('button',name='Origin Intake',exact=True).click()
    page.wait_for_timeout(900)
    (ROOT/'intake-aria.txt').write_text(page.locator('body').aria_snapshot(),encoding='utf-8')
    page.screenshot(path=str(ROOT/'03-intake.png'),full_page=True)
    assert page.get_by_role('heading',name='Origin package intake',exact=True).is_visible()
    page.get_by_role('button',name='Back',exact=True).click()
    page.wait_for_timeout(1200)
    snapshot=page.locator('body').aria_snapshot()
    assert 'Assigned station' in snapshot and 'Selected Station' not in snapshot,snapshot
    page.get_by_role('button',name='Confirm Pickup',exact=True).click()
    page.wait_for_timeout(900)
    (ROOT/'pickup-aria.txt').write_text(page.locator('body').aria_snapshot(),encoding='utf-8')
    page.screenshot(path=str(ROOT/'04-pickup.png'),full_page=True)
    assert page.get_by_role('heading',name='Package pickup confirmation',exact=True).is_visible()
    page.get_by_role('button',name='Back',exact=True).click()
    page.wait_for_timeout(1200)
    snapshot=page.locator('body').aria_snapshot()
    assert 'Assigned station' in snapshot and 'Selected Station' not in snapshot,snapshot
    page.screenshot(path=str(ROOT/'05-home-after-both-actions.png'),full_page=True)
    assert not errors,errors
    report={'browser':browser.version,'viewport':{'width':430,'height':932},'mode':'Live local API through loopback same-origin gateway; no HTTP fixtures',
        'api':'http://127.0.0.1:5055','ui':'http://127.0.0.1:18731','events':events,'pageErrors':errors,
        'outcomes':['Operator demo login succeeded','Assigned-station scope remained restricted after manual refresh','Intake and Pickup routes opened; returning to home preserved assigned-station scope'],
        'limits':'Navigation/read-only validation; no intake/pickup confirmation or administrative reassignment was executed against live data. LA-364 journey findings remain open.'}
    (ROOT/'runtime-events.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps({'outcomes':report['outcomes'],'apiRequests':len(events),'pageErrors':errors}),flush=True)
    browser.close()
