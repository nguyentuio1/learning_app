import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../models/failure_models.dart';
import '../models/paged_result.dart';
import '../services/failure_api_service.dart';

final failureApiServiceProvider = Provider<FailureApiService>(
  (ref) => FailureApiService(ref.watch(dioProvider)),
  name: 'failureApiService',
);

final failureOrderQueryProvider =
    NotifierProvider<FailureOrderQueryNotifier, FailureOrderQuery>(
      FailureOrderQueryNotifier.new,
      name: 'failureOrderQuery',
    );

class FailureOrderQueryNotifier extends Notifier<FailureOrderQuery> {
  @override
  FailureOrderQuery build() => const FailureOrderQuery();

  void update({
    int? page,
    int? pageSize,
    String? failureType,
    String? resolutionStatus,
    String? searchKeyword,
    String? fromDate,
    String? toDate,
    bool clearFailureType = false,
    bool clearResolutionStatus = false,
    bool clearSearchKeyword = false,
    bool clearFromDate = false,
    bool clearToDate = false,
  }) {
    state = FailureOrderQuery(
      page: page ?? 1,
      pageSize: pageSize ?? state.pageSize,
      failureType: clearFailureType ? null : failureType ?? state.failureType,
      resolutionStatus: clearResolutionStatus
          ? null
          : resolutionStatus ?? state.resolutionStatus,
      searchKeyword: clearSearchKeyword
          ? null
          : searchKeyword ?? state.searchKeyword,
      fromDate: clearFromDate ? null : fromDate ?? state.fromDate,
      toDate: clearToDate ? null : toDate ?? state.toDate,
    );
  }
}

final failedOrdersProvider =
    FutureProvider.autoDispose<PagedResult<FailedOrderSummary>>((ref) {
      final query = ref.watch(failureOrderQueryProvider);
      return ref.watch(failureApiServiceProvider).getFailedOrders(query);
    }, name: 'failedOrders');

final failedOrderDetailProvider = FutureProvider.autoDispose
    .family<FailureOrderDetail, String>(
      (ref, orderId) =>
          ref.watch(failureApiServiceProvider).getFailureDetail(orderId),
      name: 'failedOrderDetail',
    );
