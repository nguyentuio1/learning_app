import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/error_message.dart';
import '../../../core/session/auth_controller.dart';
import '../../../data/models/landing_station.dart';
import '../../../data/models/station_availability.dart';
import '../../../data/models/station_detail.dart';
import '../../../data/models/station_slot.dart';
import '../../../routes/route_paths.dart';
import 'station_availability_providers.dart';

class OperatorHomePage extends ConsumerWidget {
  const OperatorHomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final asyncState = ref.watch(stationAvailabilityNotifierProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Station availability'),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh),
            onPressed: () =>
                ref.read(stationAvailabilityNotifierProvider.notifier).refresh(),
          ),
          IconButton(
            tooltip: 'Profile & Settings',
            icon: const Icon(Icons.account_circle_outlined),
            onPressed: () => context.push(RoutePaths.profile),
          ),
          IconButton(
            tooltip: 'Sign out',
            icon: const Icon(Icons.logout),
            onPressed: () =>
                ref.read(authControllerProvider.notifier).signOut(),
          ),
        ],
      ),
      body: asyncState.when(
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
                  color: Theme.of(context).colorScheme.error,
                ),
                const SizedBox(height: 16),
                Text(
                  errorMessage(error),
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.bodyMedium,
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
        data: (state) {
          if (state.isUnassignedOperator) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(
                      Icons.assignment_late_outlined,
                      size: 64,
                      color: Theme.of(context).colorScheme.primary,
                    ),
                    const SizedBox(height: 16),
                    Text(
                      'No station assigned',
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                            fontWeight: FontWeight.bold,
                          ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'You do not have an assigned landing station yet. Please contact your administrator to be assigned to a station.',
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
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

          if (state.stations.isEmpty) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(
                      Icons.location_city_outlined,
                      size: 64,
                      color: Colors.grey,
                    ),
                    const SizedBox(height: 16),
                    Text(
                      'No landing stations found.',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 16),
                    OutlinedButton(
                      onPressed: () => ref
                          .read(stationAvailabilityNotifierProvider.notifier)
                          .refresh(),
                      child: const Text('Reload'),
                    ),
                  ],
                ),
              ),
            );
          }

          final currentStation = state.currentStation;
          final availability = state.availability;
          final detail = state.detail;

          return RefreshIndicator(
            onRefresh: () => ref
                .read(stationAvailabilityNotifierProvider.notifier)
                .refresh(),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                // Station Selector & Info Card
                _StationHeaderCard(
                  stations: state.stations,
                  selectedStationId: state.selectedStationId,
                  currentStation: currentStation,
                  detail: detail,
                  onStationChanged: (id) {
                    if (id != null) {
                      ref
                          .read(stationAvailabilityNotifierProvider.notifier)
                          .selectStation(id);
                    }
                  },
                ),
                const SizedBox(height: 12),

                // Quick Action: Package Pickup Confirmation (LA-29 / LA-126)
                FilledButton.icon(
                  key: const Key('operator-pickup-button'),
                  onPressed: () => context.push(RoutePaths.operatorPickup),
                  icon: const Icon(Icons.mark_email_read_outlined),
                  label: const Text('Confirm Package Pickup'),
                ),
                const SizedBox(height: 16),

                // Availability Metrics Card
                if (availability != null) ...[
                  _AvailabilityMetricsCard(availability: availability),
                  const SizedBox(height: 16),
                ],

                // Slots Breakdown Grid (O11)
                _SlotsSection(
                  slots: state.filteredSlots,
                  totalSlots: availability?.totalSlots ??
                      detail?.totalSlots ??
                      currentStation?.totalSlots ??
                      0,
                  emptyCount: availability?.emptySlots ?? detail?.emptySlots ?? 0,
                  occupiedCount: availability?.occupiedSlots ?? 0,
                  reservedCount: availability?.reservedSlots ?? 0,
                  errorCount: availability?.errorSlots ?? 0,
                  selectedFilter: state.slotFilter,
                  onFilterSelected: (filter) => ref
                      .read(stationAvailabilityNotifierProvider.notifier)
                      .setSlotFilter(filter),
                  onSlotTapped: (slot) => _showSlotDetailsModal(context, slot),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  static void _showSlotDetailsModal(BuildContext context, StationSlot slot) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) {
        final (color, icon) = _slotStatusStyle(context, slot.status);
        return Padding(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: Colors.grey.shade300,
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              Row(
                children: [
                  CircleAvatar(
                    backgroundColor: color.withOpacity(0.15),
                    child: Icon(icon, color: color),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Slot #${slot.slotNumber}',
                          style: Theme.of(context).textTheme.titleLarge?.copyWith(
                                fontWeight: FontWeight.bold,
                              ),
                        ),
                        Text(
                          'ID: ${slot.id}',
                          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                                color: Colors.grey.shade600,
                              ),
                        ),
                      ],
                    ),
                  ),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: color.withOpacity(0.15),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Text(
                      slot.status,
                      style: TextStyle(
                        color: color,
                        fontWeight: FontWeight.bold,
                        fontSize: 12,
                      ),
                    ),
                  ),
                ],
              ),
              const Divider(height: 32),
              _DetailRow(
                label: 'Status',
                value: slot.status,
                icon: Icons.info_outline,
              ),
              const SizedBox(height: 12),
              _DetailRow(
                label: 'Last Updated',
                value: slot.lastUpdated.toLocal().toString().split('.').first,
                icon: Icons.access_time,
              ),
              if (slot.currentOrderId != null) ...[
                const SizedBox(height: 12),
                _DetailRow(
                  label: 'Current Order ID',
                  value: slot.currentOrderId!,
                  icon: Icons.inventory_2_outlined,
                ),
              ],
              if (slot.currentMissionId != null) ...[
                const SizedBox(height: 12),
                _DetailRow(
                  label: 'Flight Mission ID',
                  value: slot.currentMissionId!,
                  icon: Icons.flight_outlined,
                ),
              ],
              const SizedBox(height: 24),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text('Close'),
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  static (Color, IconData) _slotStatusStyle(
    BuildContext context,
    String status,
  ) {
    final scheme = Theme.of(context).colorScheme;
    return switch (status) {
      'EMPTY' => (Colors.green.shade700, Icons.check_circle_outline),
      'OCCUPIED' => (scheme.primary, Icons.flight_land),
      'RESERVED' => (Colors.orange.shade800, Icons.schedule),
      'ERROR' => (scheme.error, Icons.warning_amber_rounded),
      _ => (Colors.grey, Icons.help_outline),
    };
  }
}

