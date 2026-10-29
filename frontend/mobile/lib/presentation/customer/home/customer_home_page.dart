import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/session/auth_controller.dart';
import '../../../routes/route_paths.dart';

class CustomerHomePage extends ConsumerWidget {
  const CustomerHomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
    appBar: AppBar(
      title: const Text('Find a station'),
      actions: [
        IconButton(
          tooltip: 'Profile & Settings',
          icon: const Icon(Icons.account_circle_outlined),
          onPressed: () => context.push(RoutePaths.profile),
        ),
        IconButton(
          tooltip: 'Sign out',
          icon: const Icon(Icons.logout),
          onPressed: () => ref.read(authControllerProvider.notifier).signOut(),
        ),
      ],
    ),
    body: Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('Customer app'),
          const SizedBox(height: 16),
          FilledButton.icon(
            onPressed: () => context.go(RoutePaths.customerAddresses),
            icon: const Icon(Icons.location_on_outlined),
            label: const Text('Manage addresses'),
          ),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: () => context.go(RoutePaths.customerPackages),
            icon: const Icon(Icons.inventory_2_outlined),
            label: const Text('Manage packages'),
          ),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: () => context.go(RoutePaths.customerOrders),
            icon: const Icon(Icons.local_shipping_outlined),
            label: const Text('My orders'),
          ),
        ],
      ),
    ),
  );
}
