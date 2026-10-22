import 'delivery_order.dart';

class DeliveryOrderPageResponse {
  const DeliveryOrderPageResponse({
    required this.items,
    required this.page,
    required this.pageSize,
    this.totalCount,
    this.totalPages,
  });

  final List<DeliveryOrder> items;
  final int page;
  final int pageSize;
  final int? totalCount;
  final int? totalPages;

  bool get hasMore {
    if (totalPages != null) return page < totalPages!;
    if (totalCount != null) return page * pageSize < totalCount!;
    return items.length == pageSize;
  }
}
