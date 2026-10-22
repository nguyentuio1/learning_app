import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'dart:async';

import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/package_model.dart';
import 'package:smart_drone_delivery/data/providers/package_providers.dart';
import 'package:smart_drone_delivery/data/services/package_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/packages/add_edit_package_page.dart';
import 'package:smart_drone_delivery/presentation/customer/packages/packages_list_page.dart';

class _FakePackageApiService extends PackageApiService {
  _FakePackageApiService({
    List<Package>? initialPackages,
    this.getPackagesError,
    this.pendingPackages,
    this.deletePackageError,
  }) : packages = initialPackages ?? [],
       super(Dio());

  final List<Package> packages;
  Object? getPackagesError;
  Completer<List<Package>>? pendingPackages;
  Object? deletePackageError;
  PackageRequest? lastRequest;
  String? lastMutation;

  @override
  Future<List<Package>> getPackages() {
    if (getPackagesError case final error?) {
      return Future.error(error);
    }
    if (pendingPackages case final pending?) return pending.future;
    return Future.value(List.unmodifiable(packages));
  }

  @override
  Future<Package> getPackage(String id) async =>
      packages.firstWhere((package) => package.id == id);

  @override
  Future<void> createPackage(PackageRequest package) async {
    lastMutation = 'create';
    lastRequest = package;
    packages.add(_package(id: 'new-package', request: package));
  }

  @override
  Future<void> updatePackage(String id, PackageRequest package) async {
    lastMutation = 'update:$id';
    lastRequest = package;
  }

  @override
  Future<void> deletePackage(String id) async {
    lastMutation = 'delete:$id';
    if (deletePackageError case final error?) throw error;
    packages.removeWhere((package) => package.id == id);
  }
}

Package _package({
  String id = 'package-1',
  String name = 'Parcel',
  bool fragile = false,
  PackageRequest? request,
}) => Package(
  id: id,
  packageName: request?.packageName ?? name,
  weightGram: request?.weightGram ?? 500,
  lengthCm: request?.lengthCm ?? 20,
  widthCm: request?.widthCm ?? 10,
  heightCm: request?.heightCm ?? 8,
  packageImageUrl: request?.packageImageUrl,
  isFragile: request?.isFragile ?? fragile,
);

Future<void> _pumpPage(
  WidgetTester tester,
  Widget page,
  _FakePackageApiService service, {
  bool settle = true,
}) async {
  tester.view.physicalSize = const Size(800, 1800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [packageApiServiceProvider.overrideWithValue(service)],
      child: MaterialApp(
        home: Scaffold(
          body: Navigator(
            onGenerateRoute: (_) =>
                MaterialPageRoute<void>(builder: (_) => page),
          ),
        ),
      ),
    ),
  );
  if (settle) {
    await tester.pumpAndSettle();
  } else {
    await tester.pump();
  }
}

