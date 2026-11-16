import 'dart:math' as math;

import 'delivery_order_detail_response_dto.dart';

enum EtaFreshnessStatus {
  current,
  stale,
  waitingForApproval,
  arrivedAtDestination,
  completed,
  inactive,
  unavailable,
}

class OrderEtaEstimate {
  const OrderEtaEstimate({
    required this.orderId,
    required this.orderStatus,
    required this.freshnessStatus,
    this.trackingNumber,
    this.calculatedDistanceKm,
    this.predictedDurationMinutes,
    this.weatherCondition,
    this.confidenceScore,
    this.predictionCreatedAt,
    this.scheduledDepartureTime,
    this.actualDepartureTime,
    this.estimatedDeliveryTime,
    this.actualDeliveryTime,
    this.updatedAt,
    this.remainingMinutes,
  });

  final String orderId;
  final String? trackingNumber;
  final String orderStatus;
  final double? calculatedDistanceKm;
  final int? predictedDurationMinutes;
  final String? weatherCondition;
  final double? confidenceScore;
  final DateTime? predictionCreatedAt;
  final DateTime? scheduledDepartureTime;
  final DateTime? actualDepartureTime;
  final DateTime? estimatedDeliveryTime;
  final DateTime? actualDeliveryTime;
  final DateTime? updatedAt;
  final EtaFreshnessStatus freshnessStatus;
  final int? remainingMinutes;

  bool get hasPrediction =>
      calculatedDistanceKm != null || predictedDurationMinutes != null;

  bool get isTerminal =>
      orderStatus == 'DELIVERED' ||
      orderStatus == 'CANCELLED' ||
      orderStatus == 'FAILED';

  bool get isLiveEstimateActive =>
      freshnessStatus == EtaFreshnessStatus.current;

  int? get confidencePercentage =>
      confidenceScore == null ? null : (confidenceScore! * 100).round();

  factory OrderEtaEstimate.fromOrderDetail(
    DeliveryOrderDetailResponseDto detail, {
    DateTime? now,
    bool telemetryStale = false,
  }) {
    final referenceNow = (now ?? DateTime.now()).toUtc();
    final status = detail.status.trim().toUpperCase();
    final rawEta = detail.aiEtaPrediction;

    final validDistance = _validateDistance(rawEta?.calculatedDistanceKm);
    final validDuration = _validateDuration(rawEta?.predictedDurationMinutes);
    final hasValidPrediction = validDistance != null || validDuration != null;

    final validWeather = hasValidPrediction
        ? _validateWeather(rawEta?.weatherCondition)
        : null;
    final validConfidence = hasValidPrediction
        ? _validateConfidence(rawEta?.confidenceScore)
        : null;
    final createdAt = hasValidPrediction ? rawEta?.createdAt?.toUtc() : null;

    final estimatedArrival = detail.estimatedDeliveryTime?.toUtc();
    final actualArrival = detail.actualDeliveryTime?.toUtc();

    final freshness = _resolveFreshness(
      status: status,
      hasValidPrediction: hasValidPrediction,
      estimatedDeliveryTime: estimatedArrival,
      predictionCreatedAt: createdAt,
      referenceNow: referenceNow,
      telemetryStale: telemetryStale,
    );

    int? remainingMinutes;
    if (freshness == EtaFreshnessStatus.current && estimatedArrival != null) {
      final diffSeconds = estimatedArrival.difference(referenceNow).inSeconds;
      if (diffSeconds >= 0) {
        remainingMinutes = (diffSeconds / 60).ceil();
      }
    }

    return OrderEtaEstimate(
      orderId: detail.id,
      trackingNumber: detail.trackingNumber,
      orderStatus: status,
      calculatedDistanceKm: validDistance,
      predictedDurationMinutes: validDuration,
      weatherCondition: validWeather,
      confidenceScore: validConfidence,
      predictionCreatedAt: createdAt,
      scheduledDepartureTime: detail.scheduledDepartureTime?.toUtc(),
      actualDepartureTime: detail.actualDepartureTime?.toUtc(),
      estimatedDeliveryTime: estimatedArrival,
      actualDeliveryTime: actualArrival,
      updatedAt: detail.updatedAt?.toUtc(),
      freshnessStatus: freshness,
      remainingMinutes: remainingMinutes,
    );
  }

