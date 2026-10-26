import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/providers/delivery_order_providers.dart';

final orderDetailProvider = FutureProvider.autoDispose
    .family<DeliveryOrderDetailResponseDto, String>(
      (ref, id) =>
          ref.watch(deliveryOrderApiServiceProvider).getOrderDetail(id),
      name: 'orderDetail',
    );
