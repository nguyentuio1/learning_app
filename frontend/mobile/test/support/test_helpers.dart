import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:smart_drone_delivery/core/storage/token_storage.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

/// Tạo ProviderContainer cho test và tự dispose khi test kết thúc.
ProviderContainer createContainer({List<Override> overrides = const []}) {
  final c = ProviderContainer(overrides: overrides, retry: (_, _) => null);
  addTearDown(c.dispose);
  return c;
}

/// Thay SecureTokenStorage (cần Keychain/Keystore) bằng bản trong bộ nhớ.
class InMemoryTokenStorage implements TokenStorage {
  AuthTokens? _tokens;
  @override
  Future<AuthTokens?> read() async => _tokens;
  @override
  Future<void> write(AuthTokens tokens) async => _tokens = tokens;
  @override
  Future<void> clear() async => _tokens = null;
}