class _StationHeaderCard extends StatelessWidget {
  const _StationHeaderCard({
    required this.stations,
    required this.selectedStationId,
    required this.currentStation,
    required this.detail,
    required this.onStationChanged,
  });

  final List<LandingStation> stations;
  final String? selectedStationId;
  final LandingStation? currentStation;
  final StationDetail? detail;
  final ValueChanged<String?> onStationChanged;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final status = detail?.status ?? currentStation?.status ?? 'OPERATIONAL';
    final name = detail?.name ?? currentStation?.name ?? 'Unknown Station';
    final code = detail?.stationCode ?? currentStation?.stationCode ?? '';
    final address = detail?.address ?? currentStation?.address ?? '';
    final hostedDrones = detail?.hostedDroneCount ?? 0;

    final (statusColor, statusIcon) = switch (status) {
      'OPERATIONAL' => (Colors.green.shade700, Icons.check_circle),
      'MAINTENANCE' => (Colors.orange.shade800, Icons.build),
      _ => (Colors.red.shade700, Icons.cancel),
    };

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Station Dropdown Selector if multiple stations
            if (stations.length > 1) ...[
              DropdownButtonFormField<String>(
                value: selectedStationId,
                decoration: const InputDecoration(
                  labelText: 'Selected Station',
                  prefixIcon: Icon(Icons.hub_outlined),
                  isDense: true,
                ),
                items: stations.map((s) {
                  return DropdownMenuItem<String>(
                    value: s.id,
                    child: Text(
                      '${s.name} (${s.stationCode})',
                      overflow: TextOverflow.ellipsis,
                    ),
                  );
                }).toList(),
                onChanged: onStationChanged,
              ),
              const SizedBox(height: 16),
            ],

