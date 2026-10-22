import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../services/delivery_order_api_service.dart';

final deliveryOrderApiServiceProvider = Provider<DeliveryOrderApiService>(
  (ref) => DeliveryOrderApiService(ref.watch(dioProvider)),
  name: 'deliveryOrderApiService',
);
