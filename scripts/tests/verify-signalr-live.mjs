import http from 'node:http';
import net from 'node:net';
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';
import path from 'node:path';

const require = createRequire(import.meta.url);
const signalR = require(path.resolve(process.cwd(), 'frontend/web/node_modules/@microsoft/signalr/dist/cjs/index.js'));

const API_HOST = process.env.API_HOST || '127.0.0.1';
const API_PORT = Number(process.env.API_PORT || 5055);
const PROXY_PORT = Number(process.env.PROXY_PORT || 5056);
const BASE_API = `http://${API_HOST}:${API_PORT}`;
const BASE_PROXY = `http://${API_HOST}:${PROXY_PORT}`;
const PSQL_PATH = process.env.PSQL_PATH || 'C:\\Program Files\\PostgreSQL\\18\\bin\\psql.exe';
const PGHOST = process.env.PGHOST || 'localhost';
const PGPORT = process.env.PGPORT || '5433';
const PGDATABASE = process.env.PGDATABASE || 'drone_delivery_db';
const PGUSER = process.env.PGUSER || 'postgres';

function assert(condition, message) {
  if (!condition) throw new Error(`Assertion failed: ${message}`);
}

async function api(path, method = 'GET', token = null, body = null) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${BASE_API}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch {}
  return { status: res.status, json, text };
}

function startWsProxy() {
  let blockHub = false;
  const activeSockets = new Set();

  const server = http.createServer((req, res) => {
    if (blockHub && req.url?.startsWith('/hubs/tracking')) {
      res.writeHead(503, { 'Content-Type': 'text/plain' });
      res.end('Hub temporarily blocked for fallback verification');
      return;
    }
    const proxyReq = http.request({
      host: API_HOST,
      port: API_PORT,
      path: req.url,
      method: req.method,
      headers: req.headers,
    }, (proxyRes) => {
      res.writeHead(proxyRes.statusCode || 500, proxyRes.headers);
      proxyRes.pipe(res);
    });
    proxyReq.on('error', (err) => {
      if (!res.headersSent) res.writeHead(502);
      res.end(err.message);
    });
    req.pipe(proxyReq);
  });

  server.on('upgrade', (req, clientSocket, head) => {
    if (blockHub) {
      clientSocket.write('HTTP/1.1 503 Service Unavailable\r\n\r\n');
      clientSocket.destroy();
      return;
    }
    const upstream = net.connect(API_PORT, API_HOST, () => {
      let raw = `${req.method} ${req.url} HTTP/${req.httpVersion}\r\n`;
      for (let i = 0; i < req.rawHeaders.length; i += 2) {
        raw += `${req.rawHeaders[i]}: ${req.rawHeaders[i + 1]}\r\n`;
      }
      raw += '\r\n';
      upstream.write(raw);
      if (head?.length) upstream.write(head);
      upstream.pipe(clientSocket);
      clientSocket.pipe(upstream);
    });
    activeSockets.add(clientSocket);
    activeSockets.add(upstream);
    const cleanup = () => {
      activeSockets.delete(clientSocket);
      activeSockets.delete(upstream);
      clientSocket.destroy();
      upstream.destroy();
    };
    clientSocket.on('error', cleanup);
    upstream.on('error', cleanup);
  });

  return {
    server,
    dropActiveSockets() {
      for (const s of activeSockets) s.destroy();
      activeSockets.clear();
    },
    setBlockHub(val) { blockHub = val; },
  };
}

async function waitFor(predicate, timeoutMs = 15000, intervalMs = 200) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (predicate()) return true;
    await new Promise(r => setTimeout(r, intervalMs));
  }
  return false;
}

