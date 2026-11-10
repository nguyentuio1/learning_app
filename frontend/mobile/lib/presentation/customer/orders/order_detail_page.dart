import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/delivery_order.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/update_delivery_order_dto.dart';
import '../../../routes/route_paths.dart';
import 'cancel_order_controller.dart';
import 'cancel_order_sheet.dart';
import 'my_orders_provider.dart';
import 'order_messages.dart';
import 'resolve_failed_delivery_sheet.dart';

class OrderDetailPage extends ConsumerStatefulWidget {
  const OrderDetailPage({
    DeliveryOrder? order,
    String? orderId,
    this.allowFailureResolution = false,
    super.key,
  }) : initialOrder = order,
       orderId = orderId ?? '';

  final DeliveryOrder? initialOrder;
  final String orderId;
  final bool allowFailureResolution;

  String get detailId => orderId.isNotEmpty ? orderId : initialOrder?.id ?? '';

  @override
  ConsumerState<OrderDetailPage> createState() => _OrderDetailPageState();
}

class _OrderDetailPageState extends ConsumerState<OrderDetailPage> {
  bool _cancelSheetOpen = false;
  bool _cancelled = false;
  DeliveryOrder? _cancellingOrder;
  DeliveryOrder? _localOrder;

  @override
  Widget build(BuildContext context) {
    ref.listen(cancelOrderControllerProvider, (previous, next) {
      if (previous?.isLoading != true) return;
      if (next.hasError) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              orderOperationErrorMessage(next.error!, cancelling: true),
            ),
          ),
        );
      } else if (next.hasValue) {
        if (_cancelSheetOpen) Navigator.of(context).pop();
        setState(() {
          _cancelled = true;
          final order = _cancellingOrder ?? _localOrder ?? widget.initialOrder;
          if (order != null) _localOrder = _withStatus(order, 'CANCELLED');
        });
        ref.invalidate(orderDetailProvider(widget.detailId));
        ref.invalidate(myOrdersProvider);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Order cancelled successfully.')),
        );
      }
    });

    final detail = ref.watch(orderDetailProvider(widget.detailId));
    return Scaffold(
      appBar: AppBar(title: const Text('Order details')),
      body: detail.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(orderDetailErrorMessage(error), textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () =>
                    ref.invalidate(orderDetailProvider(widget.detailId)),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (response) {
          final order = _localOrder ?? _orderFrom(response);
          return _buildDetails(order, response);
        },
      ),
    );
  }

  Widget _buildDetails(
    DeliveryOrder order,
    DeliveryOrderDetailResponseDto detail,
  ) {
    final package = detail.package;
    final origin = detail.originStation;
    final destination = detail.destinationStation;
    final address = detail.deliveryAddress;
    final eta = detail.aiEtaPrediction;

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        _detail('Tracking number', order.trackingNumber ?? order.id),
        _detail('Status', order.status),
        if (order.parsedStatus == DeliveryOrderStatus.failed)
          _failedDeliveryBanner(detail),
        _detail('Recipient', order.recipientName),
        _detail('Phone', order.recipientPhone),
        _detail('Origin station', _stationLabel(origin, order.originStationId)),
        _detail(
          'Destination station',
          _stationLabel(destination, order.destinationStationId),
        ),
        if (package != null) ...[
          const SizedBox(height: 8),
          Text('Package', style: Theme.of(context).textTheme.titleMedium),
          if (package.packageName != null)
            _detail('Package name', package.packageName!),
          if (package.weightGram != null)
            _detail('Weight', '${package.weightGram} g'),
          if (package.lengthCm != null &&
              package.widthCm != null &&
              package.heightCm != null)
            _detail(
              'Dimensions',
              '${package.lengthCm} × ${package.widthCm} × '
                  '${package.heightCm} cm',
            ),
          if (package.isFragile == true) _detail('Handling', 'Fragile'),
        ],
        if (address != null) ...[
          const SizedBox(height: 8),
          Text(
            'Delivery address',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          if (address.addressLabel != null)
            _detail('Label', address.addressLabel!),
          if (address.streetAddress != null)
            _detail('Address', address.streetAddress!),
          if (address.recipientName != null)
            _detail('Address recipient', address.recipientName!),
          if (address.recipientPhone != null)
            _detail('Address phone', address.recipientPhone!),
        ] else if (order.deliveryAddressId != null) ...[
          _detail('Delivery address', order.deliveryAddressId!),
        ],
        if (eta != null) ...[
          const SizedBox(height: 8),
          Text(
            'AI ETA prediction',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          if (eta.calculatedDistanceKm != null)
            _detail('Distance', '${eta.calculatedDistanceKm} km'),
          if (eta.predictedDurationMinutes != null)
            _detail(
              'Predicted duration',
              '${eta.predictedDurationMinutes} minutes',
            ),
          if (eta.weatherCondition != null)
            _detail('Weather', eta.weatherCondition!),
          if (eta.confidenceScore != null)
            _detail('Confidence', '${eta.confidenceScore}'),
          if (eta.createdAt != null)
            _detail('Prediction created', _formatTimestamp(eta.createdAt!)),
        ],
        if (detail.scheduledDepartureTime != null)
          _detail(
            'Scheduled departure',
            _formatTimestamp(detail.scheduledDepartureTime!),
          ),
        if (detail.actualDepartureTime != null)
          _detail(
            'Actual departure',
            _formatTimestamp(detail.actualDepartureTime!),
          ),
        if (detail.estimatedDeliveryTime != null)
          _detail(
            'Estimated delivery',
            _formatTimestamp(detail.estimatedDeliveryTime!),
          ),
        if (detail.actualDeliveryTime != null)
          _detail(
            'Actual delivery',
            _formatTimestamp(detail.actualDeliveryTime!),
          ),
        if (detail.cancelReason != null)
          _detail('Cancellation reason', detail.cancelReason!),
        const SizedBox(height: 8),
        Text('Status history', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 12),
        if (detail.statusLogs.isEmpty)
          const Text('No status history available.')
        else
          for (var index = 0; index < detail.statusLogs.length; index++)
            _timelineEntry(
              detail.statusLogs[index],
              index < detail.statusLogs.length - 1,
            ),
        if (order.status == 'PENDING')
          FilledButton.icon(
            onPressed: () => _editOrder(order),
            icon: const Icon(Icons.edit),
            label: const Text('Edit Order'),
          ),
        if ((order.status == 'PENDING' || order.status == 'APPROVED') &&
            !_cancelled)
          OutlinedButton.icon(
            onPressed: () => _confirmCancellation(order),
            icon: const Icon(Icons.cancel_outlined),
            label: const Text('Cancel order'),
          ),
        if (widget.allowFailureResolution &&
            order.parsedStatus == DeliveryOrderStatus.failed)
          FilledButton.icon(
            onPressed: () => showModalBottomSheet<void>(
              context: context,
              isScrollControlled: true,
              builder: (_) => ResolveFailedDeliverySheet(orderId: order.id),
            ),
            icon: const Icon(Icons.assignment_turned_in_outlined),
            label: const Text('Resolve failed delivery'),
          ),
      ],
    );
  }

  Widget _failedDeliveryBanner(DeliveryOrderDetailResponseDto detail) {
    final reason = detail.failureReason ?? detail.cancelReason;
    return Card(
      color: Theme.of(context).colorScheme.errorContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Delivery failed',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 4),
            Text(
              reason == null || reason.isEmpty
                  ? 'Failure reason unavailable.'
                  : 'Failure reason: $reason',
            ),
          ],
        ),
      ),
    );
  }

  Widget _timelineEntry(DeliveryOrderStatusLogDto log, bool hasNext) => Padding(
    padding: const EdgeInsets.only(bottom: 18),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Column(
          children: [
            const Icon(Icons.circle, size: 12),
            if (hasNext)
              Container(
                width: 2,
                height: 46,
                color: Theme.of(context).dividerColor,
              ),
          ],
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${log.fromStatus ?? '—'} → ${log.toStatus}',
                style: Theme.of(context).textTheme.titleSmall,
              ),
              if (log.remarks != null && log.remarks!.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text(log.remarks!),
                ),
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(_formatTimestamp(log.createdAt)),
              ),
            ],
          ),
        ),
      ],
    ),
  );

  Widget _detail(String label, String value) => Padding(
    padding: const EdgeInsets.only(bottom: 16),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: Theme.of(context).textTheme.labelLarge),
        const SizedBox(height: 4),
        SelectableText(value),
      ],
    ),
  );

  Future<void> _confirmCancellation(DeliveryOrder order) async {
    _cancellingOrder = order;
    _cancelSheetOpen = true;
    try {
      await showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
        ),
        clipBehavior: Clip.antiAlias,
        builder: (_) => CancelOrderSheet(orderId: order.id),
      );
    } finally {
      _cancelSheetOpen = false;
    }
  }

  Future<void> _editOrder(DeliveryOrder order) async {
    final update = await context.push<UpdateDeliveryOrderDto>(
      RoutePaths.customerOrdersEdit,
      extra: order,
    );
    if (!mounted || update == null) return;
    setState(() {
      _localOrder = DeliveryOrder(
        id: order.id,
        recipientName: update.recipientName,
        recipientPhone: update.recipientPhone,
        originStationId: order.originStationId,
        destinationStationId: update.destinationStationId,
        status: order.status,
        deliveryAddressId: update.deliveryAddressId,
        trackingNumber: order.trackingNumber,
      );
    });
    ref.invalidate(orderDetailProvider(widget.detailId));
  }

  String _stationLabel(OrderStationDto? station, String fallbackId) {
    if (station == null) return fallbackId;
    return [
      station.name,
      station.stationCode,
      station.address,
    ].whereType<String>().where((value) => value.isNotEmpty).join(' · ');
  }
}

DeliveryOrder _withStatus(DeliveryOrder order, String status) => DeliveryOrder(
  id: order.id,
  recipientName: order.recipientName,
  recipientPhone: order.recipientPhone,
  originStationId: order.originStationId,
  destinationStationId: order.destinationStationId,
  status: status,
  deliveryAddressId: order.deliveryAddressId,
  trackingNumber: order.trackingNumber,
);

DeliveryOrder _orderFrom(DeliveryOrderDetailResponseDto detail) =>
    DeliveryOrder(
      id: detail.id,
      recipientName: detail.recipientName,
      recipientPhone: detail.recipientPhone,
      originStationId: detail.originStationId,
      destinationStationId: detail.destinationStationId,
      status: detail.status,
      deliveryAddressId: detail.deliveryAddressId,
      trackingNumber: detail.trackingNumber,
    );

String _formatTimestamp(DateTime value) => value.toLocal().toString();

String orderDetailErrorMessage(Object error) {
  if (error is ApiException &&
      (error.code == 'MSG-P-CO-25' || error.statusCode == 404)) {
    return 'Order not found or unavailable to your account.';
  }
  return 'Unable to load order details. Please try again.';
}
