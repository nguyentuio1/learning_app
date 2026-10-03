import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/session/auth_controller.dart';

class OperatorHomePage extends ConsumerWidget {
  const OperatorHomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
        appBar: AppBar(
          title: const Text('Station dashboard'),
          actions: [
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
