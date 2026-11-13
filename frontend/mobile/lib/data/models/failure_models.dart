import 'delivery_order_detail_response_dto.dart';
import 'paged_result.dart';

class FailureOrderQuery {
  const FailureOrderQuery({
    this.page = 1,
    this.pageSize = 20,
    this.failureType,
    this.resolutionStatus,
    this.searchKeyword,
    this.fromDate,
    this.toDate,
  });

  final int page;
  final int pageSize;
  final String? failureType;
  final String? resolutionStatus;
  final String? searchKeyword;
  final String? fromDate;
  final String? toDate;

  Map<String, dynamic> toQueryParameters() => {
    'page': page,
    'pageSize': pageSize,
    if (failureType != null) 'failureType': failureType,
    if (resolutionStatus != null) 'resolutionStatus': resolutionStatus,
    if (searchKeyword != null && searchKeyword!.isNotEmpty)
      'searchKeyword': searchKeyword,
    if (fromDate != null) 'fromDate': fromDate,
    if (toDate != null) 'toDate': toDate,
  };
}

class FailedOrderSummary {
  const FailedOrderSummary({
    required this.orderId,
    required this.trackingNumber,
    required this.status,
    required this.packageName,
    required this.originStation,
    required this.destinationStation,
    required this.failureLogId,
    required this.failureType,
    required this.failedAt,
    required this.resolutionStatus,
    this.failureReason,
    this.lastResolutionAction,
    this.resolvedAt,
    this.updatedAt,
  });

  final String orderId;
  final String trackingNumber;
  final String status;
  final String packageName;
  final OrderStationDto? originStation;
  final OrderStationDto? destinationStation;
  final int failureLogId;
  final String failureType;
  final String? failureReason;
  final DateTime failedAt;
  final String resolutionStatus;
  final String? lastResolutionAction;
  final DateTime? resolvedAt;
  final DateTime? updatedAt;

  factory FailedOrderSummary.fromJson(Map<String, dynamic> json) =>
      FailedOrderSummary(
        orderId: json['orderId'] as String,
        trackingNumber: json['trackingNumber'] as String? ?? '',
        status: json['status'] as String? ?? '',
        packageName: json['packageName'] as String? ?? '',
        originStation: _nested(json['originStation'], OrderStationDto.fromJson),
        destinationStation: _nested(
          json['destinationStation'],
          OrderStationDto.fromJson,
        ),
        failureLogId: (json['failureLogId'] as num?)?.toInt() ?? 0,
        failureType: json['failureType'] as String? ?? 'UNKNOWN',
        failureReason: json['failureReason'] as String?,
        failedAt:
            _date(json['failedAt']) ??
            DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
        resolutionStatus: json['resolutionStatus'] as String? ?? 'UNRESOLVED',
        lastResolutionAction: json['lastResolutionAction'] as String?,
        resolvedAt: _date(json['resolvedAt']),
        updatedAt: _date(json['updatedAt']),
      );
}

class FailureEpisode {
  const FailureEpisode({
    required this.failureLogId,
    required this.failureType,
    required this.failedAt,
    required this.resolutionStatus,
    this.failureReason,
    this.lastResolutionAction,
    this.resolvedAt,
  });

  final int failureLogId;
  final String failureType;
  final String? failureReason;
  final DateTime failedAt;
  final String resolutionStatus;
  final String? lastResolutionAction;
  final DateTime? resolvedAt;

  factory FailureEpisode.fromJson(Map<String, dynamic> json) => FailureEpisode(
    failureLogId: (json['failureLogId'] as num?)?.toInt() ?? 0,
    failureType: json['failureType'] as String? ?? 'UNKNOWN',
    failureReason: json['failureReason'] as String?,
    failedAt:
        _date(json['failedAt']) ??
        DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
    resolutionStatus: json['resolutionStatus'] as String? ?? 'UNRESOLVED',
    lastResolutionAction: json['lastResolutionAction'] as String?,
    resolvedAt: _date(json['resolvedAt']),
  );
}

class FailureHandlingEvent {
  const FailureHandlingEvent({
    required this.failureLogId,
    required this.action,
    required this.createdAt,
    this.note,
    this.performedBy,
  });

  final int failureLogId;
  final String action;
  final String? note;
  final String? performedBy;
  final DateTime createdAt;

  factory FailureHandlingEvent.fromJson(Map<String, dynamic> json) =>
      FailureHandlingEvent(
        failureLogId: (json['failureLogId'] as num?)?.toInt() ?? 0,
        action: json['action'] as String? ?? '',
        note: json['note'] as String?,
        performedBy: json['performedBy'] as String?,
        createdAt:
            _date(json['createdAt']) ??
            DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      );
}

