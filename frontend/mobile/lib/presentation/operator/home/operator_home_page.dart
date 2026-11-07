import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/session/auth_controller.dart';
import '../../../routes/route_paths.dart';

class OperatorHomePage extends ConsumerWidget {
  const OperatorHomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
    appBar: AppBar(
      title: const Text('Station dashboard'),
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
          const Text('Station Operator app'),
          const SizedBox(height: 24),
          FilledButton.icon(
            key: const Key('operator-pickup-button'),
            onPressed: () => context.push(RoutePaths.operatorPickup),
            icon: const Icon(Icons.mark_email_read_outlined),
            label: const Text('Confirm Package Pickup'),
          ),
        ],
      ),
    ),
  );
}
