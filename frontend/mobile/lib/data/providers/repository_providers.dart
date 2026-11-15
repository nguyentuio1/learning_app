import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../../core/storage/storage_providers.dart';
import '../../domain/repositories/auth_repository.dart';
import '../../domain/repositories/profile_repository.dart';
import '../../domain/repositories/tracking_repository.dart';
import '../datasources/remote/tracking_remote_data_source.dart';
import '../datasources/remote/tracking_signalr_service.dart';
import '../repositories/auth_repository_impl.dart';
import '../repositories/profile_repository_impl.dart';
import '../repositories/tracking_repository_impl.dart';

/// Nơi duy nhất nối interface (domain) -> implementation (data).
/// Test / flavor: `authRepositoryProvider.overrideWithValue(Fake())`.
final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepositoryImpl(
    ref.watch(dioProvider),
    ref.watch(tokenStorageProvider),
    ref.watch(userStorageProvider),
  ),
  name: 'authRepository',
);

final profileRepositoryProvider = Provider<ProfileRepository>(
  (ref) => ProfileRepositoryImpl(ref.watch(dioProvider)),
  name: 'profileRepository',
);

final trackingSignalRServiceProvider = Provider<TrackingSignalRService>((ref) {
  final service = TrackingSignalRService(
    networkConfig: ref.watch(networkConfigProvider),
    tokenStorage: ref.watch(tokenStorageProvider),
  );
  ref.onDispose(() => service.dispose());
  return service;
}, name: 'trackingSignalRService');

final trackingRepositoryProvider = Provider<TrackingRepository>(
  (ref) => TrackingRepositoryImpl(
    TrackingRemoteDataSource(ref.watch(dioProvider)),
    ref.watch(trackingSignalRServiceProvider),
  ),
  name: 'trackingRepository',
);
