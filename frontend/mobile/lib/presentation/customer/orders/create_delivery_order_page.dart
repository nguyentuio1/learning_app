import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/customer_address.dart';
import '../../../data/models/package_model.dart';
import '../../../presentation/customer/addresses/address_notifier.dart';
import '../../../presentation/customer/packages/package_notifier.dart';
import '../../../routes/route_paths.dart';
import 'create_order_notifier.dart';

class CreateDeliveryOrderPage extends ConsumerStatefulWidget {
  const CreateDeliveryOrderPage({super.key});

  @override
  ConsumerState<CreateDeliveryOrderPage> createState() =>
      _CreateDeliveryOrderPageState();
}

class _CreateDeliveryOrderPageState
    extends ConsumerState<CreateDeliveryOrderPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _originController;
  late final TextEditingController _destinationController;
  late final TextEditingController _nameController;
  late final TextEditingController _phoneController;

  @override
  void initState() {
    super.initState();
    final order = ref.read(createOrderNotifierProvider);
    _originController = TextEditingController(text: order.originStationId);
    _destinationController = TextEditingController(
      text: order.destinationStationId,
    );
    _nameController = TextEditingController(text: order.recipientName);
    _phoneController = TextEditingController(text: order.recipientPhone);
  }

  @override
  void dispose() {
    _originController.dispose();
    _destinationController.dispose();
    _nameController.dispose();
    _phoneController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final order = ref.watch(createOrderNotifierProvider);
    final packages = ref.watch(packageNotifierProvider);
    final addresses = ref.watch(addressNotifierProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Create delivery order')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            Text('Package', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            packages.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _LoadRetry(
                message: errorMessage(error),
                onRetry: () =>
                    ref.read(packageNotifierProvider.notifier).refresh(),
              ),
              data: (items) => DropdownButtonFormField<String>(
                key: const Key('create-order-package'),
                initialValue: order.package?.id,
                decoration: const InputDecoration(
                  labelText: 'Select a package',
                  border: OutlineInputBorder(),
                ),
                items: [
                  for (final package in items)
                    DropdownMenuItem(
                      value: package.id,
                      child: Text(
                        '${package.packageName} · ${_capacityLabel(package)}',
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                ],
                onChanged: (id) => ref
                    .read(createOrderNotifierProvider.notifier)
                    .selectPackage(
                      id == null
                          ? null
                          : items.firstWhere((package) => package.id == id),
                    ),
                validator: (id) => id == null ? 'Select a package.' : null,
              ),
            ),
            const SizedBox(height: 20),
            Text('Stations', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            const Text(
              'Enter the station IDs provided by your delivery service.',
            ),
            const SizedBox(height: 8),
            TextFormField(
              key: const Key('create-order-origin'),
              controller: _originController,
              decoration: const InputDecoration(
                labelText: 'Origin station ID',
                border: OutlineInputBorder(),
              ),
              onChanged: ref
                  .read(createOrderNotifierProvider.notifier)
                  .setOriginStationId,
              validator: _validateStationId,
            ),
            const SizedBox(height: 12),
            TextFormField(
              key: const Key('create-order-destination'),
              controller: _destinationController,
              decoration: const InputDecoration(
                labelText: 'Destination station ID',
                border: OutlineInputBorder(),
              ),
              onChanged: ref
                  .read(createOrderNotifierProvider.notifier)
                  .setDestinationStationId,
              validator: (value) {
                final invalid = _validateStationId(value);
                if (invalid != null) return invalid;
                if ((value ?? '').trim().toLowerCase() ==
                    _originController.text.trim().toLowerCase()) {
                  return 'Select different origin and destination stations.';
                }
                return null;
              },
            ),
            const SizedBox(height: 20),
            Text(
              'Delivery address (optional)',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            addresses.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _LoadRetry(
                message: errorMessage(error),
                onRetry: () =>
                    ref.read(addressNotifierProvider.notifier).refresh(),
              ),
              data: (items) => _AddressDropdown(
                items: items,
                selectedAddress: order.address,
                onChanged: (address) => ref
                    .read(createOrderNotifierProvider.notifier)
                    .selectAddress(address),
              ),
            ),
            const SizedBox(height: 20),
            Text(
              'Recipient details',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            TextFormField(
              key: const Key('create-order-recipient-name'),
              controller: _nameController,
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
                if (name.length > 150) {
                  return 'Recipient name must be at most 150 characters.';
                }
                return null;
              },
            ),
            const SizedBox(height: 12),
            TextFormField(
              key: const Key('create-order-recipient-phone'),
              controller: _phoneController,
              keyboardType: TextInputType.phone,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: const InputDecoration(
                labelText: 'Recipient phone',
                border: OutlineInputBorder(),
              ),
              onChanged: ref
                  .read(createOrderNotifierProvider.notifier)
                  .setRecipientPhone,
              validator: _validatePhone,
            ),
            if (order.error != null) ...[
              const SizedBox(height: 12),
              Text(
                order.error!.message,
                key: const Key('create-order-error'),
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            const SizedBox(height: 20),
            FilledButton(
              key: const Key('create-order-review'),
              onPressed: order.isSubmitting
                  ? null
                  : () {
                      final valid = _formKey.currentState!.validate();
                      if (!valid) return;
                      final validationError = ref
                          .read(createOrderNotifierProvider.notifier)
                          .validate();
                      if (validationError != null) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(content: Text(validationError.message)),
                        );
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

  String? _validateStationId(String? value) {
    final id = value?.trim() ?? '';
    if (!_guidPattern.hasMatch(id)) return 'Enter a valid station ID.';
    return null;
  }

  String? _validatePhone(String? value) {
    final phone = value?.trim() ?? '';
    if (phone.isEmpty) return 'Phone number is required.';
    if (!RegExp(r'^\d{9,11}$').hasMatch(phone)) {
      return 'Phone number must be 9 to 11 digits.';
    }
    return null;
  }

  String _capacityLabel(Package package) =>
      '${_display(package.weightGram)} g, ${_display(package.lengthCm)} × '
      '${_display(package.widthCm)} × ${_display(package.heightCm)} cm';
}

final _guidPattern = RegExp(
  r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
);

String _display(double value) =>
    value == value.roundToDouble() ? value.toInt().toString() : '$value';

class _AddressDropdown extends StatelessWidget {
  const _AddressDropdown({
    required this.items,
    required this.selectedAddress,
    required this.onChanged,
  });

  final List<CustomerAddress> items;
  final CustomerAddress? selectedAddress;
  final ValueChanged<CustomerAddress?> onChanged;

  @override
  Widget build(BuildContext context) => DropdownButtonFormField<String?>(
    key: const Key('create-order-address'),
    initialValue: selectedAddress?.id,
    decoration: const InputDecoration(
      labelText: 'Choose an address',
      border: OutlineInputBorder(),
    ),
    items: [
      const DropdownMenuItem<String?>(value: null, child: Text('No address')),
      for (final address in items)
        DropdownMenuItem<String?>(
          value: address.id,
          child: Text(
            '${address.addressLabel} · ${address.streetAddress}',
            overflow: TextOverflow.ellipsis,
          ),
        ),
    ],
    onChanged: (id) => onChanged(
      id == null ? null : items.firstWhere((address) => address.id == id),
    ),
  );
}

class _LoadRetry extends StatelessWidget {
  const _LoadRetry({required this.message, required this.onRetry});

  final String message;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) => Column(
    children: [
      Text(message),
      TextButton(onPressed: onRetry, child: const Text('Retry')),
    ],
  );
}