async function main() {
  const proxy = startWsProxy();
  await new Promise(resolve => proxy.server.listen(PROXY_PORT, API_HOST, resolve));

  const logLines = [];
  const log = (msg) => {
    const line = `[${new Date().toISOString()}] ${msg}`;
    logLines.push(line);
    console.error(line);
  };

  try {
    const report = {
      baseUrl: BASE_API,
      wsProxyUrl: BASE_PROXY,
      steps: [],
    };

    // 1. Login customer, dispatcher, operator
    const custLogin = await api('/api/auth/login', 'POST', null, { email: 'customer@smartdronedelivery.com', password: 'Customer@123456' });
    const dispLogin = await api('/api/auth/login', 'POST', null, { email: 'dispatcher@smartdronedelivery.com', password: 'Dispatcher@123456' });
    const opLogin = await api('/api/auth/login', 'POST', null, { email: 'operator@smartdronedelivery.com', password: 'Operator@123456' });
    if (custLogin.status !== 200 || dispLogin.status !== 200 || opLogin.status !== 200) {
      throw new Error(`Login failed: cust=${custLogin.status}, disp=${dispLogin.status}, op=${opLogin.status}`);
    }
    const custToken = custLogin.json.accessToken;
    const dispToken = dispLogin.json.accessToken;
    const opToken = opLogin.json.accessToken;
    log('Authenticated customer, dispatcher, and station operator (200 OK).');

    // 2. Stations, slots, and idle drone
    const stationsRes = await api('/api/landing-stations?page=1&pageSize=10', 'GET', custToken);
    const operational = (stationsRes.json.items || stationsRes.json).filter(s => s.status === 'OPERATIONAL');
    const origin = operational.find(s => s.stationCode === 'HCM-01') || operational[0];
    const dest = operational.find(s => s.stationCode === 'HCM-02') || operational[1];
    const slotsRes = await api(`/api/landing-stations/${origin.id}/slots`, 'GET', opToken);
    const emptySlot = (slotsRes.json.items || slotsRes.json).find(s => s.status === 'EMPTY');
    const dronesRes = await api('/api/drones?page=1&pageSize=10', 'GET', dispToken);
    const idleDrone = (dronesRes.json.items || dronesRes.json).find(d => d.status === 'IDLE');
    if (!emptySlot || !idleDrone) throw new Error('Missing empty slot or idle drone in seeded data');

    // 3. Create package + order
    const pkgRes = await api('/api/packages', 'POST', custToken, {
      packageName: 'LA-370 Live WS Telemetry Parcel',
      weightGram: 500, lengthCm: 20, widthCm: 15, heightCm: 10, isFragile: false,
    });
    const orderRes = await api('/api/delivery-orders', 'POST', custToken, {
      packageId: pkgRes.json.id,
      originStationId: origin.id,
      destinationStationId: dest.id,
      recipientName: 'LA-370 Recipient',
      recipientPhone: '0901234567',
    });
    const orderId = orderRes.json.id ?? orderRes.json.order?.id;
    const trackingNumber = orderRes.json.trackingNumber ?? orderRes.json.order?.trackingNumber;
    report.orderId = orderId;
    report.trackingNumber = trackingNumber;
    log(`Created order ${trackingNumber} (${orderId}).`);

    // 4. Negotiate check
    const negRes = await fetch(`${BASE_PROXY}/hubs/tracking/negotiate?negotiateVersion=1`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${custToken}` },
    });
    const negJson = await negRes.json();
    report.steps.push({
      step: 'signalr_negotiate',
      status: negRes.status,
      availableTransports: (negJson.availableTransports || []).map(t => t.transport),
    });

    // 5. Connect with automatic reconnect + WebSockets enforced, zero return-value callbacks
    const lifecycle = [];
    const statusEvents = [];
    const positionEvents = [];
    const fallbackPolls = [];

    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_PROXY}/hubs/tracking`, {
        accessTokenFactory: () => custToken,
        transport: signalR.HttpTransportType.WebSockets,
        skipNegotiation: false,
      })
      .withAutomaticReconnect([0, 300, 800, 1500])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    conn.on('StatusChanged', (payload) => {
      statusEvents.push({ phase: lifecycle.includes('reconnected') ? 'after_reconnect' : 'initial', payload });
    });
    conn.on('PositionUpdated', (payload) => {
      positionEvents.push({ phase: lifecycle.includes('reconnected') ? 'after_reconnect' : 'initial', payload });
    });
    conn.onreconnecting((err) => {
      lifecycle.push('reconnecting');
      log(`SignalR onreconnecting fired: ${err?.message || 'socket dropped'}`);
    });
    conn.onreconnected(async () => {
      lifecycle.push('reconnected');
      await conn.invoke('Subscribe', orderId);
      lifecycle.push('resubscribed');
      log(`SignalR onreconnected fired and re-subscribed to ${orderId}.`);
    });
    conn.onclose(async () => {
      lifecycle.push('closed_to_http_polling');
      const pollRes = await api(`/api/delivery-orders/${orderId}/tracking`, 'GET', custToken);
      const body = pollRes.json || {};
      const lastPos = body.lastPosition;
      const hasValidLastPos =
        lastPos !== null &&
        typeof lastPos === 'object' &&
        Number.isFinite(lastPos.latitude) &&
        Number.isFinite(lastPos.longitude);
      const hasPickupSecureCode = Object.prototype.hasOwnProperty.call(body, 'pickupSecureCode');

      fallbackPolls.push({
        status: pollRes.status,
        returnedOrderId: body.orderId,
        returnedStatus: body.status,
        hasValidLastPosition: hasValidLastPos,
        lastPositionLatitude: lastPos?.latitude ?? null,
        lastPositionLongitude: lastPos?.longitude ?? null,
        hasPickupSecureCode,
      });
      log(`SignalR onclose triggered automatic HTTP fallback poll -> HTTP ${pollRes.status}, status=${body.status}, lastPos=(${lastPos?.latitude}, ${lastPos?.longitude}).`);
    });

    await conn.start();
    await conn.invoke('Subscribe', orderId);
    report.steps.push({
      step: 'signalr_connect_and_subscribe',
      state: conn.state,
      transport: 'WebSockets(enforced)',
      subscribedOrderId: orderId,
    });
    log('Connected over WebSockets and subscribed to owned order.');

    // 6. Approve -> Schedule -> Intake (READY_FOR_TAKEOFF)
    const approveRes = await api(`/api/delivery-orders/${orderId}/approve`, 'POST', dispToken, { remarks: 'LA-370 WS test' });
    const depFuture = new Date(Date.now() + 20 * 60 * 1000).toISOString();
    const schedRes = await api(`/api/delivery-orders/${orderId}/schedule`, 'PUT', dispToken, {
      scheduledDepartureTime: depFuture,
    });
    const assignRes = await api(`/api/delivery-orders/${orderId}/flight-mission`, 'POST', dispToken, {
      droneId: idleDrone.id,
    });
    const intakeRes = await api(`/api/landing-stations/${origin.id}/operations/intake`, 'POST', opToken, {
      trackingNumber,
      slotId: emptySlot.id,
      measuredWeightGram: 500,
      measuredLengthCm: 20,
      measuredWidthCm: 15,
      measuredHeightCm: 10,
      visualInspectionPassed: true,
    });
    log(`Order lifecycle progressed: approve=${approveRes.status}, schedule=${schedRes.status}, assign=${assignRes.status}, intake=${intakeRes.status} (${intakeRes.status !== 200 ? intakeRes.text : 'OK'}).`);
    if (intakeRes.status !== 200) throw new Error(`Intake failed: ${intakeRes.status} ${intakeRes.text}`);

    // 7. Advance ScheduledDepartureTime in local test DB so real DroneSimulatorService takes off and streams PositionUpdated
    execFileSync(PSQL_PATH, [
      '-U', PGUSER, '-h', PGHOST, '-p', PGPORT, '-d', PGDATABASE,
      '-c', `UPDATE delivery_orders SET "ScheduledDepartureTime" = NOW() - INTERVAL '5 seconds' WHERE "Id" = '${orderId}'; UPDATE flight_missions SET "ScheduledDepartureTime" = NOW() - INTERVAL '5 seconds' WHERE "OrderId" = '${orderId}';`,
    ], { stdio: 'pipe' });
    log('Set ScheduledDepartureTime to past in test DB to trigger real DroneSimulatorService takeoff.');

    const gotInitialPos = await waitFor(() => positionEvents.length >= 1, 12000);
    if (!gotInitialPos) throw new Error('Timed out waiting for initial live PositionUpdated over WebSocket');
    log(`Received ${positionEvents.length} initial PositionUpdated frame(s) over WebSocket.`);

    const initialTs = positionEvents.find(e => e.phase === 'initial')?.payload?.timestamp;
    // 8. Drop WebSocket TCP connection to trigger withAutomaticReconnect()
    proxy.dropActiveSockets();
    const reconnectedOk = await waitFor(() => lifecycle.includes('resubscribed'), 10000);
    if (!reconnectedOk) throw new Error(`Automatic reconnect did not complete; lifecycle=${JSON.stringify(lifecycle)}`);

    const gotPostReconnectFreshPos = await waitFor(
      () => positionEvents.some(e => e.phase === 'after_reconnect' && Date.parse(e.payload?.timestamp) > Date.parse(initialTs)),
      15000,
    );
    if (!gotPostReconnectFreshPos) throw new Error('Timed out waiting for a newer PositionUpdated timestamp after automatic reconnect');
    log('Received live PositionUpdated with strictly newer timestamp after automatic reconnect and re-subscribe.');

    // 9. Block hub and drop socket to verify automatic onclose -> HTTP fallback transition
    proxy.setBlockHub(true);
    proxy.dropActiveSockets();
    const gotFallback = await waitFor(() => fallbackPolls.length >= 1, 15000);
    if (!gotFallback) throw new Error(`Automatic HTTP fallback did not trigger; lifecycle=${JSON.stringify(lifecycle)}`);

    await conn.stop();

    // 10. Strict assertions on StatusChanged events
    assert(statusEvents.length > 0, 'Expected at least one StatusChanged event over WebSocket');
    for (const ev of statusEvents) {
      assert(ev.payload?.orderId === orderId, `StatusChanged event orderId mismatch: expected ${orderId}, got ${ev.payload?.orderId}`);
    }
    const observedStatuses = new Set(statusEvents.map(e => e.payload?.status));
    assert(observedStatuses.has('READY_FOR_TAKEOFF'), `Expected StatusChanged to include READY_FOR_TAKEOFF; got ${[...observedStatuses].join(', ')}`);
    assert(observedStatuses.has('IN_TRANSIT'), `Expected StatusChanged to include IN_TRANSIT; got ${[...observedStatuses].join(', ')}`);

    // 11. Strict assertions on HTTP fallback poll
    assert(fallbackPolls.length >= 1, 'Expected at least one HTTP fallback poll');
    const fb = fallbackPolls[0];
    assert(fb.status === 200, `Expected HTTP fallback status 200, got ${fb.status}`);
    assert(fb.returnedOrderId === orderId, `HTTP fallback orderId mismatch: expected ${orderId}, got ${fb.returnedOrderId}`);
    assert(fb.returnedStatus === 'IN_TRANSIT', `Expected HTTP fallback status IN_TRANSIT, got ${fb.returnedStatus}`);
    assert(fb.hasValidLastPosition === true, 'Expected HTTP fallback tracking snapshot to include finite lastPosition.latitude and lastPosition.longitude');
    assert(fb.hasPickupSecureCode === false, 'HTTP fallback tracking response must not expose pickupSecureCode');

    // 12. Negative tests for HTTP fallback (unauthenticated & non-owned/unknown orderId)
    const unauthFallbackRes = await api(`/api/delivery-orders/${orderId}/tracking`, 'GET', null);
    assert(unauthFallbackRes.status === 401, `Expected 401 for unauthenticated fallback poll, got ${unauthFallbackRes.status}`);

    const wrongOrderId = '00000000-0000-0000-0000-000000000000';
    const wrongOrderFallbackRes = await api(`/api/delivery-orders/${wrongOrderId}/tracking`, 'GET', custToken);
    assert(
      wrongOrderFallbackRes.status === 403 || wrongOrderFallbackRes.status === 404,
      `Expected 403/404 for unknown/non-owned order fallback poll, got ${wrongOrderFallbackRes.status}`,
    );
    log(`Negative HTTP fallback checks passed: unauthenticated=${unauthFallbackRes.status}, wrongOrderId=${wrongOrderFallbackRes.status}.`);

    report.steps.push({
      step: 'status_and_telemetry_over_websocket',
      approveStatus: approveRes.status,
      scheduleStatus: schedRes.status,
      intakeStatus: intakeRes.status,
      statusEventsCount: statusEvents.length,
      observedStatuses: [...observedStatuses],
      statusEventsSample: statusEvents.slice(0, 4),
      initialPositionEventsCount: positionEvents.filter(e => e.phase === 'initial').length,
      initialPositionSample: positionEvents.find(e => e.phase === 'initial')?.payload,
    });
    report.steps.push({
      step: 'automatic_reconnect_and_resubscribe',
      lifecycle,
      postReconnectPositionEventsCount: positionEvents.filter(e => e.phase === 'after_reconnect').length,
      postReconnectPositionSample: positionEvents.find(
        e => e.phase === 'after_reconnect' && Date.parse(e.payload?.timestamp) > Date.parse(initialTs),
      )?.payload,
    });
    report.steps.push({
      step: 'automatic_http_fallback_on_ws_outage',
      fallbackPolls,
      negativeChecks: {
        unauthenticatedStatus: unauthFallbackRes.status,
        wrongOrderIdStatus: wrongOrderFallbackRes.status,
      },
    });

    console.log(JSON.stringify(report, null, 2));
  } finally {
    proxy.server.close();
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
