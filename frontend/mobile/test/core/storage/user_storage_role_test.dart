import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/storage/user_storage.dart';
import 'package:smart_drone_delivery/data/models/user_model.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  for (final role in ['STATION_OPERATOR', 'DISPATCHER', 'ADMIN', 'CUSTOMER']) {
    test('cached $role profile retains its role through real storage serialization', () async {
      final storage = SecureUserStorage();
      await storage.write(User(
        id: 'demo-user', email: 'demo@example.invalid', fullName: 'Demo',
        phoneNumber: '0900000001', status: 'ACTIVE', roleCode: role,
        createdAt: DateTime.utc(2026, 1, 1),
      ));
      final restored = await storage.read();
      expect(restored?.id, 'demo-user');
      expect(restored?.roleCode, role);
    });
  }

  test('server role object takes precedence over a cached-role fallback', () {
    final model = UserModel.fromJson({
      'id': 'demo-user',
      'role': {'code': 'STATION_OPERATOR'},
      'roleCode': 'CUSTOMER',
    });
    expect(model.roleCode, 'STATION_OPERATOR');
  });
}