  factory OrderEtaEstimate.fromJson(
    Map<String, dynamic> json, {
    DateTime? now,
    bool telemetryStale = false,
  }) {
    if (json.containsKey('statusLogs') || json.containsKey('recipientName')) {
      final detail = DeliveryOrderDetailResponseDto.fromJson(json);
      return OrderEtaEstimate.fromOrderDetail(
        detail,
        now: now,
        telemetryStale: telemetryStale,
      );
    }

    final nestedRaw =
        json['aiEtaPrediction'] ?? json['prediction'] ?? json['estimate'];
    final nested = nestedRaw is Map
        ? Map<String, dynamic>.from(nestedRaw)
        : json;

    final rawDistance =
        nested['calculatedDistanceKm'] ?? json['calculatedDistanceKm'];
    final rawDuration =
        nested['predictedDurationMinutes'] ?? json['predictedDurationMinutes'];
    final rawWeather = nested['weatherCondition'] ?? json['weatherCondition'];
    final rawConfidence = nested['confidenceScore'] ?? json['confidenceScore'];
    final rawCreatedAt =
        nested['createdAt'] ??
        nested['calculatedAt'] ??
        json['predictionCreatedAt'] ??
        json['createdAt'];

    final syntheticDetail = DeliveryOrderDetailResponseDto(
      id: (json['orderId'] ?? json['id'] ?? '') as String,
      trackingNumber: json['trackingNumber'] as String?,
      recipientName: '',
      recipientPhone: '',
      originStationId: (json['originStationId'] ?? '') as String,
      destinationStationId: (json['destinationStationId'] ?? '') as String,
      status: (json['orderStatus'] ?? json['status'] ?? 'PENDING') as String,
      statusLogs: const [],
      scheduledDepartureTime: _parseDate(json['scheduledDepartureTime']),
      actualDepartureTime: _parseDate(json['actualDepartureTime']),
      estimatedDeliveryTime: _parseDate(json['estimatedDeliveryTime']),
      actualDeliveryTime: _parseDate(json['actualDeliveryTime']),
      updatedAt: _parseDate(json['updatedAt']),
      aiEtaPrediction:
          (rawDistance != null ||
              rawDuration != null ||
              rawWeather != null ||
              rawConfidence != null ||
              rawCreatedAt != null)
          ? OrderAiEtaPredictionDto(
              calculatedDistanceKm: (rawDistance as num?)?.toDouble(),
              predictedDurationMinutes: (rawDuration as num?)?.toInt(),
              weatherCondition: rawWeather as String?,
              confidenceScore: (rawConfidence as num?)?.toDouble(),
              createdAt: _parseDate(rawCreatedAt),
            )
          : null,
    );

    final explicitStale =
        json['isStale'] == true ||
        json['telemetryStale'] == true ||
        nested['isStale'] == true;

    return OrderEtaEstimate.fromOrderDetail(
      syntheticDetail,
      now: now,
      telemetryStale: telemetryStale || explicitStale,
    );
  }

  static EtaFreshnessStatus _resolveFreshness({
    required String status,
    required bool hasValidPrediction,
    required DateTime? estimatedDeliveryTime,
    required DateTime? predictionCreatedAt,
    required DateTime referenceNow,
    required bool telemetryStale,
  }) {
    switch (status) {
      case 'DELIVERED':
        return EtaFreshnessStatus.completed;
      case 'CANCELLED':
      case 'FAILED':
        return EtaFreshnessStatus.inactive;
      case 'ARRIVED':
        return EtaFreshnessStatus.arrivedAtDestination;
      case 'PENDING':
        if (!hasValidPrediction && estimatedDeliveryTime == null) {
          return EtaFreshnessStatus.waitingForApproval;
        }
        if (estimatedDeliveryTime != null &&
            estimatedDeliveryTime.isBefore(referenceNow)) {
          return EtaFreshnessStatus.stale;
        }
        return EtaFreshnessStatus.current;
      case 'APPROVED':
      case 'READY_FOR_TAKEOFF':
      case 'IN_TRANSIT':
      default:
        if (!hasValidPrediction && estimatedDeliveryTime == null) {
          return EtaFreshnessStatus.unavailable;
        }
        if (telemetryStale && status == 'IN_TRANSIT') {
          return EtaFreshnessStatus.stale;
        }
        if (estimatedDeliveryTime != null &&
            estimatedDeliveryTime.isBefore(referenceNow)) {
          return EtaFreshnessStatus.stale;
        }
        return EtaFreshnessStatus.current;
    }
  }

