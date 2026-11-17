// LA-176: k6 load and concurrency test script for SmartDroneDelivery API
// Run against a deployed or Docker Compose API instance:
//   k6 run -e BASE_URL=http://localhost:5055 -e TEST_EMAIL=... -e TEST_PASSWORD=... scripts/k6-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5055';
const TEST_EMAIL = __ENV.TEST_EMAIL || 'customer1@smartdronedelivery.com';
const TEST_PASSWORD = __ENV.TEST_PASSWORD || 'Passw0rd123!';
const ORDER_ID = __ENV.ORDER_ID || '';

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
    http_req_duration: ['p(95)<3000'],
    http_req_failed: ['rate<0.01'],
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
  return { token: body ? body.accessToken : '' };
}

export default function (data) {
  const authHeaders = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${data.token}`,
  };

  // 1. Health check
  const healthRes = http.get(`${BASE_URL}/health/live`);
  check(healthRes, { 'health 200': (r) => r.status === 200 });

  // 2. Authenticated login probe
  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: TEST_EMAIL, password: TEST_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  check(loginRes, { 'login 200': (r) => r.status === 200 });

  // 3. List customer delivery orders
  const listRes = http.get(`${BASE_URL}/api/delivery-orders?page=1&pageSize=20`, {
    headers: authHeaders,
  });
  check(listRes, { 'list orders 200': (r) => r.status === 200 });

  // 4. Tracking read if ORDER_ID is provided
  if (ORDER_ID) {
    const trackRes = http.get(`${BASE_URL}/api/delivery-orders/${ORDER_ID}/tracking`, {
      headers: authHeaders,
    });
    check(trackRes, { 'tracking 200': (r) => r.status === 200 });
  }

  sleep(0.2);
}
