import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../models/delivery_order.dart';
import '../services/delivery_order_api_service.dart';

final deliveryOrderApiServiceProvider = Provider<DeliveryOrderApiService>(
  (ref) => DeliveryOrderApiService(ref.watch(dioProvider)),
  name: 'deliveryOrderApiService',
);

final myOrdersProvider = FutureProvider.autoDispose<List<DeliveryOrder>>(
  (ref) => ref.watch(deliveryOrderApiServiceProvider).getMyOrders(),
  name: 'myOrders',
);
