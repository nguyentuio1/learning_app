import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/station_operation_models.dart';
import '../../../data/models/station_slot.dart';
import '../home/station_availability_providers.dart';
import 'intake_controller.dart';

class PackageIntakePage extends ConsumerStatefulWidget {
  const PackageIntakePage({super.key});

  @override
  ConsumerState<PackageIntakePage> createState() => _PackageIntakePageState();
}

class _PackageIntakePageState extends ConsumerState<PackageIntakePage> {
  final _trackingController = TextEditingController();
  final _weightController = TextEditingController();
  final _lengthController = TextEditingController();
  final _widthController = TextEditingController();
  final _heightController = TextEditingController();
  final _formKey = GlobalKey<FormState>();

  String? _selectedSlotId;
  bool _visualInspectionPassed = true;
  String? _prefilledOrderId;

  @override
  void dispose() {
    _trackingController.dispose();
    _weightController.dispose();
    _lengthController.dispose();
    _widthController.dispose();
    _heightController.dispose();
    super.dispose();
  }

  void _prefillFromOrder(
    StationOrderInfo info,
    List<StationSlot> emptySlots,
  ) {
    if (_prefilledOrderId == info.orderId) return;
    _prefilledOrderId = info.orderId;
    _weightController.text =
        StationOrderInfo.formatDecimal(info.declaredWeightGram);
    _lengthController.text =
        StationOrderInfo.formatDecimal(info.declaredLengthCm);
    _widthController.text =
        StationOrderInfo.formatDecimal(info.declaredWidthCm);
    _heightController.text =
        StationOrderInfo.formatDecimal(info.declaredHeightCm);
    _visualInspectionPassed = true;
    _selectedSlotId = emptySlots.isNotEmpty ? emptySlots.first.id : null;
  }

  Future<void> _lookup(String stationId) async {
    _prefilledOrderId = null;
    await ref.read(intakeControllerProvider.notifier).lookupOrder(
          stationId: stationId,
          trackingNumber: _trackingController.text,
        );
  }

