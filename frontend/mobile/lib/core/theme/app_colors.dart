import 'package:flutter/material.dart';

import '../config/app_config.dart';

abstract final class AppColors {
  static const customerSeed = Color(0xFF12B76A); // green
  static const operatorSeed = Color(0xFF3B5BDB); // indigo

  static Color seedFor(AppFlavor flavor) => switch (flavor) {
        AppFlavor.customer => customerSeed,
        AppFlavor.operator => operatorSeed,
      };
}
