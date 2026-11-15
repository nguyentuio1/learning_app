import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

import '../../models/delivery_map_snapshot_model.dart';

class DeliveryMapSignalREvent {
  const DeliveryMapSignalREvent.position(this.position)
    : orderId = null,
      isConnected = null;
  const DeliveryMapSignalREvent.status(this.orderId)
    : position = null,
      isConnected = null;
  const DeliveryMapSignalREvent.connection(this.isConnected)
    : position = null,
      orderId = null;

  final DroneMapTelemetryModel? position;
  final String? orderId;
  final bool? isConnected;
}

abstract interface class DeliveryMapHubConnection {
  void on(String methodName, void Function(List<Object?>? arguments) handler);
  void onreconnecting(void Function() callback);
  void onreconnected(void Function() callback);
  void onclose(void Function() callback);
  Future<void> start();
  Future<void> invoke(String methodName, {List<Object>? args});
  Future<void> stop();
}

class DeliveryMapSignalRService {
  DeliveryMapSignalRService({
    required this.hubUrl,
    required this.accessTokenFactory,
    this.connectionFactory,
    this.reconnectDelay = const Duration(seconds: 5),
  });

  final String hubUrl;
  final Future<String> Function() accessTokenFactory;
  final DeliveryMapHubConnection Function()? connectionFactory;
  final Duration reconnectDelay;

  DeliveryMapHubConnection? _connection;
  StreamController<DeliveryMapSignalREvent>? _events;
  bool _closed = false;
  bool _connected = false;

  Stream<DeliveryMapSignalREvent> watchOrderMap(String orderId) {
    final events = StreamController<DeliveryMapSignalREvent>.broadcast();
    final connection =
        connectionFactory?.call() ??
        _buildConnection(
          hubUrl: hubUrl,
          accessTokenFactory: accessTokenFactory,
        );
    _events = events;
    _connection = connection;
    events.onCancel = close;

    connection.on('PositionUpdated', (arguments) {
      if (arguments == null || arguments.isEmpty) return;
      try {
        final value = arguments.first;
        if (value is Map) {
          events.add(
            DeliveryMapSignalREvent.position(
              DroneMapTelemetryModel.fromJson(Map<String, dynamic>.from(value)),
            ),
          );
        }
      } on FormatException catch (error, stackTrace) {
        events.addError(error, stackTrace);
      }
    });
    connection.on('StatusChanged', (arguments) {
      if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
        return;
      }
      final payload = Map<String, dynamic>.from(arguments.first as Map);
      final changedOrderId = payload['orderId'];
      if (changedOrderId is String) {
        events.add(DeliveryMapSignalREvent.status(changedOrderId));
      }
    });
    connection.onreconnecting(() {
      _connected = false;
      events.add(const DeliveryMapSignalREvent.connection(false));
    });
    connection.onreconnected(() {
      unawaited(_resubscribe(connection, orderId, events));
    });
    connection.onclose(() {
      if (!_closed) {
        _connected = false;
        events.add(const DeliveryMapSignalREvent.connection(false));
        unawaited(_connect(connection, orderId, events));
      }
    });
    unawaited(_connect(connection, orderId, events));
    return events.stream;
  }

  Future<void> _connect(
    DeliveryMapHubConnection connection,
    String orderId,
    StreamController<DeliveryMapSignalREvent> events,
  ) async {
    while (!_closed && !_connected) {
      try {
        await connection.start();
        await connection.invoke('Subscribe', args: [orderId]);
        _connected = true;
        events.add(const DeliveryMapSignalREvent.connection(true));
      } on Object catch (error, stackTrace) {
        if (_closed) return;
        events.add(const DeliveryMapSignalREvent.connection(false));
        events.addError(error, stackTrace);
        await Future<void>.delayed(reconnectDelay);
      }
    }
  }

  Future<void> _resubscribe(
    DeliveryMapHubConnection connection,
    String orderId,
    StreamController<DeliveryMapSignalREvent> events,
  ) async {
    try {
      await connection.invoke('Subscribe', args: [orderId]);
      _connected = true;
      events.add(const DeliveryMapSignalREvent.connection(true));
    } on Object catch (error, stackTrace) {
      _connected = false;
      events.add(const DeliveryMapSignalREvent.connection(false));
      events.addError(error, stackTrace);
    }
  }

  Future<void> close() async {
    if (_closed) return;
    _closed = true;
    final connection = _connection;
    try {
      if (connection != null && _connected) {
        await connection.invoke('Unsubscribe');
      }
    } finally {
      if (connection != null) await connection.stop();
      final events = _events;
      if (events != null && !events.isClosed) await events.close();
    }
  }

  static DeliveryMapHubConnection _buildConnection({
    required String hubUrl,
    required Future<String> Function() accessTokenFactory,
  }) => _SignalRHubConnection(
    HubConnectionBuilder()
        .withUrl(
          hubUrl,
          options: HttpConnectionOptions(
            accessTokenFactory: accessTokenFactory,
            logMessageContent: false,
          ),
        )
        .withAutomaticReconnect()
        .build(),
  );
}

class _SignalRHubConnection implements DeliveryMapHubConnection {
  const _SignalRHubConnection(this._connection);

  final HubConnection _connection;

  @override
  void on(String methodName, void Function(List<Object?>? arguments) handler) =>
      _connection.on(methodName, handler);

  @override
  void onreconnecting(void Function() callback) =>
      _connection.onreconnecting(({error}) => callback());

  @override
  void onreconnected(void Function() callback) =>
      _connection.onreconnected(({connectionId}) => callback());

  @override
  void onclose(void Function() callback) =>
      _connection.onclose(({error}) => callback());

  @override
  Future<void> start() async {
    await _connection.start();
  }

  @override
  Future<void> invoke(String methodName, {List<Object>? args}) async {
    await _connection.invoke(methodName, args: args);
  }

  @override
  Future<void> stop() => _connection.stop();
}
