import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/customer_address.dart';
import 'package:smart_drone_delivery/data/providers/address_providers.dart';
import 'package:smart_drone_delivery/data/services/address_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/addresses/address_notifier.dart';

class _FakeAddressApiService extends AddressApiService {
  _FakeAddressApiService() : super(Dio());

  final List<CustomerAddress> addresses = [_address()];
  final List<String> calls = [];
  Object? getAddressesError;
  Object? mutationError;

  @override
  Future<List<CustomerAddress>> getAddresses() async {
    calls.add('get');
    if (getAddressesError case final error?) throw error;
    return List.unmodifiable(addresses);
  }

  @override
  Future<void> createAddress(CustomerAddressRequest address) async {
    calls.add('create');
    if (mutationError case final error?) throw error;
    addresses.add(_address(id: 'created', request: address));
  }

  @override
  Future<void> updateAddress(String id, CustomerAddressRequest address) async {
    calls.add('update:$id');
    if (mutationError case final error?) throw error;
    final index = addresses.indexWhere((item) => item.id == id);
    if (index < 0) {
      throw const ApiException(message: 'Address not found.', statusCode: 404);
    }
    addresses[index] = _address(id: id, request: address);
  }

  @override
  Future<void> deleteAddress(String id) async {
    calls.add('delete:$id');
    if (mutationError case final error?) throw error;
    if (!addresses.any((item) => item.id == id)) {
      throw const ApiException(message: 'Address not found.', statusCode: 404);
    }
    addresses.removeWhere((address) => address.id == id);
  }

  @override
  Future<void> setDefaultAddress(String id) async {
    calls.add('default:$id');
    if (mutationError case final error?) throw error;
    if (!addresses.any((item) => item.id == id)) {
      throw const ApiException(message: 'Address not found.', statusCode: 404);
    }
    for (var index = 0; index < addresses.length; index++) {
      addresses[index] = _address(
        id: addresses[index].id,
        label: addresses[index].label,
        isDefault: addresses[index].id == id,
      );
    }
  }
}

CustomerAddress _address({
  String id = 'address-1',
  String label = 'Home',
  bool isDefault = true,
  CustomerAddressRequest? request,
}) => CustomerAddress(
  id: id,
  label: request?.label ?? label,
  contactName: request?.contactName ?? 'Mai Tran',
  contactPhone: request?.contactPhone ?? '0901234567',
  addressLine: request?.addressLine ?? '12 Main Street',
  latitude: request?.latitude,
  longitude: request?.longitude,
  isDefault: request?.isDefault ?? isDefault,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-02T00:00:00Z',
);

