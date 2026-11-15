import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../../core/storage/storage_providers.dart';
import '../../domain/repositories/auth_repository.dart';
import '../../domain/repositories/delivery_map_repository.dart';
import '../../domain/repositories/profile_repository.dart';
import '../datasources/remote/delivery_map_remote_data_source.dart';
import '../datasources/remote/delivery_map_signalr_service.dart';
import '../repositories/auth_repository_impl.dart';
import '../repositories/delivery_map_repository_impl.dart';
import '../repositories/profile_repository_impl.dart';

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

final deliveryMapRepositoryProvider = Provider<DeliveryMapRepository>((ref) {
  final dio = ref.watch(dioProvider);
  final tokenStorage = ref.watch(tokenStorageProvider);
  final baseUri = Uri.parse(dio.options.baseUrl);
  final basePath = baseUri.path.replaceFirst(RegExp(r'/api/?$'), '');
  final hubUri = baseUri.replace(path: '$basePath/hubs/tracking');

  return DeliveryMapRepositoryImpl(
    DeliveryMapRemoteDataSource(dio),
    () => DeliveryMapSignalRService(
      hubUrl: hubUri.toString(),
      accessTokenFactory: () async {
        final tokens = await tokenStorage.read();
        if (tokens == null) {
          throw StateError('Authentication token unavailable.');
        }
        return tokens.accessToken;
      },
    ),
  );
}, name: 'deliveryMapRepository');
