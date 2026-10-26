import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/customer_address.dart';
import 'address_notifier.dart';
import 'add_edit_address_page.dart';

class AddressesListPage extends ConsumerWidget {
  const AddressesListPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.listen<AsyncValue<List<CustomerAddress>>>(addressNotifierProvider, (
      _,
      next,
    ) {
      if (next.hasError && !next.isLoading) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(errorMessage(next.error!))));
      }
    });
    final addresses = ref.watch(addressNotifierProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Saved addresses')),
      body: addresses.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(errorMessage(error)),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () =>
                    ref.read(addressNotifierProvider.notifier).refresh(),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (items) => items.isEmpty
            ? const Center(child: Text('No saved addresses'))
            : ListView.separated(
                padding: const EdgeInsets.all(16),
                itemCount: items.length,
                separatorBuilder: (_, _) => const SizedBox(height: 12),
                itemBuilder: (context, index) =>
                    _AddressCard(address: items[index]),
              ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _openForm(context),
        icon: const Icon(Icons.add),
        label: const Text('Add address'),
      ),
    );
  }

  Future<void> _openForm(BuildContext context) async {
    await Navigator.of(
      context,
    ).push<bool>(MaterialPageRoute(builder: (_) => const AddEditAddressPage()));
  }
}

class _AddressCard extends ConsumerWidget {
  const _AddressCard({required this.address});

  final CustomerAddress address;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isLoading = ref.watch(addressNotifierProvider).isLoading;

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
                    address.addressLabel,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                if (address.isDefault)
                  const Chip(
                    avatar: Icon(Icons.check, size: 18),
                    label: Text('Mặc định'),
                  ),
              ],
            ),
            const SizedBox(height: 8),
            Text(address.recipientName),
            Text(address.recipientPhone),
            Text(address.streetAddress),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                if (!address.isDefault)
                  OutlinedButton(
                    onPressed: isLoading
                        ? null
                        : () => ref
                              .read(addressNotifierProvider.notifier)
                              .setDefaultAddress(address.id),
                    child: const Text('Set default'),
                  ),
                OutlinedButton.icon(
                  onPressed: isLoading
                      ? null
                      : () => Navigator.of(context).push<bool>(
                          MaterialPageRoute(
                            builder: (_) =>
                                AddEditAddressPage(address: address),
                          ),
                        ),
                  icon: const Icon(Icons.edit),
                  label: const Text('Edit'),
                ),
                TextButton.icon(
                  onPressed: isLoading
                      ? null
                      : () => _confirmDelete(context, ref),
                  icon: const Icon(Icons.delete_outline),
                  label: const Text('Delete'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        content: const Text('Delete this address?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      await ref
          .read(addressNotifierProvider.notifier)
          .deleteAddress(address.id);
    }
  }
}
