import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/datasources/remote/delivery_map_signalr_service.dart';

class _FakeHubConnection implements DeliveryMapHubConnection {
  final handlers = <String, void Function(List<Object?>?)>{};
  final invocations = <(String, List<Object>?)>[];
  void Function()? reconnecting;
  void Function()? reconnected;
  void Function()? closed;
  var started = false;
  var stopped = false;
  var handlersRegisteredAtStart = false;

  @override
  void on(String methodName, void Function(List<Object?>? arguments) handler) {
    handlers[methodName] = handler;
  }

  @override
  void onclose(void Function() callback) => closed = callback;

  @override
  void onreconnected(void Function() callback) => reconnected = callback;

  @override
  void onreconnecting(void Function() callback) => reconnecting = callback;

  @override
  Future<void> start() async {
    handlersRegisteredAtStart =
        handlers.containsKey('PositionUpdated') &&
        handlers.containsKey('StatusChanged');
    started = true;
  }

  @override
  Future<void> invoke(String methodName, {List<Object>? args}) async {
    invocations.add((methodName, args));
  }

  @override
  Future<void> stop() async {
    stopped = true;
  }
}

void main() {
  test(
    'registers events before start and subscribes the selected order',
    () async {
      final connection = _FakeHubConnection();
      final service = DeliveryMapSignalRService(
        hubUrl: 'https://example.test/hubs/tracking',
        accessTokenFactory: () async => 'test-token',
        connectionFactory: () => connection,
      );
      final events = <DeliveryMapSignalREvent>[];
      final subscription = service.watchOrderMap('order-1').listen(events.add);
      await Future<void>.delayed(Duration.zero);

      expect(connection.handlersRegisteredAtStart, isTrue);
      expect(connection.started, isTrue);
      expect(connection.invocations.first.$1, 'Subscribe');
      expect(connection.invocations.first.$2, ['order-1']);
      expect(events.last.isConnected, isTrue);

      await subscription.cancel();
      expect(connection.invocations.last, ('Unsubscribe', null));
      expect(connection.stopped, isTrue);
    },
  );

  test('forwards position/status and resubscribes after reconnect', () async {
    final connection = _FakeHubConnection();
    final service = DeliveryMapSignalRService(
      hubUrl: 'https://example.test/hubs/tracking',
      accessTokenFactory: () async => 'test-token',
      connectionFactory: () => connection,
    );
    final events = <DeliveryMapSignalREvent>[];
    final subscription = service.watchOrderMap('order-1').listen(events.add);
    await Future<void>.delayed(Duration.zero);
    connection.handlers['PositionUpdated']!([
      {
        'latitude': 10.8,
        'longitude': 106.7,
        'altitudeMeters': 70,
        'speedMps': 10,
        'batteryPercentage': 80,
        'timestamp': '2026-11-15T08:10:05Z',
      },
    ]);
    connection.handlers['StatusChanged']!([
      {'orderId': 'order-1', 'status': 'ARRIVED'},
    ]);
    connection.reconnecting!();
    connection.reconnected!();
    await Future<void>.delayed(Duration.zero);

    expect(events.any((event) => event.position?.speedMps == 10), isTrue);
    expect(events.any((event) => event.orderId == 'order-1'), isTrue);
    expect(events.any((event) => event.isConnected == false), isTrue);
    expect(
      connection.invocations.where((call) => call.$1 == 'Subscribe'),
      hasLength(2),
    );
    expect(events.last.isConnected, isTrue);

    await subscription.cancel();
  });
}
