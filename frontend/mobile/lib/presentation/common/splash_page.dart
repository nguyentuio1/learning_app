import 'package:flutter/material.dart';

/// Hiển thị trong lúc app khôi phục phiên (AuthStatus.unknown).
class SplashPage extends StatelessWidget {
  const SplashPage({super.key});

  @override
  Widget build(BuildContext context) =>
      const Scaffold(body: Center(child: CircularProgressIndicator()));
}
