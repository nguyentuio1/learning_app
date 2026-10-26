import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../services/address_api_service.dart';

final addressApiServiceProvider = Provider<AddressApiService>(
  (ref) => AddressApiService(ref.watch(dioProvider)),
  name: 'addressApiService',
);
