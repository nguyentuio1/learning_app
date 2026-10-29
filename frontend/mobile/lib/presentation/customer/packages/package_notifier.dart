import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/models/package_model.dart';
import '../../../data/providers/package_providers.dart';
import '../../../data/services/package_api_service.dart';

class PackageNotifier extends AsyncNotifier<List<Package>> {
  @override
  FutureOr<List<Package>> build() =>
      ref.watch(packageApiServiceProvider).getPackages();

  Future<void> fetchPackages() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(packageApiServiceProvider).getPackages(),
    );
  }

  Future<void> refresh() async {
    await fetchPackages();
  }

  Future<void> createPackage(PackageRequest package) =>
      _runMutation((service) => service.createPackage(package));

  Future<void> updatePackage(String id, PackageRequest package) =>
      _runMutation((service) => service.updatePackage(id, package));

  Future<void> deletePackage(String id) =>
      _runMutation((service) => service.deletePackage(id));

  Future<void> _runMutation(
    Future<void> Function(PackageApiService service) mutation,
  ) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final service = ref.read(packageApiServiceProvider);
      await mutation(service);
      return service.getPackages();
    });
  }
}

final packageNotifierProvider =
    AsyncNotifierProvider.autoDispose<PackageNotifier, List<Package>>(
      PackageNotifier.new,
      name: 'packageNotifier',
    );
