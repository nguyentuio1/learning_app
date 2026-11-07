import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/session/auth_controller.dart';
import '../../../data/models/station_operation_models.dart';
import 'pickup_controller.dart';

class PackagePickupPage extends ConsumerStatefulWidget {
  const PackagePickupPage({super.key});

  @override
  ConsumerState<PackagePickupPage> createState() => _PackagePickupPageState();
}

class _PackagePickupPageState extends ConsumerState<PackagePickupPage> {
  final _trackingController = TextEditingController();
  final _codeController = TextEditingController();

  // In real station operations, stationId is associated with the operator's current hub
  // For demo/standalone, we can allow input or default
  String _stationId = '7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44';

  @override
  void dispose() {
    _trackingController.dispose();
    _codeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(pickupControllerProvider);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Package pickup confirmation'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Instructions banner
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: theme.colorScheme.primaryContainer.withOpacity(0.4),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(
                children: [
                  Icon(
                    Icons.security,
                    color: theme.colorScheme.primary,
                  ),
                  const SizedBox(width: 12),
                  const Expanded(
                    child: Text(
                      'Enter tracking number and verify the 6-digit secure code presented by the recipient to release the slot.',
                      style: TextStyle(fontSize: 13),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),

            // Step 1: Lookup Section (O05 Scan / Lookup)
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '1. Order Lookup',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _trackingController,
                      decoration: const InputDecoration(
                        labelText: 'Tracking Number',
                        hintText: 'e.g. DD-202611-0001',
                        prefixIcon: Icon(Icons.qr_code_scanner),
                      ),
                      textCapitalization: TextCapitalization.characters,
                    ),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: state.isLoading
                          ? null
                          : () {
                              ref
                                  .read(pickupControllerProvider.notifier)
                                  .lookupOrder(
                                    stationId: _stationId,
                                    trackingNumber: _trackingController.text,
                                  );
                            },
                      icon: state.isLoading
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.search),
                      label: const Text('Look up parcel'),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),

            // Error display
            if (state.errorMessage != null)
              Container(
                margin: const EdgeInsets.only(bottom: 16),
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: theme.colorScheme.errorContainer,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Row(
                  children: [
                    Icon(Icons.error_outline,
                        color: theme.colorScheme.onErrorContainer),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        state.errorMessage!,
                        style: TextStyle(
                            color: theme.colorScheme.onErrorContainer),
                      ),
                    ),
                  ],
                ),
              ),

            // Step 2: Order Details & Pickup Verification (O07 / O08)
            if (state.orderInfo != null) ...[
              _OrderSummaryCard(order: state.orderInfo!),
              const SizedBox(height: 16),
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '2. Verify Secure Code',
                        style: theme.textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        'Ask the recipient for the 6-digit numeric pickup secure code generated in their app.',
                        style: TextStyle(fontSize: 13, color: Colors.grey),
                      ),
                      const SizedBox(height: 16),
                      TextField(
                        controller: _codeController,
                        keyboardType: TextInputType.number,
                        maxLength: 6,
                        decoration: const InputDecoration(
                          labelText: '6-digit Pickup Secure Code',
                          hintText: '482915',
                          prefixIcon: Icon(Icons.pin_outlined),
                        ),
                      ),
                      const SizedBox(height: 8),
                      FilledButton.icon(
                        style: FilledButton.styleFrom(
                          backgroundColor: Colors.green.shade700,
                        ),
                        onPressed: state.isConfirming
                            ? null
                            : () async {
                                final success = await ref
                                    .read(pickupControllerProvider.notifier)
                                    .confirmPickup(
                                      stationId: _stationId,
                                      trackingNumber:
                                          state.orderInfo!.trackingNumber,
                                      secureCode: _codeController.text,
                                    );
                                if (success && mounted) {
                                  ScaffoldMessenger.of(context).showSnackBar(
                                    const SnackBar(
                                      content: Text(
                                          'Pickup completed successfully! Slot released.'),
                                      backgroundColor: Colors.green,
                                    ),
                                  );
                                }
                              },
                        icon: state.isConfirming
                            ? const SizedBox(
                                width: 18,
                                height: 18,
                                child: CircularProgressIndicator(
                                    strokeWidth: 2, color: Colors.white),
                              )
                            : const Icon(Icons.check_circle_outline),
                        label: const Text('Confirm Pickup & Release Slot'),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 16),
            ],

            // Step 3: Success Result (O08 Pickup Done)
            if (state.completedPickup != null) ...[
              _PickupSuccessCard(pickup: state.completedPickup!),
            ],
          ],
        ),
      ),
    );
  }
}

class _OrderSummaryCard extends StatelessWidget {
  const _OrderSummaryCard({required this.order});

  final StationOrderInfo order;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  order.trackingNumber,
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: Colors.blue.withOpacity(0.15),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Text(
                    order.status,
                    style: const TextStyle(
                      color: Colors.blue,
                      fontWeight: FontWeight.bold,
                      fontSize: 12,
                    ),
                  ),
                ),
              ],
            ),
            const Divider(height: 24),
            _SummaryRow(label: 'Package', value: order.packageName),
            const SizedBox(height: 6),
            _SummaryRow(
                label: 'Weight', value: '${order.declaredWeightGram} g'),
            const SizedBox(height: 6),
            _SummaryRow(
              label: 'Dimensions',
              value:
                  '${order.declaredLengthCm} x ${order.declaredWidthCm} x ${order.declaredHeightCm} cm',
            ),
            const SizedBox(height: 6),
            _SummaryRow(
              label: 'Fragile',
              value: order.isFragile ? 'Yes (Handle with care)' : 'No',
            ),
            const SizedBox(height: 6),
            _SummaryRow(label: 'Role at this Station', value: order.role),
          ],
        ),
      ),
    );
  }
}

class _SummaryRow extends StatelessWidget {
  const _SummaryRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: const TextStyle(fontSize: 13, color: Colors.grey)),
        Text(value,
            style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600)),
      ],
    );
  }
}

class _PickupSuccessCard extends StatelessWidget {
  const _PickupSuccessCard({required this.pickup});

  final ConfirmPickupResponse pickup;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: Colors.green.shade50,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: Colors.green.shade300),
      ),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          children: [
            Icon(Icons.check_circle, size: 48, color: Colors.green.shade700),
            const SizedBox(height: 12),
            Text(
              'Handover Completed',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.bold,
                color: Colors.green.shade900,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'Order ${pickup.trackingNumber} is now marked DELIVERED.',
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.green.shade800),
            ),
            const SizedBox(height: 12),
            Text(
              'Slot ${pickup.slotId} is released to EMPTY and ready for future flights.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 12, color: Colors.green.shade700),
            ),
          ],
        ),
      ),
    );
  }
}