            // Station Name & Code Badge
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        name,
                        style: theme.textTheme.titleLarge?.copyWith(
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Row(
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 8,
                              vertical: 2,
                            ),
                            decoration: BoxDecoration(
                              color: theme.colorScheme.primaryContainer,
                              borderRadius: BorderRadius.circular(6),
                            ),
                            child: Text(
                              code,
                              style: TextStyle(
                                color: theme.colorScheme.onPrimaryContainer,
                                fontWeight: FontWeight.bold,
                                fontSize: 12,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 8,
                              vertical: 2,
                            ),
                            decoration: BoxDecoration(
                              color: statusColor.withOpacity(0.12),
                              borderRadius: BorderRadius.circular(6),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Icon(statusIcon, size: 14, color: statusColor),
                                const SizedBox(width: 4),
                                Text(
                                  status,
                                  style: TextStyle(
                                    color: statusColor,
                                    fontWeight: FontWeight.bold,
                                    fontSize: 12,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                if (hostedDrones > 0)
                  Chip(
                    avatar: const Icon(Icons.flight, size: 16),
                    label: Text('$hostedDrones hosted'),
                    padding: EdgeInsets.zero,
                    visualDensity: VisualDensity.compact,
                  ),
              ],
            ),
            if (address.isNotEmpty) ...[
              const SizedBox(height: 12),
              Row(
                children: [
                  Icon(
                    Icons.location_on_outlined,
                    size: 16,
                    color: Colors.grey.shade600,
                  ),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Text(
                      address,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: Colors.grey.shade700,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _AvailabilityMetricsCard extends StatelessWidget {
  const _AvailabilityMetricsCard({required this.availability});

  final StationAvailability availability;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final total = availability.totalSlots;
    final empty = availability.emptySlots;
    final occupied = availability.occupiedSlots;
    final reserved = availability.reservedSlots;
    final error = availability.errorSlots;
    final ratio = total > 0 ? empty / total : 0.0;

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
                  'Live Availability',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
                Text(
                  'Checked ${availability.checkedAt.toLocal().toString().split('.').first.split(' ').last}',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: Colors.grey.shade600,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),

            // Capacity Progress Bar
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Available capacity',
                  style: theme.textTheme.bodyMedium,
                ),
                Text(
                  '$empty / $total slots empty',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: empty > 0 ? Colors.green.shade700 : Colors.red,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: LinearProgressIndicator(
                value: ratio,
                minHeight: 8,
                backgroundColor: Colors.grey.shade200,
                color: empty > 0 ? Colors.green.shade600 : Colors.grey,
              ),
            ),
            const SizedBox(height: 16),

            // Status Counters
            Row(
              children: [
                Expanded(
                  child: _MetricCounterBox(
                    count: empty,
                    label: 'EMPTY',
                    sublabel: 'Available',
                    color: Colors.green.shade700,
                    icon: Icons.check_circle_outline,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _MetricCounterBox(
                    count: occupied,
                    label: 'OCCUPIED',
                    sublabel: 'In Use',
                    color: theme.colorScheme.primary,
                    icon: Icons.flight_land,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _MetricCounterBox(
                    count: reserved,
                    label: 'RESERVED',
                    sublabel: 'Booked',
                    color: Colors.orange.shade800,
                    icon: Icons.schedule,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _MetricCounterBox(
                    count: error,
                    label: 'ERROR',
                    sublabel: 'Maintenance',
                    color: theme.colorScheme.error,
                    icon: Icons.warning_amber_rounded,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),

            // Arrival & Departure Readiness
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              decoration: BoxDecoration(
                color: theme.colorScheme.surfaceVariant.withOpacity(0.4),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: _ReadinessIndicator(
                      title: 'Arrival readiness',
                      isReady: availability.isAvailableForArrival,
                    ),
                  ),
                  Container(
                    width: 1,
                    height: 28,
                    color: Colors.grey.shade300,
                  ),
                  Expanded(
                    child: _ReadinessIndicator(
                      title: 'Departure readiness',
                      isReady: availability.isAvailableForDeparture,
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

class _MetricCounterBox extends StatelessWidget {
  const _MetricCounterBox({
    required this.count,
    required this.label,
    required this.sublabel,
    required this.color,
    required this.icon,
  });

  final int count;
  final String label;
  final String sublabel;
  final Color color;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 4),
      decoration: BoxDecoration(
        color: color.withOpacity(0.08),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: color.withOpacity(0.2)),
      ),
      child: Column(
        children: [
          Icon(icon, size: 18, color: color),
          const SizedBox(height: 4),
          Text(
            '$count',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.bold,
              color: color,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w600,
              color: color,
            ),
          ),
          Text(
            sublabel,
            style: TextStyle(
              fontSize: 9,
              color: Colors.grey.shade600,
            ),
          ),
        ],
      ),
    );
  }
}

class _ReadinessIndicator extends StatelessWidget {
  const _ReadinessIndicator({
    required this.title,
    required this.isReady,
  });

  final String title;
  final bool isReady;

  @override
  Widget build(BuildContext context) {
    final color = isReady ? Colors.green.shade700 : Colors.grey.shade600;
    final icon = isReady ? Icons.check_circle : Icons.block;
    final text = isReady ? 'Ready' : 'Blocked';

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Icon(icon, size: 16, color: color),
        const SizedBox(width: 6),
        Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              title,
              style: const TextStyle(fontSize: 11, color: Colors.grey),
            ),
            Text(
              text,
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.bold,
                color: color,
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _SlotsSection extends StatelessWidget {
  const _SlotsSection({
    required this.slots,
    required this.totalSlots,
    required this.emptyCount,
    required this.occupiedCount,
    required this.reservedCount,
    required this.errorCount,
    required this.selectedFilter,
    required this.onFilterSelected,
    required this.onSlotTapped,
  });

  final List<StationSlot> slots;
  final int totalSlots;
  final int emptyCount;
  final int occupiedCount;
  final int reservedCount;
  final int errorCount;
  final String selectedFilter;
  final ValueChanged<String> onFilterSelected;
  final ValueChanged<StationSlot> onSlotTapped;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    final filters = [
      ('ALL', 'All ($totalSlots)'),
      ('EMPTY', 'Empty ($emptyCount)'),
      ('OCCUPIED', 'Occupied ($occupiedCount)'),
      ('RESERVED', 'Reserved ($reservedCount)'),
      ('ERROR', 'Error ($errorCount)'),
    ];

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
                  'Landing & locker slots',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
                Text(
                  '${slots.length} showing',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: Colors.grey.shade600,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),

            // Filter Chips
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: filters.map((f) {
                  final isSelected = selectedFilter == f.$1;
                  return Padding(
                    padding: const EdgeInsets.only(right: 6),
                    child: FilterChip(
                      selected: isSelected,
                      label: Text(f.$2),
                      labelStyle: TextStyle(
                        fontSize: 12,
                        fontWeight:
                            isSelected ? FontWeight.bold : FontWeight.normal,
                      ),
                      onSelected: (_) => onFilterSelected(f.$1),
                    ),
                  );
                }).toList(),
              ),
            ),
            const SizedBox(height: 16),

            if (slots.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 24),
                child: Center(
                  child: Text(
                    'No slots matching "$selectedFilter"',
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: Colors.grey.shade600,
                    ),
                  ),
                ),
              )
            else
              GridView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: 2,
                  crossAxisSpacing: 10,
                  mainAxisSpacing: 10,
                  childAspectRatio: 1.35,
                ),
                itemCount: slots.length,
                itemBuilder: (context, index) {
                  final slot = slots[index];
                  final (color, icon) =
                      OperatorHomePage._slotStatusStyle(context, slot.status);

                  return InkWell(
                    onTap: () => onSlotTapped(slot),
                    borderRadius: BorderRadius.circular(12),
                    child: Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: color.withOpacity(0.06),
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(
                          color: color.withOpacity(0.3),
                          width: 1.2,
                        ),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Text(
                                'Slot ${slot.slotNumber}',
                                style: theme.textTheme.titleSmall?.copyWith(
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              Icon(icon, size: 16, color: color),
                            ],
                          ),
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 6,
                              vertical: 2,
                            ),
                            decoration: BoxDecoration(
                              color: color.withOpacity(0.15),
                              borderRadius: BorderRadius.circular(6),
                            ),
                            child: Text(
                              slot.status,
                              style: TextStyle(
                                color: color,
                                fontWeight: FontWeight.bold,
                                fontSize: 11,
                              ),
                            ),
                          ),
                          Text(
                            slot.currentOrderId != null
                                ? 'Order active'
                                : slot.currentMissionId != null
                                    ? 'Mission booked'
                                    : 'Updated ${slot.lastUpdated.toLocal().toString().split('.').first.split(' ').last}',
                            style: TextStyle(
                              fontSize: 10,
                              color: Colors.grey.shade700,
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
          ],
        ),
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({
    required this.label,
    required this.value,
    required this.icon,
  });

  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 18, color: Colors.grey.shade600),
        const SizedBox(width: 10),
        Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              style: TextStyle(fontSize: 12, color: Colors.grey.shade600),
            ),
            Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
            ),
          ],
        ),
      ],
    );
  }
}