  Future<void> _confirmIntake(String stationId, StationOrderInfo info) async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final slotId = _selectedSlotId;
    if (slotId == null || slotId.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select an empty station slot.')),
      );
      return;
    }

    final ok = await ref.read(intakeControllerProvider.notifier).confirmIntake(
          stationId: stationId,
          trackingNumber: info.trackingNumber,
          slotId: slotId,
          measuredWeightGram: double.parse(_weightController.text.trim()),
          measuredLengthCm: double.parse(_lengthController.text.trim()),
          measuredWidthCm: double.parse(_widthController.text.trim()),
          measuredHeightCm: double.parse(_heightController.text.trim()),
          visualInspectionPassed: _visualInspectionPassed,
        );
    if (ok && mounted) {
      await ref.read(stationAvailabilityNotifierProvider.notifier).refresh();
    }
  }

  Future<void> _showRejectDialog(String stationId, StationOrderInfo info) async {
    final reasonController = TextEditingController();
    final confirmed = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Reject Package Intake'),
        content: TextField(
          key: const Key('intake-reject-reason-input'),
          controller: reasonController,
          maxLines: 3,
          decoration: const InputDecoration(
            labelText: 'Rejection reason',
            hintText: 'e.g. Damaged packaging or weight mismatch',
            border: OutlineInputBorder(),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Cancel'),
          ),
          FilledButton(
            key: const Key('intake-reject-confirm-button'),
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(dialogContext).colorScheme.error,
            ),
            onPressed: () =>
                Navigator.of(dialogContext).pop(reasonController.text.trim()),
            child: const Text('Reject Parcel'),
          ),
        ],
      ),
    );
    reasonController.dispose();

    if (confirmed == null || !mounted) return;
    final ok = await ref.read(intakeControllerProvider.notifier).rejectIntake(
          stationId: stationId,
          trackingNumber: info.trackingNumber,
          reason: confirmed,
        );
    if (ok && mounted) {
      await ref.read(stationAvailabilityNotifierProvider.notifier).refresh();
    }
  }

  String? _positiveNumberValidator(String? value, String label) {
    final parsed = double.tryParse(value?.trim() ?? '');
    if (parsed == null || parsed <= 0) {
      return 'Enter a valid $label';
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final stationAsync = ref.watch(stationAvailabilityNotifierProvider);
    final state = ref.watch(intakeControllerProvider);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Origin package intake'),
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
          final station = stationState.currentStation;
          final stationId = stationState.selectedStationId;
          if (station == null || stationId == null) {
            return const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text(
                  'No landing station assigned to your account.',
                  textAlign: TextAlign.center,
                ),
              ),
            );
          }

          final emptySlots = (stationState.detail?.slots ?? const <StationSlot>[])
              .where((s) => s.status == 'EMPTY')
              .toList(growable: false);

          if (state.orderInfo != null) {
            _prefillFromOrder(state.orderInfo!, emptySlots);
          }

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        '${station.name} (${station.stationCode})',
                        style: theme.textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        key: const Key('intake-tracking-input'),
                        controller: _trackingController,
                        textCapitalization: TextCapitalization.characters,
                        decoration: const InputDecoration(
                          labelText: 'Tracking number',
                          hintText: 'e.g. TRK-2026-0001',
                          prefixIcon: Icon(Icons.qr_code_scanner),
                          border: OutlineInputBorder(),
                        ),
                        onSubmitted: (_) => _lookup(stationId),
                      ),
                      const SizedBox(height: 12),
                      FilledButton.icon(
                        key: const Key('intake-lookup-button'),
                        onPressed:
                            state.isLoading ? null : () => _lookup(stationId),
                        icon: state.isLoading
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.search),
                        label: const Text('Look Up Order'),
                      ),
                    ],
                  ),
                ),
              ),
              if (state.errorMessage != null) ...[
                const SizedBox(height: 12),
                Card(
                  color: theme.colorScheme.errorContainer,
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Row(
                      children: [
                        Icon(
                          Icons.error_outline,
                          color: theme.colorScheme.onErrorContainer,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            state.errorMessage!,
                            style: TextStyle(
                              color: theme.colorScheme.onErrorContainer,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
              if (state.completedIntake != null) ...[
                const SizedBox(height: 12),
                _ResultCard(
                  icon: Icons.check_circle_outline,
                  color: Colors.green.shade700,
                  title: 'Package Intake Confirmed',
                  lines: [
                    'Tracking: ${state.completedIntake!.trackingNumber}',
                    'Status: ${state.completedIntake!.status}',
                    'Assigned Slot: #${state.completedIntake!.slotNumber}',
                  ],
                  onReset: () {
                    _prefilledOrderId = null;
                    _trackingController.clear();
                    ref.read(intakeControllerProvider.notifier).reset();
                  },
                ),
              ] else if (state.completedRejection != null) ...[
                const SizedBox(height: 12),
                _ResultCard(
                  icon: Icons.cancel_outlined,
                  color: theme.colorScheme.error,
                  title: 'Package Intake Rejected',
                  lines: [
                    'Tracking: ${state.completedRejection!.trackingNumber}',
                    'Status: ${state.completedRejection!.status}',
                  ],
                  onReset: () {
                    _prefilledOrderId = null;
                    _trackingController.clear();
                    ref.read(intakeControllerProvider.notifier).reset();
                  },
                ),
              ] else if (state.orderInfo != null) ...[
                const SizedBox(height: 12),
                _OrderDetailsCard(info: state.orderInfo!),
                const SizedBox(height: 12),
                if (!state.orderInfo!.isEligibleForIntake)
                  Card(
                    color: theme.colorScheme.surfaceContainerHighest,
                    child: const Padding(
                      padding: EdgeInsets.all(16),
                      child: Text(
                        'Only ORIGIN orders in APPROVED status can be accepted for intake at this station.',
                      ),
                    ),
                  )
                else
                  Form(
                    key: _formKey,
                    child: Card(
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            Text(
                              'Physical Inspection & Slot Assignment',
                              style: theme.textTheme.titleMedium?.copyWith(
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            const SizedBox(height: 12),
                            if (emptySlots.isEmpty)
                              Text(
                                'No EMPTY slots available at this station.',
                                style: TextStyle(color: theme.colorScheme.error),
                              )
                            else
                              DropdownButtonFormField<String>(
                                key: const Key('intake-slot-dropdown'),
                                value: emptySlots.any(
                                  (s) => s.id == _selectedSlotId,
                                )
                                    ? _selectedSlotId
                                    : emptySlots.first.id,
                                decoration: const InputDecoration(
                                  labelText: 'Assign empty slot',
                                  border: OutlineInputBorder(),
                                ),
                                items: emptySlots
                                    .map(
                                      (s) => DropdownMenuItem<String>(
                                        value: s.id,
                                        child: Text('Slot #${s.slotNumber}'),
                                      ),
                                    )
                                    .toList(growable: false),
                                onChanged: (val) =>
                                    setState(() => _selectedSlotId = val),
                              ),
                            const SizedBox(height: 12),
                            TextFormField(
                              key: const Key('intake-weight-input'),
                              controller: _weightController,
                              keyboardType:
                                  const TextInputType.numberWithOptions(
                                decimal: true,
                              ),
                              decoration: const InputDecoration(
                                labelText: 'Measured weight (g)',
                                border: OutlineInputBorder(),
                              ),
                              validator: (v) =>
                                  _positiveNumberValidator(v, 'weight'),
                            ),
                            const SizedBox(height: 12),
                            Row(
                              children: [
                                Expanded(
                                  child: TextFormField(
                                    key: const Key('intake-length-input'),
                                    controller: _lengthController,
                                    keyboardType:
                                        const TextInputType.numberWithOptions(
                                      decimal: true,
                                    ),
                                    decoration: const InputDecoration(
                                      labelText: 'L (cm)',
                                      border: OutlineInputBorder(),
                                    ),
                                    validator: (v) =>
                                        _positiveNumberValidator(v, 'length'),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: TextFormField(
                                    key: const Key('intake-width-input'),
                                    controller: _widthController,
                                    keyboardType:
                                        const TextInputType.numberWithOptions(
                                      decimal: true,
                                    ),
                                    decoration: const InputDecoration(
                                      labelText: 'W (cm)',
                                      border: OutlineInputBorder(),
                                    ),
                                    validator: (v) =>
                                        _positiveNumberValidator(v, 'width'),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: TextFormField(
                                    key: const Key('intake-height-input'),
                                    controller: _heightController,
                                    keyboardType:
                                        const TextInputType.numberWithOptions(
                                      decimal: true,
                                    ),
                                    decoration: const InputDecoration(
                                      labelText: 'H (cm)',
                                      border: OutlineInputBorder(),
                                    ),
                                    validator: (v) =>
                                        _positiveNumberValidator(v, 'height'),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 8),
                            CheckboxListTile(
                              key: const Key('intake-visual-checkbox'),
                              contentPadding: EdgeInsets.zero,
                              title: const Text('Visual inspection passed'),
                              value: _visualInspectionPassed,
                              onChanged: (val) => setState(
                                () => _visualInspectionPassed = val ?? false,
                              ),
                            ),
                            const SizedBox(height: 12),
                            FilledButton.icon(
                              key: const Key('intake-confirm-button'),
                              onPressed: state.isSubmitting || emptySlots.isEmpty
                                  ? null
                                  : () => _confirmIntake(
                                        stationId,
                                        state.orderInfo!,
                                      ),
                              icon: const Icon(Icons.inventory_outlined),
                              label: const Text('Confirm Intake'),
                            ),
                            const SizedBox(height: 8),
                            OutlinedButton.icon(
                              key: const Key('intake-reject-button'),
                              style: OutlinedButton.styleFrom(
                                foregroundColor: theme.colorScheme.error,
                              ),
                              onPressed: state.isSubmitting
                                  ? null
                                  : () => _showRejectDialog(
                                        stationId,
                                        state.orderInfo!,
                                      ),
                              icon: const Icon(Icons.block_outlined),
                              label: const Text('Reject Parcel'),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
              ],
            ],
          );
        },
      ),
    );
  }
}

class _OrderDetailsCard extends StatelessWidget {
  const _OrderDetailsCard({required this.info});

  final StationOrderInfo info;

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
              children: [
                Expanded(
                  child: Text(
                    info.packageName.isEmpty
                        ? info.trackingNumber
                        : info.packageName,
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                Chip(label: Text(info.status)),
              ],
            ),
            const SizedBox(height: 8),
            Text('Tracking: ${info.trackingNumber}'),
            Text('Role: ${info.role}'),
            Text(
              'Declared: ${StationOrderInfo.formatDecimal(info.declaredWeightGram)} g • '
              '${StationOrderInfo.formatDecimal(info.declaredLengthCm)}×'
              '${StationOrderInfo.formatDecimal(info.declaredWidthCm)}×'
              '${StationOrderInfo.formatDecimal(info.declaredHeightCm)} cm',
            ),
            if (info.isFragile) ...[
              const SizedBox(height: 6),
              Text(
                'Fragile handling required',
                style: TextStyle(
                  color: theme.colorScheme.error,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _ResultCard extends StatelessWidget {
  const _ResultCard({
    required this.icon,
    required this.color,
    required this.title,
    required this.lines,
    required this.onReset,
  });

  final IconData icon;
  final Color color;
  final String title;
  final List<String> lines;
  final VoidCallback onReset;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Icon(icon, color: color, size: 28),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    title,
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: color,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            for (final line in lines) Text(line),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: onReset,
              child: const Text('Intake Another Parcel'),
            ),
          ],
        ),
      ),
    );
  }
}
