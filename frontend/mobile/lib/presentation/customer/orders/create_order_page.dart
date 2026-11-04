import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/models/customer_address.dart';
import '../../../data/models/landing_station.dart';
import '../../../data/providers/delivery_order_providers.dart';
import '../../../routes/route_paths.dart';
import 'create_order_notifier.dart';

class CreateOrderPage extends ConsumerStatefulWidget {
  const CreateOrderPage({super.key});

  @override
  ConsumerState<CreateOrderPage> createState() => _CreateOrderPageState();
}

class _CreateOrderPageState extends ConsumerState<CreateOrderPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _name;
  late final TextEditingController _phone;

  @override
  void initState() {
    super.initState();
    final state = ref.read(createOrderNotifierProvider);
    _name = TextEditingController(text: state.recipientName);
    _phone = TextEditingController(text: state.recipientPhone);
  }

  @override
  void dispose() {
    _name.dispose();
    _phone.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(createOrderNotifierProvider);
    final packages = ref.watch(orderPackagesProvider);
    final stations = ref.watch(orderStationsProvider);
    final addresses = ref.watch(orderAddressesProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Create delivery order')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            packages.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _LoadError(
                message: 'Unable to load packages. Please try again.',
                onRetry: () => ref.invalidate(orderPackagesProvider),
              ),
              data: (items) => DropdownButtonFormField<String>(
                key: const Key('order-package'),
                initialValue: state.package?.id,
                decoration: const InputDecoration(
                  labelText: 'Package',
                  border: OutlineInputBorder(),
                ),
                items: [
                  for (final package in items)
                    DropdownMenuItem(
                      value: package.id,
                      child: Text(package.packageName),
                    ),
                ],
                onChanged: (id) => ref
                    .read(createOrderNotifierProvider.notifier)
                    .selectPackage(
                      id == null
                          ? null
                          : items.firstWhere((item) => item.id == id),
                    ),
                validator: (value) =>
                    value == null ? 'Select a package.' : null,
              ),
            ),
            const SizedBox(height: 16),
            stations.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _LoadError(
                message: 'Unable to load stations. Please try again.',
                onRetry: () => ref.invalidate(orderStationsProvider),
              ),
              data: (items) => Column(
                children: [
                  _StationPicker(
                    fieldKey: const Key('order-origin'),
                    label: 'Origin station',
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
                  const SizedBox(height: 12),
                  _StationPicker(
                    fieldKey: const Key('order-destination'),
                    label: 'Destination station',
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
                ],
              ),
            ),
            const SizedBox(height: 16),
            addresses.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _LoadError(
                message: 'Unable to load addresses. Please try again.',
                onRetry: () => ref.invalidate(orderAddressesProvider),
              ),
              data: (items) => _AddressPicker(
                items: items,
                selected: state.address,
                onChanged: ref
                    .read(createOrderNotifierProvider.notifier)
                    .selectAddress,
              ),
            ),
            const SizedBox(height: 16),
            TextFormField(
              key: const Key('order-recipient-name'),
              controller: _name,
              maxLength: 150,
              textCapitalization: TextCapitalization.words,
              decoration: const InputDecoration(
                labelText: 'Recipient name',
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
            const SizedBox(height: 12),
            TextFormField(
              key: const Key('order-recipient-phone'),
              controller: _phone,
              keyboardType: TextInputType.phone,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: const InputDecoration(
                labelText: 'Recipient phone',
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
            if (state.error != null)
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Text(
                  state.error!.message,
                  key: const Key('order-error'),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            const SizedBox(height: 20),
            FilledButton(
              key: const Key('order-review'),
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
              child: const Text('Review order'),
            ),
          ],
        ),
      ),
    );
  }
}

class _StationPicker extends StatelessWidget {
  const _StationPicker({
    required this.fieldKey,
    required this.label,
    required this.value,
    required this.items,
    required this.onChanged,
    this.validator,
  });

  final Key fieldKey;
  final String label;
  final String? value;
  final List<LandingStation> items;
  final ValueChanged<String> onChanged;
  final FormFieldValidator<String>? validator;

  @override
  Widget build(BuildContext context) => DropdownButtonFormField<String>(
    key: fieldKey,
    initialValue: value,
    decoration: InputDecoration(
      labelText: label,
      border: const OutlineInputBorder(),
    ),
    items: [
      for (final station in items)
        DropdownMenuItem(
          value: station.id,
          child: Text('${station.name} · ${station.stationCode}'),
        ),
    ],
    onChanged: (id) {
      if (id != null) onChanged(id);
    },
    validator:
        validator ??
        (id) => id == null ? 'Select a ${label.toLowerCase()}.' : null,
  );
}

class _AddressPicker extends StatelessWidget {
  const _AddressPicker({
    required this.items,
    required this.selected,
    required this.onChanged,
  });

  final List<CustomerAddress> items;
  final CustomerAddress? selected;
  final ValueChanged<CustomerAddress?> onChanged;

  @override
  Widget build(BuildContext context) => DropdownButtonFormField<String?>(
    key: const Key('order-address'),
    initialValue: selected?.id,
    decoration: const InputDecoration(
      labelText: 'Delivery address (optional)',
      border: OutlineInputBorder(),
    ),
    items: [
      const DropdownMenuItem<String?>(value: null, child: Text('No address')),
      for (final address in items)
        DropdownMenuItem<String?>(
          value: address.id,
          child: Text('${address.label} · ${address.addressLine}'),
        ),
    ],
    onChanged: (id) => onChanged(
      id == null ? null : items.firstWhere((item) => item.id == id),
    ),
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
