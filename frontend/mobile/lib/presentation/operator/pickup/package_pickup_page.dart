import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/station_operation_models.dart';
import '../home/station_availability_providers.dart';
import 'pickup_controller.dart';

class PackagePickupPage extends ConsumerStatefulWidget {
  const PackagePickupPage({super.key});

  @override
  ConsumerState<PackagePickupPage> createState() => _PackagePickupPageState();
}

class _PackagePickupPageState extends ConsumerState<PackagePickupPage> {
  final _trackingController = TextEditingController();
  final _codeController = TextEditingController();

  @override
  void dispose() {
    _trackingController.dispose();
    _codeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final stationAsync = ref.watch(stationAvailabilityNotifierProvider);
    final state = ref.watch(pickupControllerProvider);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Package pickup confirmation'),
      ),
      body: stationAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  Icons.error_outline,
                  size: 48,
                  color: theme.colorScheme.error,
                ),
                const SizedBox(height: 16),
                Text(
                  errorMessage(error),
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodyMedium,
                ),
                const SizedBox(height: 16),
                FilledButton.icon(
                  onPressed: () => ref
                      .read(stationAvailabilityNotifierProvider.notifier)
                      .refresh(),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (stationState) {
          final stationId = stationState.selectedStationId;
          if (stationState.isUnassignedOperator ||
              stationId == null ||
              stationId.isEmpty) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(
                      Icons.assignment_late_outlined,
                      size: 64,
                      color: theme.colorScheme.primary,
                    ),
                    const SizedBox(height: 16),
                    Text(
                      'No station assigned',
                      style: theme.textTheme.titleLarge?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'You do not have an assigned landing station yet. Please contact your administrator to be assigned to a station.',
                      textAlign: TextAlign.center,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: Colors.grey.shade700,
                      ),
                    ),
                    const SizedBox(height: 16),
                    OutlinedButton.icon(
                      onPressed: () => ref
                          .read(stationAvailabilityNotifierProvider.notifier)
                          .refresh(),
                      icon: const Icon(Icons.refresh),
                      label: const Text('Check again'),
                    ),
                  ],
                ),
              ),
            );
          }

          final order = state.orderInfo;

          return SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Instructions banner
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.primaryContainer
                        .withValues(alpha: 0.4),
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
                                  _codeController.clear();
                                  ref
                                      .read(pickupControllerProvider.notifier)
                                      .lookupOrder(
                                        stationId: stationId,
                                        trackingNumber:
                                            _trackingController.text,
                                      );
                                },
                          icon: state.isLoading
                              ? const SizedBox(
                                  width: 18,
                                  height: 18,
                                  child:
                                      CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const Icon(Icons.search),
                          label: const Text('Look up parcel'),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // Error display + Remaining Attempts / Lockout details
                if (state.errorMessage != null)
                  Container(
                    margin: const EdgeInsets.only(bottom: 16),
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: theme.colorScheme.errorContainer,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Icon(
                              state.isLocked
                                  ? Icons.lock_clock
                                  : Icons.error_outline,
                              color: theme.colorScheme.onErrorContainer,
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Text(
                                state.errorMessage!,
                                style: TextStyle(
                                  color: theme.colorScheme.onErrorContainer,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ),
                          ],
                        ),
                        if (state.remainingAttempts != null &&
                            !state.isLocked) ...[
                          const SizedBox(height: 6),
                          Text(
                            'Attempts remaining: ${state.remainingAttempts}',
                            style: TextStyle(
                              fontSize: 12,
                              color: theme.colorScheme.onErrorContainer,
                            ),
                          ),
                        ],
                        if (state.isLocked) ...[
                          const SizedBox(height: 6),
                          Text(
                            [
                              'Pickup is locked due to too many failed attempts.',
                              if (state.lockoutSecondsRemaining != null)
                                'Retry after ${state.lockoutSecondsRemaining}s.',
                              if (state.lockoutExpiresAt != null)
                                'Locked until ${state.lockoutExpiresAt!.toUtc().toIso8601String()}.',
                            ].join(' '),
                            style: TextStyle(
                              fontSize: 12,
                              color: theme.colorScheme.onErrorContainer,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),

                // Step 2: Order Details & Eligibility Check / Pickup Verification
                if (order != null) ...[
                  _OrderSummaryCard(order: order),
                  const SizedBox(height: 16),
                  if (!order.isEligibleForPickup)
                    _IneligiblePickupCard(order: order)
                  else
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
                              style:
                                  TextStyle(fontSize: 13, color: Colors.grey),
                            ),
                            const SizedBox(height: 16),
                            TextField(
                              controller: _codeController,
                              enabled: !state.isLocked,
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
                              onPressed: (state.isConfirming || state.isLocked)
                                  ? null
                                  : () async {
                                      final messenger =
                                          ScaffoldMessenger.of(context);
                                      final success = await ref
                                          .read(
                                              pickupControllerProvider.notifier)
                                          .confirmPickup(
                                            stationId: stationId,
                                            trackingNumber:
                                                order.trackingNumber,
                                            secureCode: _codeController.text,
                                          );
                                      if (success && mounted) {
                                        messenger.showSnackBar(
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
                              label:
                                  const Text('Confirm Pickup & Release Slot'),
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
          );
        },
      ),
    );
  }
}

class _IneligiblePickupCard extends StatelessWidget {
  const _IneligiblePickupCard({required this.order});

  final StationOrderInfo order;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: Colors.orange.shade50,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: Colors.orange.shade300),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(Icons.warning_amber_rounded, color: Colors.orange.shade800),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Parcel not eligible for pickup',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      color: Colors.orange.shade900,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    PickupController.eligibilityMessage(order),
                    style: TextStyle(
                      fontSize: 13,
                      color: Colors.orange.shade900,
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
}

class _OrderSummaryCard extends StatelessWidget {
  const _OrderSummaryCard({required this.order});

  final StationOrderInfo order;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final weight = StationOrderInfo.formatDecimal(order.declaredWeightGram);
    final length = StationOrderInfo.formatDecimal(order.declaredLengthCm);
    final width = StationOrderInfo.formatDecimal(order.declaredWidthCm);
    final height = StationOrderInfo.formatDecimal(order.declaredHeightCm);

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
                    color: Colors.blue.withValues(alpha: 0.15),
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
            _SummaryRow(label: 'Weight', value: '$weight g'),
            const SizedBox(height: 6),
            _SummaryRow(
              label: 'Dimensions',
              value: '$length x $width x $height cm',
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
