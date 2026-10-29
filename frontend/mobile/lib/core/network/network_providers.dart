import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../config/app_config.dart';
import '../session/auth_controller.dart';
import '../storage/storage_providers.dart';
import 'dio_client.dart';
import 'network_config.dart';

/// Test / môi trường đặc biệt: `networkConfigProvider.overrideWithValue(NetworkConfig(...))`.
final networkConfigProvider = Provider<NetworkConfig>(
  (ref) => NetworkConfig.fromApp(ref.watch(appConfigProvider)),
  name: 'networkConfig',
);

final dioProvider = Provider<Dio>((ref) {
  final dio = DioClient.create(
    config: ref.watch(appConfigProvider),
    network: ref.watch(networkConfigProvider),
    storage: ref.watch(tokenStorageProvider),
    // read() lazily to avoid a provider cycle
    onSessionExpired: () =>
        ref.read(authControllerProvider.notifier).onSessionExpired(),
  );
  ref.onDispose(dio.close);
  return dio;
}, name: 'dio');
