import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

import '../../../core/network/network_config.dart';
import '../../../core/storage/token_storage.dart';
import '../../../domain/entities/telemetry_position.dart';
import '../../../domain/entities/tracking_snapshot.dart';
import '../../../domain/entities/tracking_status_update.dart';
import '../../models/telemetry_position_model.dart';

class TrackingSignalRService {
  TrackingSignalRService({
    required this._networkConfig,
    required this._tokenStorage,
    TrackingHubConnectionFactory? hubConnectionFactory,
  }) : _hubConnectionFactory =
           hubConnectionFactory ?? const SignalRTrackingHubConnectionFactory();

  final NetworkConfig _networkConfig;
  final TokenStorage _tokenStorage;
  final TrackingHubConnectionFactory _hubConnectionFactory;
  final _positions = StreamController<TelemetryPosition>.broadcast();
  final _statusUpdates = StreamController<TrackingStatusUpdate>.broadcast();
  final _connectionStates = StreamController<bool>.broadcast();
  TrackingHubConnection? _connection;
  bool _disposed = false;

  Stream<TelemetryPosition> get positions => _positions.stream;
  Stream<TrackingStatusUpdate> get statusUpdates => _statusUpdates.stream;
  Stream<bool> get connectionStates => _connectionStates.stream;

  Future<void> subscribe(String orderId) async {
    if (_disposed) throw StateError('Tracking connection has been disposed.');
    if (_connection != null) await unsubscribe();

    final hub = _hubConnectionFactory.create(
      trackingHubUrl(_networkConfig.baseUrl),
      () async {
        final tokens = await _tokenStorage.read();
        if (tokens == null || tokens.accessToken.isEmpty) {
          throw StateError('No access token is available for tracking.');
        }
        return tokens.accessToken;
      },
    );
    _connection = hub;

    hub.on('PositionUpdated', _onPositionUpdated);
    hub.on('StatusChanged', _onStatusChanged);
    hub.onReconnecting(() => _emitConnectionState(false));
    hub.onClose(() => _emitConnectionState(false));
    hub.onReconnected(
      () => unawaited(_resubscribeAfterReconnect(hub, orderId)),
    );

    try {
      await hub.start();
      await hub.invoke('Subscribe', args: [orderId]);
      if (!_disposed && identical(_connection, hub)) {
        _connectionStates.add(true);
      }
    } catch (_) {
      if (!_disposed && identical(_connection, hub)) {
        _connectionStates.add(false);
      }
      rethrow;
    }
  }

  Future<void> unsubscribe() async {
    final hub = _connection;
    _connection = null;
    if (hub == null) return;
    try {
      if (hub.isConnected) {
        await hub.invoke('Unsubscribe');
      }
    } finally {
      await hub.stop();
    }
  }

  Future<void> dispose() async {
    if (_disposed) return;
    _disposed = true;
    try {
      await unsubscribe();
    } finally {
      await Future.wait([
        _positions.close(),
        _statusUpdates.close(),
        _connectionStates.close(),
      ]);
    }
  }

  Future<void> _resubscribeAfterReconnect(
    TrackingHubConnection hub,
    String orderId,
  ) async {
    try {
      await hub.invoke('Subscribe', args: [orderId]);
      if (!_disposed && identical(_connection, hub)) {
        _connectionStates.add(true);
      }
    } catch (error, stackTrace) {
      if (!_disposed && identical(_connection, hub)) {
        _connectionStates.add(false);
        _connectionStates.addError(error, stackTrace);
      }
    }
  }

  void _onPositionUpdated(List<Object?>? arguments) {
    if (_disposed) return;
    try {
      final payload = _payload(arguments);
      _positions.add(TelemetryPositionModel.fromJson(payload));
    } catch (error, stackTrace) {
      _positions.addError(error, stackTrace);
    }
  }

  void _onStatusChanged(List<Object?>? arguments) {
    if (_disposed) return;
    try {
      final payload = _payload(arguments);
      _statusUpdates.add(
        TrackingStatusUpdate(
          orderId: _requiredString(payload, 'orderId'),
          trackingNumber: _requiredString(payload, 'trackingNumber'),
          status: DeliveryTrackingStatus.parse(
            _requiredString(payload, 'status'),
          ),
          updatedAt: _requiredDate(payload, 'updatedAt'),
        ),
      );
    } catch (error, stackTrace) {
      _statusUpdates.addError(error, stackTrace);
    }
  }

  void _emitConnectionState(bool connected) {
    if (!_disposed) _connectionStates.add(connected);
  }

  static Map<String, dynamic> _payload(List<Object?>? arguments) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      throw const FormatException('Expected a SignalR event object.');
    }
    return Map<String, dynamic>.from(arguments.first as Map);
  }

  static String _requiredString(Map<String, dynamic> data, String key) {
    final value = data[key];
    if (value is! String) throw FormatException('Expected string $key.');
    return value;
  }

  static DateTime _requiredDate(Map<String, dynamic> data, String key) {
    final value = _requiredString(data, key);
    final date = DateTime.tryParse(value);
    if (date == null) throw FormatException('Invalid timestamp $key.');
    return date;
  }

  static String trackingHubUrl(String apiBaseUrl) {
    final base = Uri.parse(apiBaseUrl);
    var path = base.path.replaceFirst(RegExp(r'/+$'), '');
    if (path.endsWith('/api')) path = path.substring(0, path.length - 4);
    return base.replace(path: '$path/hubs/tracking', query: null).toString();
  }
}

abstract interface class TrackingHubConnectionFactory {
  TrackingHubConnection create(
    String url,
    Future<String> Function() accessTokenFactory,
  );
}

abstract interface class TrackingHubConnection {
  bool get isConnected;

  void on(String event, void Function(List<Object?>? arguments) handler);
  void onReconnecting(void Function() callback);
  void onClose(void Function() callback);
  void onReconnected(void Function() callback);
  Future<void> start();
  Future<Object?> invoke(String method, {List<Object>? args});
  Future<void> stop();
}

class SignalRTrackingHubConnectionFactory
    implements TrackingHubConnectionFactory {
  const SignalRTrackingHubConnectionFactory();

  @override
  TrackingHubConnection create(
    String url,
    Future<String> Function() accessTokenFactory,
  ) => _SignalRTrackingHubConnection(
    HubConnectionBuilder()
        .withUrl(
          url,
          options: HttpConnectionOptions(
            accessTokenFactory: accessTokenFactory,
          ),
        )
        .withAutomaticReconnect()
        .build(),
  );
}

class _SignalRTrackingHubConnection implements TrackingHubConnection {
  const _SignalRTrackingHubConnection(this._connection);

  final HubConnection _connection;

  @override
  bool get isConnected => _connection.state == HubConnectionState.Connected;

  @override
  void on(String event, void Function(List<Object?>? arguments) handler) =>
      _connection.on(event, handler);

  @override
  void onReconnecting(void Function() callback) =>
      _connection.onreconnecting(({error}) => callback());

  @override
  void onClose(void Function() callback) =>
      _connection.onclose(({error}) => callback());

  @override
  void onReconnected(void Function() callback) =>
      _connection.onreconnected(({connectionId}) => callback());

  @override
  Future<void> start() async {
    await _connection.start();
  }

  @override
  Future<Object?> invoke(String method, {List<Object>? args}) =>
      _connection.invoke(method, args: args);

  @override
  Future<void> stop() => _connection.stop();
}
