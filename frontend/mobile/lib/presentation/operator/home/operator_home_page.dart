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
    body: const Center(child: Text('Station Operator app')),
  );
}