void main() {
  late _FakeAddressApiService service;
  late ProviderContainer container;

  setUp(() {
    service = _FakeAddressApiService();
    container = ProviderContainer(
      overrides: [addressApiServiceProvider.overrideWithValue(service)],
      retry: (_, _) => null,
    );
  });

  tearDown(() => container.dispose());

  test('address JSON uses server fields and omits server-owned metadata', () {
    final json = _address().toJson();

    expect(json.keys, {
      'label',
      'contactName',
      'contactPhone',
      'addressLine',
      'latitude',
      'longitude',
      'isDefault',
    });
    expect(json.containsKey('id'), isFalse);
    expect(json.containsKey('createdAt'), isFalse);
    expect(json.containsKey('updatedAt'), isFalse);
    expect(
      CustomerAddress.fromJson({
        ...json,
        'id': 'address-1',
        'createdAt': '2026-01-01T00:00:00Z',
        'updatedAt': null,
      }).createdAt,
      '2026-01-01T00:00:00Z',
    );
  });

  test('loads addresses into AsyncData', () async {
    final addresses = await container.read(addressNotifierProvider.future);

    expect(addresses, hasLength(1));
    expect(addresses.single.label, 'Home');
    expect(container.read(addressNotifierProvider).hasValue, isTrue);
    expect(service.calls, ['get']);
  });

  test('keeps API 404 details for an address the user does not own', () async {
    service.getAddressesError = const ApiException(
      message: 'Address not found.',
      statusCode: 404,
      code: 'address_not_found',
    );

    await expectLater(
      container.read(addressNotifierProvider.future),
      throwsA(
        isA<ApiException>()
            .having((error) => error.statusCode, 'statusCode', 404)
            .having((error) => error.message, 'message', 'Address not found.'),
      ),
    );
    expect(container.read(addressNotifierProvider).hasError, isTrue);
  });

  test(
    'preserves 404 when mutating an address outside the owner scope',
    () async {
      await container.read(addressNotifierProvider.future);
      const request = CustomerAddressRequest(
        label: 'Office',
        contactName: 'Mai Tran',
        contactPhone: '0901234567',
        addressLine: '20 Office Road',
        isDefault: false,
      );

      await container
          .read(addressNotifierProvider.notifier)
          .updateAddress('not-owned', request);

      final error = container.read(addressNotifierProvider).error;
      expect(error, isA<ApiException>());
      expect((error! as ApiException).statusCode, 404);
      expect((error as ApiException).message, 'Address not found.');
      expect(service.calls, ['get', 'update:not-owned']);
    },
  );

  test('creates an address then reloads the list', () async {
    await container.read(addressNotifierProvider.future);
    const request = CustomerAddressRequest(
      label: 'Office',
      contactName: 'Mai Tran',
      contactPhone: '0901234567',
      addressLine: '20 Office Road',
      isDefault: false,
    );

    await container
        .read(addressNotifierProvider.notifier)
        .createAddress(request);

    final state = container.read(addressNotifierProvider);
    expect(service.calls, ['get', 'create', 'get']);
    expect(state.requireValue.map((address) => address.id), [
      'address-1',
      'created',
    ]);
  });

  test('updates, sets default, and deletes before reloading', () async {
    service.addresses.add(_address(id: 'address-2', label: 'Office'));
    await container.read(addressNotifierProvider.future);
    const request = CustomerAddressRequest(
      label: 'Updated',
      contactName: 'Mai Tran',
      contactPhone: '0901234567',
      addressLine: 'New Street',
      isDefault: true,
    );
    final notifier = container.read(addressNotifierProvider.notifier);

    await notifier.updateAddress('address-1', request);
    await notifier.setDefaultAddress('address-2');

    final afterSetDefault = container
        .read(addressNotifierProvider)
        .requireValue;
    expect(
      afterSetDefault.singleWhere((item) => item.id == 'address-2').isDefault,
      isTrue,
    );
    expect(
      afterSetDefault.singleWhere((item) => item.id == 'address-1').isDefault,
      isFalse,
    );

    await notifier.deleteAddress('address-1');

    expect(service.calls, [
      'get',
      'update:address-1',
      'get',
      'default:address-2',
      'get',
      'delete:address-1',
      'get',
    ]);
    expect(
      container.read(addressNotifierProvider).requireValue.single.id,
      'address-2',
    );
  });

  test('mutation errors retain RFC 7807 status and detail', () async {
    await container.read(addressNotifierProvider.future);
    service.mutationError = const ApiException(
      message: 'You have reached the limit of 10 addresses.',
      statusCode: 409,
      code: 'address_limit_reached',
    );
    const request = CustomerAddressRequest(
      label: 'Office',
      contactName: 'Mai Tran',
      contactPhone: '0901234567',
      addressLine: '20 Office Road',
      isDefault: false,
    );

    await container
        .read(addressNotifierProvider.notifier)
        .createAddress(request);

    final error = container.read(addressNotifierProvider).error;
    expect(error, isA<ApiException>());
    expect((error! as ApiException).statusCode, 409);
    expect(
      (error as ApiException).message,
      'You have reached the limit of 10 addresses.',
    );
    expect(service.calls, ['get', 'create']);
  });
}
