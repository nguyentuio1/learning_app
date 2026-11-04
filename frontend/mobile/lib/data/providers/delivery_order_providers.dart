import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../models/customer_address.dart';
import '../models/delivery_order.dart';
import '../models/landing_station.dart';
import '../models/package_model.dart';
import '../services/address_api_service.dart';
import '../services/delivery_order_api_service.dart';
import '../services/landing_station_api_service.dart';
import '../services/package_api_service.dart';

final deliveryOrderApiServiceProvider = Provider<DeliveryOrderApiService>(
  (ref) => DeliveryOrderApiService(ref.watch(dioProvider)),
  name: 'deliveryOrderApiService',
);

final myOrdersProvider = FutureProvider.autoDispose<List<DeliveryOrder>>(
  (ref) => ref.watch(deliveryOrderApiServiceProvider).getMyOrders(),
  name: 'myOrders',
);

final orderPackagesProvider = FutureProvider.autoDispose<List<Package>>(
  (ref) => PackageApiService(ref.watch(dioProvider)).getPackages(),
  name: 'orderPackages',
);

final orderStationsProvider = FutureProvider.autoDispose<List<LandingStation>>(
  (ref) =>
      LandingStationApiService(ref.watch(dioProvider)).getOperationalStations(),
  name: 'orderStations',
);

final orderAddressesProvider =
    FutureProvider.autoDispose<List<CustomerAddress>>(
      (ref) => AddressApiService(ref.watch(dioProvider)).getAddresses(),
      name: 'orderAddresses',
    );
