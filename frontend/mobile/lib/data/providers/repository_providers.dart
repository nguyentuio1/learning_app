import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../../domain/repositories/auth_repository.dart';
import '../../domain/repositories/profile_repository.dart';
import '../repositories/auth_repository_impl.dart';
import '../repositories/profile_repository_impl.dart';

/// Nơi duy nhất nối interface (domain) -> implementation (data).
/// Test / flavor: `authRepositoryProvider.overrideWithValue(Fake())`.
final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepositoryImpl(ref.watch(dioProvider)),
  name: 'authRepository',
);

final profileRepositoryProvider = Provider<ProfileRepository>(
  (ref) => ProfileRepositoryImpl(ref.watch(dioProvider)),
  name: 'profileRepository',
);