  static double? _validateDistance(double? value) {
    if (value == null || value.isNaN || value.isInfinite || value <= 0) {
      return null;
    }
    return value;
  }

  static int? _validateDuration(int? value) {
    if (value == null || value <= 0) {
      return null;
    }
    return value;
  }

  static String? _validateWeather(String? value) {
    if (value == null) return null;
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  static double? _validateConfidence(double? value) {
    if (value == null || value.isNaN || value.isInfinite) {
      return null;
    }
    if (value < 0.0 || value > 1.0) {
      return null;
    }
    return value;
  }

  static DateTime? _parseDate(dynamic value) {
    if (value == null) return null;
    if (value is DateTime) return value.toUtc();
    if (value is String && value.isNotEmpty) {
      return DateTime.tryParse(value)?.toUtc();
    }
    return null;
  }
}

class RouteEtaPreview {
  const RouteEtaPreview({
    required this.distanceKm,
    required this.estimatedDurationMinutes,
    required this.packageWeightGram,
    this.isFragile = false,
  });

  final double distanceKm;
  final int estimatedDurationMinutes;
  final double packageWeightGram;
  final bool isFragile;

  static RouteEtaPreview? compute({
    required double? originLatitude,
    required double? originLongitude,
    required double? destinationLatitude,
    required double? destinationLongitude,
    required double? weightGram,
    bool isFragile = false,
    double cruiseSpeedMps = 12.0,
  }) {
    if (originLatitude == null ||
        originLongitude == null ||
        destinationLatitude == null ||
        destinationLongitude == null ||
        weightGram == null) {
      return null;
    }
    if (!_validLat(originLatitude) ||
        !_validLon(originLongitude) ||
        !_validLat(destinationLatitude) ||
        !_validLon(destinationLongitude)) {
      return null;
    }
    if (weightGram <= 0 || weightGram > 3000 || cruiseSpeedMps <= 0) {
      return null;
    }

    final rawKm = greatCircleDistanceKm(
      originLatitude,
      originLongitude,
      destinationLatitude,
      destinationLongitude,
    );
    if (rawKm <= 0 || rawKm.isNaN || rawKm.isInfinite) {
      return null;
    }

    final roundedKm = (rawKm * 100).roundToDouble() / 100;
    // Weight penalty up to +15% at max 3000g payload, +5% for fragile handling
    final weightFactor = 1.0 + (weightGram / 3000.0) * 0.15;
    final fragileFactor = isFragile ? 1.05 : 1.0;
    final effectiveSpeedMps =
        cruiseSpeedMps / (weightFactor * fragileFactor);
    final minutes = math.max(
      1,
      ((rawKm * 1000.0) / effectiveSpeedMps / 60.0).ceil(),
    );

    return RouteEtaPreview(
      distanceKm: roundedKm == 0 ? 0.01 : roundedKm,
      estimatedDurationMinutes: minutes,
      packageWeightGram: weightGram,
      isFragile: isFragile,
    );
  }

  static double greatCircleDistanceKm(
    double fromLatitude,
    double fromLongitude,
    double toLatitude,
    double toLongitude,
  ) {
    const radians = math.pi / 180.0;
    final lat1 = fromLatitude * radians;
    final lat2 = toLatitude * radians;
    final dLat = lat2 - lat1;
    final dLon = (toLongitude - fromLongitude) * radians;
    final a =
        math.pow(math.sin(dLat / 2), 2) +
        math.cos(lat1) * math.cos(lat2) * math.pow(math.sin(dLon / 2), 2);
    final clamped = a.clamp(0.0, 1.0).toDouble();
    return 6371.0088 * 2.0 * math.asin(math.sqrt(clamped));
  }

  static bool _validLat(double value) =>
      !value.isNaN && !value.isInfinite && value >= -90.0 && value <= 90.0;

  static bool _validLon(double value) =>
      !value.isNaN && !value.isInfinite && value >= -180.0 && value <= 180.0;
}
