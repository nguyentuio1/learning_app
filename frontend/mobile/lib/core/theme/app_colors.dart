import 'package:flutter/material.dart';

import '../config/app_config.dart';

abstract final class AppColors {
  static const customerSeed = Color(0xFF1E56A0);
  static const operatorSeed = Color(0xFF3B5BDB);
  static const customerBackground = Color(0xFFF8FAFC);
  static const customerSurface = Color(0xFFFFFFFF);
  static const customerBorder = Color(0xFFE2E8F0);
  static const customerTextPrimary = Color(0xFF0F172A);
  static const customerTextSecondary = Color(0xFF64748B);
  static const customerPrimaryContainer = Color(0xFFEEF2FF);

  static Color seedFor(AppFlavor flavor) => switch (flavor) {
    AppFlavor.customer => customerSeed,
    AppFlavor.operator => operatorSeed,
  };
}
