import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/models/customer_address.dart';
import '../../../data/models/landing_station.dart';
import '../../../data/models/package_model.dart';
import '../../../data/providers/delivery_order_providers.dart';
import '../../../routes/route_paths.dart';
import 'create_order_notifier.dart';
import 'eta_estimation_controller.dart';
import 'widgets/eta_estimation_card.dart';

class CreateOrderPage extends ConsumerStatefulWidget {
  const CreateOrderPage({super.key});

  @override
  ConsumerState<CreateOrderPage> createState() => _CreateOrderPageState();
}

class _CreateOrderPageState extends ConsumerState<CreateOrderPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _name;
  late final TextEditingController _phone;
  late final TextEditingController _instructions;

  @override
  void initState() {
    super.initState();
    final state = ref.read(createOrderNotifierProvider);
    _name = TextEditingController(text: state.recipientName);
    _phone = TextEditingController(text: state.recipientPhone);
    _instructions = TextEditingController(text: state.instructions);
  }

  @override
  void dispose() {
    _name.dispose();
    _phone.dispose();
    _instructions.dispose();
    super.dispose();
  }

  String _formatPackageLabel(PackageModel package) {
    final kg = package.weightGram / 1000;
    final kgText = kg == kg.roundToDouble()
        ? '${kg.toInt()} kg'
        : '${kg.toStringAsFixed(1)} kg';
    final fragileText = package.isFragile ? ' · Fragile' : '';
    return '${package.packageName} · $kgText$fragileText';
  }

  LandingStation? _findStation(List<LandingStation> items, String id) {
    if (id.isEmpty) return null;
    for (final station in items) {
      if (station.id == id) return station;
    }
    return null;
  }

  Future<void> _showSavedAddressesSheet(
    BuildContext context,
    AsyncValue<List<CustomerAddress>> addressesAsync,
  ) async {
    await showModalBottomSheet<void>(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (sheetContext) {
        final theme = Theme.of(sheetContext);
        final items = addressesAsync.value ?? const <CustomerAddress>[];
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(20, 16, 20, 20),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Saved addresses',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 12),
                if (items.isEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 12),
                    child: Text(
                      'No saved addresses yet.',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  )
                else
                  Flexible(
                    child: ListView.separated(
                      shrinkWrap: true,
                      itemCount: items.length,
                      separatorBuilder: (_, _) => const Divider(height: 1),
                      itemBuilder: (_, index) {
                        final address = items[index];
                        return ListTile(
                          contentPadding: EdgeInsets.zero,
                          title: Text(
                            '${address.label} · ${address.contactName}',
                            style: theme.textTheme.titleSmall?.copyWith(
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                          subtitle: Text(
                            '${address.contactPhone} · ${address.addressLine}',
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                          onTap: () {
                            final notifier = ref.read(
                              createOrderNotifierProvider.notifier,
                            );
                            notifier.selectAddress(address);
                            _name.text = address.contactName;
                            _phone.text = address.contactPhone;
                            notifier.setRecipientName(address.contactName);
                            notifier.setRecipientPhone(address.contactPhone);
                            Navigator.of(sheetContext).pop();
                          },
                        );
                      },
                    ),
                  ),
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  key: const Key('order-manage-addresses-button'),
                  onPressed: () async {
                    Navigator.of(sheetContext).pop();
                    await context.push(RoutePaths.customerAddresses);
                    ref.invalidate(orderAddressesProvider);
                  },
                  icon: const Icon(Icons.location_on_outlined, size: 18),
                  label: const Text('Manage addresses'),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final state = ref.watch(createOrderNotifierProvider);
    final packages = ref.watch(orderPackagesProvider);
    final stations = ref.watch(orderStationsProvider);
    final addresses = ref.watch(orderAddressesProvider);
    final routePreview = ref.watch(routeEtaPreviewProvider);

    return Scaffold(
      resizeToAvoidBottomInset: true,
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => Navigator.of(context).maybePop(),
        ),
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Create Delivery',
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            Text(
              'Enter your delivery information',
              style: theme.textTheme.bodySmall?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
      body: Form(
        key: _formKey,
        child: ListView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: const EdgeInsets.all(24),
          children: [
            Text(
              'Package Details',
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 12),
            Text(
              'Select Package',
              style: theme.textTheme.titleSmall?.copyWith(
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: 8),
            packages.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _LoadError(
                message: 'Unable to load packages. Please try again.',
                onRetry: () => ref.invalidate(orderPackagesProvider),
              ),
              data: (items) => items.isEmpty
                  ? Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const Text(
                          'No packages available yet. Add a package to continue.',
                        ),
                        const SizedBox(height: 8),
                        OutlinedButton.icon(
                          key: const Key('order-add-package-button'),
                          onPressed: () async {
                            await context.push<bool>(
                              RoutePaths.customerPackagesAdd,
                            );
                            ref.invalidate(orderPackagesProvider);
                          },
                          icon: const Icon(Icons.add),
                          label: const Text('+ Add New Package'),
                        ),
                      ],
                    )
                  : Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        DropdownButtonFormField<String>(
                          key: const Key('order-package'),
                          isExpanded: true,
                          icon: const Icon(Icons.keyboard_arrow_down),
                          initialValue: state.package?.id,
                          decoration: const InputDecoration(
                            hintText: 'Choose a package',
                            border: OutlineInputBorder(),
                          ),
                          items: [
                            for (final package in items)
                              DropdownMenuItem(
                                value: package.id,
                                enabled:
                                    !package.isAssigned &&
                                    package.orderId == null,
                                child: Text(
                                  '${_formatPackageLabel(package)}'
                                  '${package.isAssigned || package.orderId != null ? ' (In use)' : ''}',
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                          ],
                          onChanged: (id) {
                            if (id == null) return;
                            final selected = items.firstWhere(
                              (item) => item.id == id,
                            );
                            if (!selected.isAssigned &&
                                selected.orderId == null) {
                              ref
                                  .read(createOrderNotifierProvider.notifier)
                                  .selectPackage(selected);
                            }
                          },
                          validator: (value) =>
                              value == null ? 'Select a package.' : null,
                        ),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: TextButton(
                            key: const Key('order-add-package-button'),
                            onPressed: () async {
                              await context.push<bool>(
                                RoutePaths.customerPackagesAdd,
                              );
                              ref.invalidate(orderPackagesProvider);
                            },
                            child: const Text('+ Add New Package'),
                          ),
                        ),
                      ],
                    ),
            ),
            const SizedBox(height: 20),
            Text(
              'Delivery Route',
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 12),
            stations.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _LoadError(
                message: 'Unable to load stations. Please try again.',
                onRetry: () => ref.invalidate(orderStationsProvider),
              ),
              data: (items) {
                final originStation = _findStation(
                  items,
                  state.originStationId,
                );
                final destinationStation = _findStation(
                  items,
                  state.destinationStationId,
                );

                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'Origin Station',
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 8),
                    _StationPicker(
                      fieldKey: const Key('order-origin'),
                      hintText: 'Select origin station',
                      value: state.originStationId.isEmpty
                          ? null
                          : state.originStationId,
                      items: items,
                      onChanged: ref
                          .read(createOrderNotifierProvider.notifier)
                          .setOriginStationId,
                      validator: (id) {
                        if (id == null) return 'Select an origin station.';
                        return id == state.destinationStationId
                            ? 'Select different origin and destination stations.'
                            : null;
                      },
                    ),
                    if (originStation != null) ...[
                      const SizedBox(height: 6),
                      _StationHelperText(station: originStation),
                    ],
                    const SizedBox(height: 14),
                    Text(
                      'Destination Station',
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 8),
                    _StationPicker(
                      fieldKey: const Key('order-destination'),
                      hintText: 'Select destination station',
                      value: state.destinationStationId.isEmpty
                          ? null
                          : state.destinationStationId,
                      items: items,
                      onChanged: ref
                          .read(createOrderNotifierProvider.notifier)
                          .setDestinationStationId,
                      validator: (id) {
                        if (id == null) return 'Select a destination station.';
                        return id == state.originStationId
                            ? 'Select different origin and destination stations.'
                            : null;
                      },
                    ),
                    if (destinationStation != null) ...[
                      const SizedBox(height: 6),
                      _StationHelperText(station: destinationStation),
                    ],
                  ],
                );
              },
            ),
            const SizedBox(height: 24),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Recipient Information',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                TextButton(
                  onPressed: () => _showSavedAddressesSheet(context, addresses),
                  child: const Text('Use saved address'),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              'Recipient Name',
              style: theme.textTheme.titleSmall?.copyWith(
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: 8),
            TextFormField(
              key: const Key('order-recipient-name'),
              controller: _name,
              maxLength: 150,
              textCapitalization: TextCapitalization.words,
              decoration: const InputDecoration(
                hintText: 'Enter recipient name',
                counterText: '',
                border: OutlineInputBorder(),
              ),
              onChanged: ref
                  .read(createOrderNotifierProvider.notifier)
                  .setRecipientName,
              validator: (value) {
                final name = value?.trim() ?? '';
                if (name.isEmpty) return 'Recipient name is required.';
                return name.length > 150
                    ? 'Recipient name must be at most 150 characters.'
                    : null;
              },
            ),
            const SizedBox(height: 14),
            Text(
              'Recipient Phone',
              style: theme.textTheme.titleSmall?.copyWith(
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: 8),
            TextFormField(
              key: const Key('order-recipient-phone'),
              controller: _phone,
              keyboardType: TextInputType.phone,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: const InputDecoration(
                hintText: 'Enter phone number',
                border: OutlineInputBorder(),
              ),
              onChanged: ref
                  .read(createOrderNotifierProvider.notifier)
                  .setRecipientPhone,
              validator: (value) {
                final phone = value?.trim() ?? '';
                if (phone.isEmpty) return 'Phone number is required.';
                return RegExp(r'^\d{9,11}$').hasMatch(phone)
                    ? null
                    : 'Phone number must be 9 to 11 digits.';
              },
            ),
            if (routePreview != null) ...[
              const SizedBox(height: 20),
              RouteEtaPreviewCard(preview: routePreview),
            ],
            const SizedBox(height: 20),
            Row(
              children: [
                Text(
                  'Delivery Instructions',
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                Text(
                  ' (optional)',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            TextFormField(
              key: const Key('order-instructions'),
              controller: _instructions,
              maxLines: 3,
              decoration: const InputDecoration(
                hintText: 'Add special instructions',
                border: OutlineInputBorder(),
              ),
              onChanged: ref
                  .read(createOrderNotifierProvider.notifier)
                  .setInstructions,
            ),
            if (state.error != null)
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Text(
                  state.error!.message,
                  key: const Key('order-error'),
                  style: TextStyle(color: colorScheme.error),
                ),
              ),
            const SizedBox(height: 24),
            FilledButton(
              key: const Key('order-review'),
              style: FilledButton.styleFrom(
                minimumSize: const Size.fromHeight(52),
              ),
              onPressed: () {
                if (!_formKey.currentState!.validate()) return;
                final error = ref
                    .read(createOrderNotifierProvider.notifier)
                    .validate();
                if (error != null) {
                  ScaffoldMessenger.of(context)
                      .showSnackBar(SnackBar(content: Text(error.message)));
                  return;
                }
                context.push(RoutePaths.customerOrdersReview);
              },
              child: const Text('Review Delivery'),
            ),
            const SizedBox(height: 8),
            Text(
              'Review your details before submitting.',
              textAlign: TextAlign.center,
              style: theme.textTheme.bodySmall?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StationHelperText extends StatelessWidget {
  const _StationHelperText({required this.station});

  final LandingStation station;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final slots = station.availableSlots ?? 0;
    final slotWord = slots == 1 ? 'slot' : 'slots';

    return Text(
      'Operational · $slots $slotWord free',
      style: theme.textTheme.bodySmall?.copyWith(
        color: theme.colorScheme.onSurfaceVariant,
      ),
    );
  }
}

class _StationPicker extends StatelessWidget {
  const _StationPicker({
    required this.fieldKey,
    required this.hintText,
    required this.value,
    required this.items,
    required this.onChanged,
    this.validator,
  });

  final Key fieldKey;
  final String hintText;
  final String? value;
  final List<LandingStation> items;
  final ValueChanged<String> onChanged;
  final FormFieldValidator<String>? validator;

  @override
  Widget build(BuildContext context) => DropdownButtonFormField<String>(
    key: fieldKey,
    isExpanded: true,
    icon: const Icon(Icons.keyboard_arrow_down),
    initialValue: value,
    decoration: InputDecoration(
      hintText: hintText,
      border: const OutlineInputBorder(),
    ),
    items: [
      for (final station in items)
        DropdownMenuItem(
          value: station.id,
          child: Text(
            '${station.name} · ${station.stationCode}',
            overflow: TextOverflow.ellipsis,
          ),
        ),
    ],
    onChanged: (id) {
      if (id != null) onChanged(id);
    },
    validator: validator ?? (id) => id == null ? 'Select a station.' : null,
  );
}

class _LoadError extends StatelessWidget {
  const _LoadError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Column(
    children: [
      Text(message),
      TextButton(onPressed: onRetry, child: const Text('Retry')),
    ],
  );
}
