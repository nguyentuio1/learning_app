import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/cancel_delivery_order_dto.dart';
import '../../../data/models/delivery_order.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/update_delivery_order_dto.dart';
import '../../../routes/route_paths.dart';
import 'order_detail_provider.dart';
import 'order_messages.dart';
import 'order_notifier.dart';

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
  bool _cancelling = false;
  bool _cancelled = false;
  late DeliveryOrder _order;
  bool _hasLocalOrderChanges = false;

  @override
  void initState() {
    super.initState();
    _order =
        widget.initialOrder ??
        DeliveryOrder(
          id: widget.detailId,
          recipientName: '',
          recipientPhone: '',
          originStationId: '',
          destinationStationId: '',
          status: '',
        );
  }

  @override
  void didUpdateWidget(covariant OrderDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.detailId != widget.detailId) {
      _order =
          widget.initialOrder ??
          DeliveryOrder(
            id: widget.detailId,
            recipientName: '',
            recipientPhone: '',
            originStationId: '',
            destinationStationId: '',
            status: '',
          );
      _cancelled = false;
      _hasLocalOrderChanges = false;
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(orderNotifierProvider, (previous, next) {
      if (next.hasError && _cancelling) {
        _cancelling = false;
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              orderOperationErrorMessage(next.error!, cancelling: true),
            ),
          ),
        );
      } else if (previous?.isLoading == true && next.hasValue && _cancelling) {
        _cancelling = false;
        if (!mounted) return;
        setState(() {
          _cancelled = true;
          _hasLocalOrderChanges = true;
          _order = DeliveryOrder(
            id: _order.id,
            recipientName: _order.recipientName,
            recipientPhone: _order.recipientPhone,
            originStationId: _order.originStationId,
            destinationStationId: _order.destinationStationId,
            status: 'CANCELLED',
            deliveryAddressId: _order.deliveryAddressId,
            trackingNumber: _order.trackingNumber,
          );
        });
        ref.invalidate(orderDetailProvider(_order.id));
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Order cancelled successfully.')),
        );
      }
    });

    final detailId = widget.detailId;
    final detail = ref.watch(orderDetailProvider(detailId));
    final order = _order;
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
                onPressed: () => ref.invalidate(orderDetailProvider(detailId)),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (response) => _buildDetails(
          _hasLocalOrderChanges ? order : _asDeliveryOrder(response),
          response,
        ),
      ),
    );
  }

  Widget _buildDetails(
    DeliveryOrder order,
    DeliveryOrderDetailResponseDto response,
  ) => ListView(
    padding: const EdgeInsets.all(24),
    children: [
      _detail('Tracking number', order.trackingNumber ?? order.id),
      _detail('Status', order.status),
      _detail('Recipient', order.recipientName),
      _detail('Phone', order.recipientPhone),
      _detail('Origin station', order.originStationId),
      _detail('Destination station', order.destinationStationId),
      if (order.deliveryAddressId != null)
        _detail('Delivery address', order.deliveryAddressId!),
      if (response.packageName != null)
        _detail('Package', response.packageName!),
      if (response.packageId != null)
        _detail('Package ID', response.packageId!),
      if (response.cancelReason != null)
        _detail('Cancellation reason', response.cancelReason!),
      if (response.scheduledDepartureTime != null)
        _detail(
          'Scheduled departure',
          _formatTimestamp(response.scheduledDepartureTime!),
        ),
      if (response.actualDepartureTime != null)
        _detail(
          'Actual departure',
          _formatTimestamp(response.actualDepartureTime!),
        ),
      if (response.estimatedDeliveryTime != null)
        _detail(
          'Estimated delivery',
          _formatTimestamp(response.estimatedDeliveryTime!),
        ),
      if (response.actualDeliveryTime != null)
        _detail(
          'Actual delivery',
          _formatTimestamp(response.actualDeliveryTime!),
        ),
      if (response.createdAt != null)
        _detail('Created', _formatTimestamp(response.createdAt!)),
      if (response.updatedAt != null)
        _detail('Last updated', _formatTimestamp(response.updatedAt!)),
      const SizedBox(height: 8),
      Text('Status history', style: Theme.of(context).textTheme.titleLarge),
      const SizedBox(height: 12),
      if (response.statusLogs.isEmpty)
        const Text('No status history available.')
      else
        for (var index = 0; index < response.statusLogs.length; index++)
          _timelineEntry(
            response.statusLogs[index],
            index < response.statusLogs.length - 1,
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
          onPressed: _cancelling ? null : _confirmCancellation,
          icon: const Icon(Icons.cancel_outlined),
          label: const Text('Cancel order'),
        ),
    ],
  );

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

  Future<void> _confirmCancellation() async {
    final reason = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (_) => const _CancelOrderSheet(),
    );
    if (!mounted || reason == null) return;
    setState(() => _cancelling = true);
    await ref
        .read(orderNotifierProvider.notifier)
        .cancelOrder(
          _order.id,
          CancelDeliveryOrderDto(cancellationReason: reason),
          currentStatus: _order.status,
        );
  }

  Future<void> _editOrder(DeliveryOrder order) async {
    final update = await context.push<UpdateDeliveryOrderDto>(
      RoutePaths.customerOrdersEdit,
      extra: order,
    );
    if (!mounted || update == null) return;
    setState(() {
      _hasLocalOrderChanges = true;
      _order = DeliveryOrder(
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
    ref.invalidate(orderDetailProvider(order.id));
  }
}

DeliveryOrder _asDeliveryOrder(DeliveryOrderDetailResponseDto detail) =>
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
  if (error case final ApiException apiException
      when apiException.code == 'MSG-P-CO-25' ||
          apiException.statusCode == 404) {
    return 'Order not found or unavailable to your account.';
  }
  return 'Unable to load order details. Please try again.';
}

class _CancelOrderSheet extends StatefulWidget {
  const _CancelOrderSheet();

  @override
  State<_CancelOrderSheet> createState() => _CancelOrderSheetState();
}

class _CancelOrderSheetState extends State<_CancelOrderSheet> {
  final _formKey = GlobalKey<FormState>();
  final _reasonController = TextEditingController();

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: EdgeInsets.fromLTRB(
        24,
        24,
        24,
        24 + MediaQuery.viewInsetsOf(context).bottom,
      ),
      child: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Cancel this order?',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _reasonController,
              maxLength: 500,
              maxLines: 3,
              decoration: const InputDecoration(
                labelText: 'Cancellation reason',
                alignLabelWithHint: true,
              ),
              validator: (value) => value == null || value.trim().isEmpty
                  ? 'Enter a cancellation reason.'
                  : null,
            ),
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(
                  onPressed: () => Navigator.pop(context),
                  child: const Text('Keep order'),
                ),
                const SizedBox(width: 8),
                FilledButton(
                  onPressed: () {
                    if (!_formKey.currentState!.validate()) return;
                    Navigator.pop(context, _reasonController.text.trim());
                  },
                  child: const Text('Confirm cancellation'),
                ),
              ],
            ),
          ],
        ),
      ),
    ),
  );
}
