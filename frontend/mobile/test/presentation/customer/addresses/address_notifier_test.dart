import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/customer_address.dart';
import 'package:smart_drone_delivery/data/providers/address_providers.dart';
import 'package:smart_drone_delivery/data/services/address_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/addresses/address_notifier.dart';

class _FakeAddressApiService extends AddressApiService {
  _FakeAddressApiService() : super(Dio());

  final List<CustomerAddress> addresses = [_address()];
  final List<String> calls = [];
  Object? getAddressesError;

  @override
  Future<List<CustomerAddress>> getAddresses() async {
    calls.add('get');
    if (getAddressesError case final error?) throw error;
    return List.unmodifiable(addresses);
  }

  @override
  Future<void> createAddress(CustomerAddressRequest address) async {
    calls.add('create');
    addresses.add(_address(id: 'created', request: address));
  }

  @override
  Future<void> updateAddress(String id, CustomerAddressRequest address) async {
    calls.add('update:$id');
  }

  @override
  Future<void> deleteAddress(String id) async {
    calls.add('delete:$id');
    addresses.removeWhere((address) => address.id == id);
  }

  @override
  Future<void> setDefaultAddress(String id) async {
    calls.add('default:$id');
  }
}

CustomerAddress _address({
  String id = 'address-1',
  CustomerAddressRequest? request,
}) => CustomerAddress(
  id: id,
  addressLabel: request?.addressLabel ?? 'Home',
  recipientName: request?.recipientName ?? 'Mai Tran',
  recipientPhone: request?.recipientPhone ?? '0901234567',
  streetAddress: request?.streetAddress ?? '12 Main Street',
  latitude: request?.latitude,
  longitude: request?.longitude,
  isDefault: request?.isDefault ?? true,
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

  test('loads addresses into AsyncData', () async {
    final addresses = await container.read(addressNotifierProvider.future);

    expect(addresses, hasLength(1));
    expect(addresses.single.addressLabel, 'Home');
    expect(container.read(addressNotifierProvider).hasValue, isTrue);
    expect(service.calls, ['get']);
  });

  test('exposes loading error from initial address request', () async {
    service.getAddressesError = StateError('failed to load');

    await expectLater(
      container.read(addressNotifierProvider.future),
      throwsA(isA<StateError>()),
    );
    expect(container.read(addressNotifierProvider).hasError, isTrue);
  });

  test('creates an address then reloads the list', () async {
    await container.read(addressNotifierProvider.future);
    const request = CustomerAddressRequest(
      addressLabel: 'Office',
      recipientName: 'Mai Tran',
      recipientPhone: '0901234567',
      streetAddress: '20 Office Road',
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

  test('updates, deletes, and sets the default before reloading', () async {
    await container.read(addressNotifierProvider.future);
    const request = CustomerAddressRequest(
      addressLabel: 'Updated',
      recipientName: 'Mai Tran',
      recipientPhone: '0901234567',
      streetAddress: 'New Street',
      isDefault: true,
    );
    final notifier = container.read(addressNotifierProvider.notifier);

    await notifier.updateAddress('address-1', request);
    await notifier.setDefaultAddress('address-1');
    await notifier.deleteAddress('address-1');

    expect(service.calls, [
      'get',
      'update:address-1',
      'get',
      'default:address-1',
      'get',
      'delete:address-1',
      'get',
    ]);
    expect(container.read(addressNotifierProvider).requireValue, isEmpty);
  });

  test('keeps mutation failures in AsyncError', () async {
    await container.read(addressNotifierProvider.future);
    service.getAddressesError = StateError('failed after mutation');

    await container
        .read(addressNotifierProvider.notifier)
        .deleteAddress('address-1');

    final state = container.read(addressNotifierProvider);
    expect(state.hasError, isTrue);
    expect(state.error, isA<StateError>());
  });
}
