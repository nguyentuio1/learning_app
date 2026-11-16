import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/api_exception.dart';
import '../../../core/error/error_message.dart';
import '../../../data/models/delivery_order.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/station_operation_models.dart';
import '../../../data/models/update_delivery_order_dto.dart';
import '../../../presentation/common/widgets/async_value_view.dart';
import '../../../routes/route_paths.dart';
import '../delivery_map/delivery_map_controller.dart';
import '../delivery_map/delivery_map_telemetry_bar.dart';
import '../delivery_map/delivery_map_widget.dart';
import 'cancel_order_controller.dart';
import 'cancel_order_sheet.dart';
import 'confirm_delivery_page.dart';
import 'my_orders_provider.dart';
import 'widgets/eta_estimation_card.dart';

class OrderDetailPage extends ConsumerStatefulWidget {
  const OrderDetailPage({DeliveryOrder? order, String? orderId, super.key})
    : initialOrder = order,
      orderId = orderId ?? '';

  final DeliveryOrder? initialOrder;
  final String orderId;

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
      if (!next.hasError && next.hasValue) {
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
      resizeToAvoidBottomInset: true,
      appBar: AppBar(title: const Text('Order details')),
      body: detail.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    isNetworkError(error)
                        ? Icons.wifi_off_rounded
                        : Icons.error_outline,
                    size: 48,
                    color: Theme.of(context).colorScheme.error,
                  ),
                  const SizedBox(height: 16),
                  Text(
                    orderDetailErrorMessage(error),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 16),
                  FilledButton(
                    style: FilledButton.styleFrom(
                      minimumSize: const Size(120, 48),
                    ),
                    onPressed: () =>
                        ref.invalidate(orderDetailProvider(widget.detailId)),
                    child: const Text('Retry'),
                  ),
                ],
              ),
            ),
          ),
        ),
        data: (response) {
          final order = _localOrder ?? _orderFrom(response);
          final isNonTerminal =
              order.status != 'DELIVERED' &&
              order.status != 'CANCELLED' &&
              order.status != 'FAILED';
          final mapState = isNonTerminal
              ? ref.watch(deliveryMapControllerProvider(order.id))
              : null;
          return _buildDetails(order, response, mapState);
        },
      ),
    );
  }

  Widget _buildDetails(
    DeliveryOrder order,
    DeliveryOrderDetailResponseDto detail,
    AsyncValue<DeliveryMapViewState>? mapState,
  ) {
    final package = detail.package;
    final origin = detail.originStation;
    final destination = detail.destinationStation;
    final address = detail.deliveryAddress;

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        if (order.status == 'ARRIVED') _arrivedBanner(detail),
        if (order.status == 'FAILED') _failureBanner(detail),
        if (order.status == 'IN_TRANSIT' && mapState != null) ...[
          AsyncValueView<DeliveryMapViewState>(
            value: mapState,
            onRetry: () =>
                ref.invalidate(deliveryMapControllerProvider(order.id)),
            loading: const SizedBox(
              height: 320,
              child: Center(child: CircularProgressIndicator()),
            ),
            data: (map) => Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                DeliveryMapWidget(
                  snapshot: map.snapshot,
                  liveUnavailable: map.liveUnavailable,
                ),
                DeliveryMapTelemetryBar(snapshot: map.snapshot),
              ],
            ),
          ),
          const SizedBox(height: 8),
        ],
        _detail('Tracking number', order.trackingNumber ?? order.id),
        _detail('Status', order.status),
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
        const SizedBox(height: 8),
        EtaEstimationCard(orderId: order.id),
        const SizedBox(height: 12),
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
        OutlinedButton.icon(
          key: const Key('view-order-history-button'),
          onPressed: () =>
              context.push(RoutePaths.customerOrderHistoryFor(order.id)),
          icon: const Icon(Icons.history),
          label: const Text('View Order History'),
        ),
        OutlinedButton.icon(
          key: const Key('ask-ai-assistant-button'),
          onPressed: () => context.push(
            RoutePaths.customerAssistantFor(
              orderId: order.id,
              trackingNumber: order.trackingNumber,
            ),
          ),
          icon: const Icon(Icons.chat_outlined),
          label: const Text('Ask AI Assistant'),
        ),
        if (order.status == 'ARRIVED') ...[
          const SizedBox(height: 8),
          FilledButton.icon(
            key: const Key('enter-pickup-code-button'),
            onPressed: () => _openConfirmDelivery(order),
            icon: const Icon(Icons.vpn_key_outlined),
            label: const Text('Enter pickup code'),
          ),
        ],
        if (order.status == 'PENDING') ...[
          const SizedBox(height: 8),
          FilledButton.icon(
            onPressed: () => _editOrder(order),
            icon: const Icon(Icons.edit),
            label: const Text('Edit Order'),
          ),
        ],
        if ((order.status == 'PENDING' || order.status == 'APPROVED') &&
            !_cancelled) ...[
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () => _confirmCancellation(order),
            icon: const Icon(Icons.cancel_outlined),
            label: const Text('Cancel order'),
          ),
        ],
      ],
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

  Widget _arrivedBanner(DeliveryOrderDetailResponseDto detail) {
    final stationName =
        detail.destinationStation?.name ??
        detail.destinationStation?.stationCode ??
        detail.destinationStationId;
    return Card(
      color: Theme.of(context).colorScheme.primaryContainer,
      margin: const EdgeInsets.only(bottom: 16),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              Icons.inventory_2_outlined,
              color: Theme.of(context).colorScheme.onPrimaryContainer,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Arrived at $stationName',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: Theme.of(context).colorScheme.onPrimaryContainer,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Your package has landed. Enter the pickup code to complete delivery.',
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.onPrimaryContainer,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _failureBanner(DeliveryOrderDetailResponseDto detail) {
    final failureLogs = detail.statusLogs
        .where((log) => log.toStatus == 'FAILED')
        .toList();
    final failureLog = failureLogs.isEmpty ? null : failureLogs.last;
    final reason = failureLog?.remarks?.trim();
    return Card(
      color: Theme.of(context).colorScheme.errorContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              Icons.error_outline,
              color: Theme.of(context).colorScheme.error,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Delivery failed',
                    style: Theme.of(context).textTheme.titleMedium
                        ?.copyWith(color: Theme.of(context).colorScheme.error),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    reason == null || reason.isEmpty
                        ? 'See status history for details.'
                        : reason,
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

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
        builder: (_) =>
            CancelOrderSheet(orderId: order.id, currentStatus: order.status),
      );
    } finally {
      _cancelSheetOpen = false;
    }
  }

  Future<void> _openConfirmDelivery(DeliveryOrder order) async {
    final router = GoRouter.maybeOf(context);
    ConfirmPickupResponse? result;
    if (router != null) {
      result = await context.push<ConfirmPickupResponse>(
        RoutePaths.customerOrderConfirmDeliveryFor(order.id),
        extra: order,
      );
    } else {
      result = await Navigator.of(context).push<ConfirmPickupResponse>(
        MaterialPageRoute<ConfirmPickupResponse>(
          builder: (_) =>
              ConfirmDeliveryPage(orderId: order.id, initialOrder: order),
        ),
      );
    }
    if (!mounted) return;
    if (result != null) {
      setState(() {
        _localOrder = _withStatus(order, result!.status);
      });
    }
    ref.invalidate(orderDetailProvider(widget.detailId));
    ref.invalidate(myOrdersProvider);
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
