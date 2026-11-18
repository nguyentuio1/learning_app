// LA-176: k6 load and concurrency test script for SmartDroneDelivery API
// Run against a deployed or Docker Compose API instance:
//   k6 run -e BASE_URL=http://localhost:5055 -e TEST_EMAIL=... -e TEST_PASSWORD=... scripts/k6-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5055';
const TEST_EMAIL = __ENV.TEST_EMAIL || 'customer1@smartdronedelivery.com';
const TEST_PASSWORD = __ENV.TEST_PASSWORD || 'Passw0rd123!';
const ENV_ORDER_ID = __ENV.ORDER_ID || '';

export const options = {
  scenarios: {
    concurrency_10: {
      executor: 'constant-vus',
      vus: 10,
      duration: '15s',
      startTime: '0s',
      tags: { tier: '10_vus' },
    },
    concurrency_50: {
      executor: 'constant-vus',
      vus: 50,
      duration: '20s',
      startTime: '20s',
      tags: { tier: '50_vus' },
    },
    concurrency_100: {
      executor: 'constant-vus',
      vus: 100,
      duration: '20s',
      startTime: '45s',
      tags: { tier: '100_vus' },
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:health,tier:10_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:health,tier:50_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:health,tier:100_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:login,tier:10_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:login,tier:50_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:login,tier:100_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:list_orders,tier:10_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:list_orders,tier:50_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:list_orders,tier:100_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:tracking,tier:10_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:tracking,tier:50_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:tracking,tier:100_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:create_order,tier:10_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:create_order,tier:50_vus}': ['p(95)<3000'],
    'http_req_duration{endpoint:create_order,tier:100_vus}': ['p(95)<3000'],
  },
};

export function setup() {
  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: TEST_EMAIL, password: TEST_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  check(loginRes, { 'setup login 200': (r) => r.status === 200 });
  const body = loginRes.json();
  const token = body ? body.accessToken : '';
  if (!token) {
    throw new Error(`k6 setup failed: login did not return accessToken (status=${loginRes.status})`);
  }
  const authHeaders = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };

  let originStationId = '';
  let destinationStationId = '';
  const stationsRes = http.get(`${BASE_URL}/api/landing-stations?page=1&pageSize=10`, {
    headers: authHeaders,
  });
  if (stationsRes.status === 200) {
    const stationItems = (stationsRes.json() && stationsRes.json().items) || stationsRes.json() || [];
    const operational = Array.isArray(stationItems)
      ? stationItems.filter((s) => s.status === 'OPERATIONAL')
      : [];
    if (operational.length >= 2) {
      originStationId = operational[0].id;
      destinationStationId = operational[1].id;
    }
  }
  if (!originStationId || !destinationStationId) {
    throw new Error(
      `k6 setup failed: need at least 2 OPERATIONAL landing stations (status=${stationsRes.status})`,
    );
  }

  let orderId = ENV_ORDER_ID;
  if (!orderId) {
    const ordersRes = http.get(`${BASE_URL}/api/delivery-orders?page=1&pageSize=1`, {
      headers: authHeaders,
    });
    if (ordersRes.status === 200) {
      const items = (ordersRes.json() && ordersRes.json().items) || [];
      if (items.length > 0) {
        orderId = items[0].id;
      }
    }
  }

  if (!orderId) {
    const pkgRes = http.post(
      `${BASE_URL}/api/packages`,
      JSON.stringify({
        packageName: 'k6 Setup Package',
        weightGram: 500,
        lengthCm: 20,
        widthCm: 15,
        heightCm: 10,
        isFragile: false,
      }),
      { headers: authHeaders },
    );
    if (pkgRes.status !== 201 || !pkgRes.json() || !pkgRes.json().id) {
      throw new Error(`k6 setup failed: unable to create setup package (status=${pkgRes.status})`);
    }
    const ordRes = http.post(
      `${BASE_URL}/api/delivery-orders`,
      JSON.stringify({
        packageId: pkgRes.json().id,
        originStationId,
        destinationStationId,
        recipientName: 'k6 Setup Recipient',
        recipientPhone: '0901234567',
      }),
      { headers: authHeaders },
    );
    if (ordRes.status !== 201 || !ordRes.json() || !ordRes.json().id) {
      throw new Error(`k6 setup failed: unable to create setup order (status=${ordRes.status})`);
    }
    orderId = ordRes.json().id;
  }

  return { token, orderId, originStationId, destinationStationId };
}

export default function (data) {
  const authHeaders = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${data.token}`,
  };

  // 1. Health check
  const healthRes = http.get(`${BASE_URL}/health/live`, {
    tags: { endpoint: 'health' },
  });
  check(healthRes, { 'health 200': (r) => r.status === 200 });

  // 2. Authenticated login probe
  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: TEST_EMAIL, password: TEST_PASSWORD }),
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { endpoint: 'login' },
    },
  );
  check(loginRes, { 'login 200': (r) => r.status === 200 });

  // 3. List customer delivery orders
  const listRes = http.get(`${BASE_URL}/api/delivery-orders?page=1&pageSize=20`, {
    headers: authHeaders,
    tags: { endpoint: 'list_orders' },
  });
  check(listRes, { 'list orders 200': (r) => r.status === 200 });

  // 4. Tracking read (setup guarantees orderId)
  const trackRes = http.get(`${BASE_URL}/api/delivery-orders/${data.orderId}/tracking`, {
    headers: authHeaders,
    tags: { endpoint: 'tracking' },
  });
  check(trackRes, { 'tracking 200': (r) => r.status === 200 });

  // 5. Create delivery order (setup guarantees originStationId & destinationStationId)
  const pkgRes = http.post(
    `${BASE_URL}/api/packages`,
    JSON.stringify({
      packageName: `k6 Box VU${__VU}-${__ITER}`,
      weightGram: 500,
      lengthCm: 20,
      widthCm: 15,
      heightCm: 10,
      isFragile: false,
    }),
    { headers: authHeaders },
  );
  const pkgOk = check(pkgRes, { 'setup iter package 201': (r) => r.status === 201 });
  if (!pkgOk || !pkgRes.json() || !pkgRes.json().id) {
    throw new Error(`k6 iteration failed: package creation returned status=${pkgRes.status}`);
  }
  const createOrderRes = http.post(
    `${BASE_URL}/api/delivery-orders`,
    JSON.stringify({
      packageId: pkgRes.json().id,
      originStationId: data.originStationId,
      destinationStationId: data.destinationStationId,
      recipientName: 'k6 Load Recipient',
      recipientPhone: '0901234567',
    }),
    { headers: authHeaders, tags: { endpoint: 'create_order' } },
  );
  check(createOrderRes, { 'create order 201': (r) => r.status === 201 });

  sleep(0.2);
}
