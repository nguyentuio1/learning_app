"""Exercise real demo APIs. Run only against an explicitly selected Development environment.

python scripts/demo-smoke.py --base-url http://localhost:5056 --wait-for-flight
Creates one labelled order. Flight respects the real 15-minute scheduling rule.
No tokens, pickup codes or credentials are printed or written to the report.
"""
import argparse
import json
import time
from datetime import datetime, timedelta, timezone
from urllib.error import HTTPError
from urllib.parse import urlparse
from urllib.request import Request, urlopen


class DemoClient:
    def __init__(self, base_url):
        self.base_url = base_url.rstrip('/')
        self.accounts = {
            'customer': ('customer@smartdronedelivery.com', 'Customer@123456'),
            'dispatcher': ('dispatcher@smartdronedelivery.com', 'Dispatcher@123456'),
            'operator': ('operator@smartdronedelivery.com', 'Operator@123456'),
            'operator2': ('operator2@smartdronedelivery.com', 'Operator@123456'),
            'manager': ('manager@smartdronedelivery.com', 'Manager@123456'),
        }
        self.tokens = {}

    def request(self, path, role=None, method='GET', body=None, retry_auth=True):
        headers = {'Content-Type': 'application/json'}
        if role:
            if role not in self.tokens:
                self.login(role)
            headers['Authorization'] = 'Bearer ' + self.tokens[role]
        data = json.dumps(body).encode() if body is not None else None
        try:
            with urlopen(Request(self.base_url + path, data, headers, method=method), timeout=30) as response:
                raw = response.read()
                return json.loads(raw) if raw else None
        except HTTPError as error:
            if error.code == 401 and role and retry_auth:
                self.login(role)
                return self.request(path, role, method, body, retry_auth=False)
            # Problem bodies contain actionable validation details; never include auth responses.
            raise RuntimeError(f'{method} {path}: HTTP {error.code}: {error.read().decode()}') from None

    def login(self, role):
        email, password = self.accounts[role]
        result = self.request('/api/auth/login', method='POST', body={'email': email, 'password': password})
        self.tokens[role] = result['accessToken']
        return result['user']['role']['code']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-url', required=True)
    parser.add_argument('--wait-for-flight', action='store_true')
    args = parser.parse_args()
    if urlparse(args.base_url).hostname not in ('localhost', '127.0.0.1', '::1'):
        parser.error('This mutating demo smoke test is restricted to loopback Development environments.')
    client = DemoClient(args.base_url)
    assert client.request('/health/ready')['status'] == 'Healthy'
    print(json.dumps({'check': 'demo-role-login', 'roles': {role: client.login(role) for role in client.accounts}}), flush=True)
    stations = client.request('/api/landing-stations?status=OPERATIONAL', 'customer')
    origin = next(s for s in stations if s['stationCode'] == 'HCM-01')
    destination = next(s for s in stations if s['stationCode'] == 'HCM-02')
    package = client.request('/api/packages', 'customer', 'POST', {
        'packageName': 'Integration smoke ' + datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S'),
        'weightGram': 500, 'lengthCm': 20, 'widthCm': 15, 'heightCm': 8, 'isFragile': False,
    })
    order = client.request('/api/delivery-orders', 'customer', 'POST', {
        'packageId': package['id'], 'originStationId': origin['id'], 'destinationStationId': destination['id'],
        'recipientName': 'Demo recipient', 'recipientPhone': '0900000099',
    })
    order_id, pickup_code = order['id'], order['pickupSecureCode']
    client.request(f'/api/delivery-orders/{order_id}/approve', 'dispatcher', 'POST')
    departure = (datetime.now(timezone.utc) + timedelta(minutes=16)).isoformat().replace('+00:00', 'Z')
    client.request(f'/api/delivery-orders/{order_id}/schedule', 'dispatcher', 'PUT', {'scheduledDepartureTime': departure})
    candidates = client.request(f'/api/delivery-orders/{order_id}/available-drones', 'dispatcher')
    drone = next(d for d in candidates if d['isEligible'])
    client.request(f'/api/delivery-orders/{order_id}/flight-mission', 'dispatcher', 'POST', {'droneId': drone['droneId']})
    slots = client.request(f"/api/landing-stations/{origin['id']}/slots", 'operator')
    slot = next(s for s in slots if s['status'] == 'EMPTY')
    intake = client.request(f"/api/landing-stations/{origin['id']}/operations/intake", 'operator', 'POST', {
        'trackingNumber': order['trackingNumber'], 'slotId': slot['id'], 'measuredWeightGram': 500,
        'measuredLengthCm': 20, 'measuredWidthCm': 15, 'measuredHeightCm': 8, 'visualInspectionPassed': True,
    })
    assert intake['status'] == 'READY_FOR_TAKEOFF'
    print(json.dumps({'check': 'created-approved-scheduled-assigned-intaken', 'orderId': order_id,
                      'trackingNumber': order['trackingNumber'], 'departureUtc': departure,
                      'status': intake['status'], 'liveFlightVerified': False}), flush=True)
    if not args.wait_for_flight:
        return
    deadline = time.monotonic() + 30 * 60
    last_status, saw_position = None, False
    while time.monotonic() < deadline:
        snapshot = client.request(f'/api/delivery-orders/{order_id}/tracking', 'customer')
        status = snapshot['status']
        if snapshot.get('lastPosition'):
            saw_position = True
        if status != last_status:
            print(json.dumps({'check': 'live-tracking', 'status': status, 'positionObserved': saw_position}), flush=True)
            last_status = status
        if status == 'ARRIVED':
            assert saw_position, 'No simulator position was observed before arrival'
            client.request(f"/api/landing-stations/{destination['id']}/operations/pickup", 'operator2', 'POST', {
                'trackingNumber': order['trackingNumber'], 'pickupSecureCode': pickup_code,
            })
            final = client.request(f'/api/delivery-orders/{order_id}', 'customer')
            assert final['status'] == 'DELIVERED'
            print(json.dumps({'check': 'live-flight-and-pickup', 'orderId': order_id, 'status': final['status'],
                              'liveFlightVerified': True}), flush=True)
            return
        if status in ('FAILED', 'CANCELLED', 'DELIVERED'):
            raise RuntimeError(f'Unexpected terminal status before smoke-test pickup: {status}')
        time.sleep(2)
    raise RuntimeError('Timed out waiting for simulator flight and arrival')


if __name__ == '__main__':
    main()