class ParcelLocation {
  const ParcelLocation({this.stationId, this.slotId});

  final String? stationId;
  final String? slotId;

  factory ParcelLocation.fromJson(Map<String, dynamic> json) => ParcelLocation(
    stationId: json['stationId'] as String?,
    slotId: json['slotId'] as String?,
  );
}

class FailureOrderDetail {
  const FailureOrderDetail({
    required this.orderId,
    required this.trackingNumber,
    required this.status,
    required this.latestFailure,
    required this.returnToOriginStatus,
    required this.failureHistory,
    required this.resolutionHistory,
    required this.resolutionHistoryComplete,
    required this.statusLogs,
    this.package,
    this.originStation,
    this.destinationStation,
    this.parcelLocation,
    this.createdAt,
    this.updatedAt,
  });

  final String orderId;
  final String trackingNumber;
  final String status;
  final OrderPackageDto? package;
  final OrderStationDto? originStation;
  final OrderStationDto? destinationStation;
  final ParcelLocation? parcelLocation;
  final FailureEpisode latestFailure;
  final String returnToOriginStatus;
  final List<FailureEpisode> failureHistory;
  final List<FailureHandlingEvent> resolutionHistory;
  final bool resolutionHistoryComplete;
  final List<DeliveryOrderStatusLogDto> statusLogs;
  final DateTime? createdAt;
  final DateTime? updatedAt;

  factory FailureOrderDetail.fromJson(Map<String, dynamic> json) {
    List<T> parseList<T>(
      dynamic value,
      T Function(Map<String, dynamic>) parse,
    ) {
      if (value is! List) return const [];
      return List.unmodifiable(
        value.map((item) {
          if (item is! Map) {
            throw const FormatException('Expected a failure history object.');
          }
          return parse(Map<String, dynamic>.from(item));
        }),
      );
    }

    return FailureOrderDetail(
      orderId: json['orderId'] as String,
      trackingNumber: json['trackingNumber'] as String? ?? '',
      status: json['status'] as String? ?? '',
      package: _nested(json['package'], OrderPackageDto.fromJson),
      originStation: _nested(json['originStation'], OrderStationDto.fromJson),
      destinationStation: _nested(
        json['destinationStation'],
        OrderStationDto.fromJson,
      ),
      parcelLocation: _nested(json['parcelLocation'], ParcelLocation.fromJson),
      latestFailure: FailureEpisode.fromJson(
        Map<String, dynamic>.from(json['latestFailure'] as Map),
      ),
      returnToOriginStatus: json['returnToOriginStatus'] as String? ?? '',
      failureHistory: parseList(
        json['failureHistory'],
        FailureEpisode.fromJson,
      ),
      resolutionHistory: parseList(
        json['resolutionHistory'],
        FailureHandlingEvent.fromJson,
      ),
      resolutionHistoryComplete:
          json['resolutionHistoryComplete'] as bool? ?? true,
      statusLogs: parseList(
        json['statusLogs'],
        DeliveryOrderStatusLogDto.fromJson,
      ),
      createdAt: _date(json['createdAt']),
      updatedAt: _date(json['updatedAt']),
    );
  }
}

class FailureResolutionRequest {
  const FailureResolutionRequest({
    required this.action,
    required this.note,
    required this.packageVerified,
  });

  final String action;
  final String note;
  final bool packageVerified;

  Map<String, dynamic> toJson() => {
    'action': action,
    'note': note,
    'packageVerified': packageVerified,
  };
}

class ReportFailureRequest {
  const ReportFailureRequest({required this.failureType, required this.reason});

  final String failureType;
  final String reason;

  Map<String, dynamic> toJson() => {
    'failureType': failureType,
    'reason': reason,
  };
}

class OrderDecision {
  const OrderDecision({
    required this.orderId,
    required this.trackingNumber,
    required this.status,
    required this.updatedAt,
  });

  final String orderId;
  final String trackingNumber;
  final String status;
  final DateTime updatedAt;

  factory OrderDecision.fromJson(Map<String, dynamic> json) => OrderDecision(
    orderId: json['orderId'] as String,
    trackingNumber: json['trackingNumber'] as String? ?? '',
    status: json['status'] as String? ?? '',
    updatedAt:
        _date(json['updatedAt']) ??
        DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
  );
}

T? _nested<T>(dynamic value, T Function(Map<String, dynamic>) parse) {
  if (value == null) return null;
  if (value is! Map) {
    throw const FormatException('Expected a failure response object.');
  }
  return parse(Map<String, dynamic>.from(value));
}

DateTime? _date(dynamic value) =>
    value == null ? null : DateTime.parse(value as String).toUtc();

typedef PagedFailedOrders = PagedResult<FailedOrderSummary>;
