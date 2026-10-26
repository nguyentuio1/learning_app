import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../models/package_model.dart';
import '../services/package_api_service.dart';

final packageApiServiceProvider = Provider<PackageApiService>(
  (ref) => PackageApiService(ref.watch(dioProvider)),
  name: 'packageApiService',
);

final packageByIdProvider = FutureProvider.autoDispose.family<Package, String>(
  (ref, id) => ref.watch(packageApiServiceProvider).getPackage(id),
  name: 'packageById',
);
