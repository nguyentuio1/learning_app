import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'token_storage.dart';
import 'user_storage.dart';

export 'auth_tokens.dart';
export 'token_storage.dart';
export 'user_storage.dart';

final tokenStorageProvider = Provider<TokenStorage>(
  (ref) => SecureTokenStorage(),
  name: 'tokenStorage',
);

final userStorageProvider = Provider<UserStorage>(
  (ref) => SecureUserStorage(),
  name: 'userStorage',
);
