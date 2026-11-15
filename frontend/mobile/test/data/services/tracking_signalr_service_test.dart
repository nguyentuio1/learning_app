import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/network/network_config.dart';
import 'package:smart_drone_delivery/core/storage/token_storage.dart';
import 'package:smart_drone_delivery/data/datasources/remote/tracking_signalr_service.dart';

class _TokenStorage implements TokenStorage {
  @override
  Future<AuthTokens?> read() async =>
      const AuthTokens(accessToken: 'tracking-jwt', refreshToken: 'refresh');

  @override
  Future<void> clear() async {}

  @override
  Future<void> write(AuthTokens tokens) async {}
}

class _FakeHubFactory implements TrackingHubConnectionFactory {
  final connection = _FakeHubConnection();
  String? url;
  Future<String> Function()? tokenFactory;

  @override
  TrackingHubConnection create(
    String url,
    Future<String> Function() accessTokenFactory,
  ) {
    this.url = url;
    tokenFactory = accessTokenFactory;
    return connection;
  }
}

class _FakeHubConnection implements TrackingHubConnection {
  final handlers = <String, void Function(List<Object?>?)>{};
  final calls = <String>[];
  void Function()? reconnecting;
  void Function()? closed;
  void Function()? reconnected;
  @override
  bool isConnected = false;
  bool listenersRegisteredBeforeStart = false;
  int stopCalls = 0;

  @override
  void on(String event, void Function(List<Object?>? arguments) handler) {
    handlers[event] = handler;
  }

  @override
  void onReconnecting(void Function() callback) => reconnecting = callback;

  @override
  void onClose(void Function() callback) => closed = callback;

  @override
  void onReconnected(void Function() callback) => reconnected = callback;

  @override
  Future<void> start() async {
    listenersRegisteredBeforeStart = handlers.keys.toSet().containsAll({
      'PositionUpdated',
      'StatusChanged',
    });
    isConnected = true;
  }

  @override
  Future<Object?> invoke(String method, {List<Object>? args}) async {
    calls.add(method);
    return null;
  }

  @override
  Future<void> stop() async {
    stopCalls++;
    isConnected = false;
  }
}

void main() {
  test('builds hub URL from API base URL without the /api suffix', () {
    expect(
      TrackingSignalRService.trackingHubUrl(
        'https://delivery.example.test/api/',
      ),
      'https://delivery.example.test/hubs/tracking',
    );
  });

  test(
    'registers event handlers before connect and subscribes with JWT',
    () async {
      final factory = _FakeHubFactory();
      final service = TrackingSignalRService(
        networkConfig: const NetworkConfig(
          baseUrl: 'https://delivery.example.test/api',
        ),
        tokenStorage: _TokenStorage(),
        hubConnectionFactory: factory,
      );
      final states = <bool>[];
      final subscription = service.connectionStates.listen(states.add);
      addTearDown(subscription.cancel);

      await service.subscribe('order-1');

      expect(factory.connection.listenersRegisteredBeforeStart, isTrue);
      expect(factory.url, 'https://delivery.example.test/hubs/tracking');
      expect(await factory.tokenFactory!(), 'tracking-jwt');
      expect(factory.connection.calls, ['Subscribe']);
      expect(states, [true]);
      await service.dispose();
      expect(factory.connection.calls, ['Subscribe', 'Unsubscribe']);
      expect(factory.connection.stopCalls, 1);
    },
  );

  test(
    'forwards telemetry and status events and re-subscribes on reconnect',
    () async {
      final factory = _FakeHubFactory();
      final service = TrackingSignalRService(
        networkConfig: const NetworkConfig(
          baseUrl: 'https://delivery.example.test/api',
        ),
        tokenStorage: _TokenStorage(),
        hubConnectionFactory: factory,
      );
      final positions = <dynamic>[];
      final statuses = <dynamic>[];
      final connectionStates = <bool>[];
      final listeners = [
        service.positions.listen(positions.add),
        service.statusUpdates.listen(statuses.add),
        service.connectionStates.listen(connectionStates.add),
      ];
      addTearDown(
        () => Future.wait(listeners.map((listener) => listener.cancel())),
      );

      await service.subscribe('order-1');
      factory.connection.handlers['PositionUpdated']!([
        {
          'latitude': 10.8,
          'longitude': 106.7,
          'altitudeMeters': 80,
          'speedMps': 11.5,
          'batteryPercentage': 71,
          'timestamp': '2026-11-12T08:10:05Z',
        },
      ]);
      factory.connection.handlers['StatusChanged']!([
        {
          'orderId': 'order-1',
          'trackingNumber': 'DD-202611-0012',
          'status': 'ARRIVED',
          'updatedAt': '2026-11-12T08:20:00Z',
        },
      ]);
      await Future<void>.delayed(Duration.zero);

      expect(positions.single.speedMps, 11.5);
      expect(statuses.single.status.apiValue, 'ARRIVED');

      factory.connection.reconnecting!();
      factory.connection.reconnected!();
      await Future<void>.delayed(Duration.zero);
      expect(connectionStates, [true, false, true]);
      expect(factory.connection.calls, ['Subscribe', 'Subscribe']);
      await service.dispose();
    },
  );
}
