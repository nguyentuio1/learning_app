const deliveryOrderStatuses = [
  'ALL',
  'PENDING',
  'APPROVED',
  'READY_FOR_TAKEOFF',
  'IN_TRANSIT',
  'ARRIVED',
  'DELIVERED',
  'CANCELLED',
  'FAILED',
];

class DeliveryOrderFilterParams {
  const DeliveryOrderFilterParams({
    this.page = 1,
    this.pageSize = 20,
    this.status = 'ALL',
    this.fromDate,
    this.toDate,
    this.searchKeyword,
  });

  final int page;
  final int pageSize;
  final String? status;
  final String? fromDate;
  final String? toDate;
  final String? searchKeyword;

  DeliveryOrderFilterParams copyWith({
    int? page,
    int? pageSize,
    String? status,
    bool clearStatus = false,
    String? fromDate,
    bool clearFromDate = false,
    String? toDate,
    bool clearToDate = false,
    String? searchKeyword,
    bool clearSearchKeyword = false,
  }) => DeliveryOrderFilterParams(
    page: page ?? this.page,
    pageSize: pageSize ?? this.pageSize,
    status: clearStatus ? null : status ?? this.status,
    fromDate: clearFromDate ? null : fromDate ?? this.fromDate,
    toDate: clearToDate ? null : toDate ?? this.toDate,
    searchKeyword: clearSearchKeyword
        ? null
        : searchKeyword ?? this.searchKeyword,
  );

  Map<String, dynamic> toQueryParameters() => {
    'page': page,
    'pageSize': pageSize,
    if (status != null && status != 'ALL') 'status': status,
    if (fromDate != null) 'fromDate': fromDate,
    if (toDate != null) 'toDate': toDate,
    if (searchKeyword != null && searchKeyword!.trim().isNotEmpty)
      'searchKeyword': searchKeyword!.trim(),
  };
}
