import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/models/cancel_delivery_order_dto.dart';
import '../../../data/models/delivery_order.dart';
import '../../../data/models/update_delivery_order_dto.dart';
import '../../../routes/route_paths.dart';
import 'order_messages.dart';
import 'order_notifier.dart';

class OrderDetailPage extends ConsumerStatefulWidget {
  const OrderDetailPage({required this.order, super.key});

  final DeliveryOrder order;

  @override
  ConsumerState<OrderDetailPage> createState() => _OrderDetailPageState();
}

class _OrderDetailPageState extends ConsumerState<OrderDetailPage> {
  bool _cancelling = false;
  bool _cancelled = false;
  late DeliveryOrder _order;

  @override
  void initState() {
    super.initState();
    _order = widget.order;
  }

  @override
  void didUpdateWidget(covariant OrderDetailPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.order.id != widget.order.id ||
        oldWidget.order.status != widget.order.status) {
      _order = widget.order;
      _cancelled = false;
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
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Order cancelled successfully.')),
        );
      }
    });

    final order = _order;
    final canEdit = order.status == 'PENDING';
    final canCancel = order.status == 'PENDING' || order.status == 'APPROVED';

    return Scaffold(
      appBar: AppBar(title: const Text('Order details')),
      body: ListView(
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
          if (canEdit)
            FilledButton.icon(
              onPressed: () => _editOrder(order),
              icon: const Icon(Icons.edit),
              label: const Text('Edit Order'),
            ),
          if (canCancel && !_cancelled)
            OutlinedButton.icon(
              onPressed: _cancelling ? null : _confirmCancellation,
              icon: const Icon(Icons.cancel_outlined),
              label: const Text('Cancel order'),
            ),
        ],
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
  }
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