void main() {
  testWidgets('C18 shows loading indicator while packages load', (
    tester,
  ) async {
    final pending = Completer<List<Package>>();
    final service = _FakePackageApiService(pendingPackages: pending);

    await _pumpPage(tester, const PackagesListPage(), service, settle: false);

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    pending.complete([_package()]);
    await tester.pumpAndSettle();
    expect(find.text('Parcel'), findsOneWidget);
  });

  testWidgets('C18 shows empty state when no packages exist', (tester) async {
    await _pumpPage(tester, const PackagesListPage(), _FakePackageApiService());

    expect(find.text('No packages'), findsOneWidget);
  });

  testWidgets('C18 retries after a package-list error', (tester) async {
    final service = _FakePackageApiService(
      getPackagesError: const ApiException(
        message: 'Temporary failure',
        statusCode: 503,
      ),
      initialPackages: [_package()],
    );

    await _pumpPage(tester, const PackagesListPage(), service);
    expect(find.text('Temporary failure'), findsOneWidget);
    service.getPackagesError = null;

    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();

    expect(find.text('Parcel'), findsOneWidget);
    expect(find.text('Temporary failure'), findsNothing);
  });

  testWidgets('C18 displays packages and fragile badge', (tester) async {
    final service = _FakePackageApiService(
      initialPackages: [
        _package(fragile: true),
        _package(id: 'package-2', name: 'Shoes'),
      ],
    );

    await _pumpPage(tester, const PackagesListPage(), service);

    expect(find.text('Parcel'), findsOneWidget);
    expect(find.text('Shoes'), findsOneWidget);
    expect(find.text('Dễ vỡ'), findsOneWidget);
    expect(find.text('Edit'), findsNWidgets(2));
    expect(find.text('Delete'), findsNWidgets(2));
  });

  testWidgets('C18 confirms deletion before making the request', (
    tester,
  ) async {
    final service = _FakePackageApiService(initialPackages: [_package()]);

    await _pumpPage(tester, const PackagesListPage(), service);
    await tester.tap(find.text('Delete'));
    await tester.pumpAndSettle();

    expect(find.text('Delete this package?'), findsOneWidget);
    expect(service.lastMutation, isNull);
    await tester.tap(find.widgetWithText(FilledButton, 'Delete'));
    await tester.pumpAndSettle();

    expect(service.lastMutation, 'delete:package-1');
    expect(find.text('No packages'), findsOneWidget);
  });

  testWidgets('C18 shows the MSG-P-CO-12 message on delete conflict', (
    tester,
  ) async {
    final service = _FakePackageApiService(
      initialPackages: [_package()],
      deletePackageError: const ApiException(
        message: 'Package is in use.',
        statusCode: 409,
      ),
    );

    await _pumpPage(tester, const PackagesListPage(), service);
    await tester.tap(find.text('Delete'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Delete'));
    await tester.pumpAndSettle();

    expect(
      find.text('This package is in use and cannot be deleted.'),
      findsOneWidget,
    );
    expect(find.text('Parcel'), findsNothing);
  });

  testWidgets('C19 validates name, required measurements, and capacity', (
    tester,
  ) async {
    final service = _FakePackageApiService();

    await _pumpPage(tester, const AddEditPackagePage(), service);
    await tester.ensureVisible(find.text('Save package'));
    await tester.tap(find.text('Save package'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 350));

    expect(find.text('Package name is required.'), findsOneWidget);
    expect(
      find.text('Weight and dimensions must be greater than zero.'),
      findsNWidgets(4),
    );
    expect(service.lastMutation, isNull);

    await tester.enterText(_field(0), 'Heavy parcel');
    await tester.enterText(_field(1), '0');
    await tester.enterText(_field(2), '0');
    await tester.enterText(_field(3), '0');
    await tester.enterText(_field(4), '0');
    await tester.tap(find.text('Save package'));
    await tester.pumpAndSettle();
    expect(
      find.text('Weight and dimensions must be greater than zero.'),
      findsNWidgets(4),
    );

    await tester.enterText(_field(1), '3000.01');
    await tester.enterText(_field(2), '30');
    await tester.enterText(_field(3), '20');
    await tester.enterText(_field(4), '15');
    await tester.tap(find.text('Save package'));
    await tester.pumpAndSettle();

    expect(
      find.text("This package exceeds the selected drone's capacity."),
      findsOneWidget,
    );
    expect(service.lastMutation, isNull);

    await tester.enterText(_field(1), '-1');
    await tester.enterText(_field(2), '30.01');
    await tester.enterText(_field(3), '20.01');
    await tester.enterText(_field(4), '15.01');
    await tester.tap(find.text('Save package'));
    await tester.pumpAndSettle();

    expect(
      find.text('Weight and dimensions must be greater than zero.'),
      findsOneWidget,
    );
    expect(
      find.text("This package exceeds the selected drone's capacity."),
      findsNWidgets(3),
    );
    expect(service.lastMutation, isNull);
  });

  testWidgets('C19 enforces package-name length', (tester) async {
    final service = _FakePackageApiService();

    await _pumpPage(tester, const AddEditPackagePage(), service);
    await tester.enterText(_field(0), 'P' * 201);

    expect(
      tester.widget<TextFormField>(_field(0)).controller!.text,
      hasLength(200),
    );
    expect(service.lastMutation, isNull);
  });

  testWidgets('C19 creates package with measurements and fragile flag', (
    tester,
  ) async {
    final service = _FakePackageApiService();

    await _pumpPage(tester, const AddEditPackagePage(), service);
    await tester.enterText(_field(0), 'Books');
    await tester.enterText(_field(1), '3000');
    await tester.enterText(_field(2), '30');
    await tester.enterText(_field(3), '20');
    await tester.enterText(_field(4), '15');
    await tester.enterText(_field(5), 'https://example.com/package.jpg');
    await tester.tap(find.text('Fragile'));
    await tester.ensureVisible(find.text('Save package'));
    await tester.tap(find.text('Save package'));
    await tester.pumpAndSettle();

    expect(service.lastMutation, 'create');
    expect(service.lastRequest?.packageName, 'Books');
    expect(service.lastRequest?.weightGram, 3000);
    expect(service.lastRequest?.lengthCm, 30);
    expect(service.lastRequest?.widthCm, 20);
    expect(service.lastRequest?.heightCm, 15);
    expect(
      service.lastRequest?.packageImageUrl,
      'https://example.com/package.jpg',
    );
    expect(service.lastRequest?.isFragile, isTrue);
    expect(find.text('Package saved successfully.'), findsOneWidget);
    expect(find.byType(AddEditPackagePage), findsNothing);
  });

  testWidgets('C19 loads existing package and submits update', (tester) async {
    final package = _package(fragile: true);
    final service = _FakePackageApiService(initialPackages: [package]);

    await _pumpPage(tester, AddEditPackagePage(package: package), service);

    expect(find.text('Edit package'), findsOneWidget);
    expect(find.text('Parcel'), findsOneWidget);
    await tester.ensureVisible(find.text('Save changes'));
    await tester.tap(find.text('Save changes'));
    await tester.pumpAndSettle();

    expect(service.lastMutation, 'update:package-1');
    expect(service.lastRequest?.packageName, 'Parcel');
  });

  testWidgets('C19 edit route fetches package details by id', (tester) async {
    final package = _package();
    final service = _FakePackageApiService(initialPackages: [package]);

    await _pumpPage(
      tester,
      const EditPackageRoute(packageId: 'package-1'),
      service,
    );

    expect(find.text('Edit package'), findsOneWidget);
    expect(find.text('Parcel'), findsOneWidget);
  });
}

Finder _field(int index) => find.byType(TextFormField).at(index);
