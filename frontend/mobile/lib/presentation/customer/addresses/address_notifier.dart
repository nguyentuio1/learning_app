import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/models/customer_address.dart';
import '../../../data/providers/address_providers.dart';
import '../../../data/services/address_api_service.dart';

class AddressNotifier extends AsyncNotifier<List<CustomerAddress>> {
  @override
  FutureOr<List<CustomerAddress>> build() =>
      ref.watch(addressApiServiceProvider).getAddresses();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(addressApiServiceProvider).getAddresses(),
    );
  }

  Future<void> createAddress(CustomerAddressRequest address) =>
      _runMutation((service) => service.createAddress(address));

  Future<void> updateAddress(String id, CustomerAddressRequest address) =>
      _runMutation((service) => service.updateAddress(id, address));

  Future<void> deleteAddress(String id) =>
      _runMutation((service) => service.deleteAddress(id));

  Future<void> setDefaultAddress(String id) =>
      _runMutation((service) => service.setDefaultAddress(id));

  Future<void> _runMutation(
    Future<void> Function(AddressApiService service) mutation,
  ) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final service = ref.read(addressApiServiceProvider);
      await mutation(service);
      return service.getAddresses();
    });
  }
}

final addressNotifierProvider =
    AsyncNotifierProvider.autoDispose<AddressNotifier, List<CustomerAddress>>(
      AddressNotifier.new,
      name: 'addressNotifier',
    );
