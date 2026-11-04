import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/package_model.dart';
import '../../../data/providers/package_providers.dart';
import '../../../data/services/package_api_service.dart';

class PackageNotifier extends AsyncNotifier<List<PackageModel>> {
  @override
  FutureOr<List<PackageModel>> build() =>
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
      _runMutation((service) => service.createPackage(package), package);

  Future<void> updatePackage(String id, PackageRequest package) =>
      _runMutation((service) => service.updatePackage(id, package), package);

  Future<void> deletePackage(String id) =>
      _runMutation((service) => service.deletePackage(id), null);

  Future<void> _runMutation(
    Future<void> Function(PackageApiService service) mutation,
    PackageRequest? request,
  ) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      if (request != null) _validatePackageRequest(request);
      final service = ref.read(packageApiServiceProvider);
      await mutation(service);
      return service.getPackages();
    });
  }

  void _validatePackageRequest(PackageRequest request) {
    final name = request.packageName.trim();
    if (name.isEmpty) {
      throw const ApiException(
        message: 'Package name is required.',
        statusCode: 400,
      );
    }
    if (name.length > 200) {
      throw const ApiException(
        message: 'Package name must be at most 200 characters.',
        statusCode: 400,
      );
    }

    _validateMeasurement('Weight', request.weightGram, 3000, 'g');
    _validateMeasurement('Length', request.lengthCm, 30, 'cm');
    _validateMeasurement('Width', request.widthCm, 20, 'cm');
    _validateMeasurement('Height', request.heightCm, 15, 'cm');

    final imageUrl = request.packageImageUrl;
    if (imageUrl != null) {
      final uri = Uri.tryParse(imageUrl);
      if (uri == null ||
          !uri.hasScheme ||
          !const {'http', 'https'}.contains(uri.scheme) ||
          uri.host.isEmpty) {
        throw const ApiException(
          message: 'Enter a valid public image URL.',
          statusCode: 400,
        );
      }
    }
  }

  void _validateMeasurement(
    String name,
    double value,
    double maximum,
    String unit,
  ) {
    if (!value.isFinite || value <= 0 || value > maximum) {
      throw ApiException(
        message: '$name must be greater than 0 and at most $maximum $unit.',
        statusCode: 400,
      );
    }
  }
}

final packageNotifierProvider =
    AsyncNotifierProvider.autoDispose<PackageNotifier, List<PackageModel>>(
      PackageNotifier.new,
      name: 'packageNotifier',
    );
