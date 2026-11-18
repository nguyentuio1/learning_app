import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';

import 'dart:typed_data';

import 'dart:async';

import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/package_model.dart';
import 'package:smart_drone_delivery/data/providers/package_providers.dart';
import 'package:smart_drone_delivery/data/services/package_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/packages/add_edit_package_page.dart';
import 'package:smart_drone_delivery/presentation/customer/packages/packages_list_page.dart';

class _FakePackageApiService extends PackageApiService {
  int photoCreates = 0;
  int photoUploads = 0;
  bool photoFails = true;

  @override
  Future<PackageModel> createPackageRecord(PackageRequest request) async {
    photoCreates++;
    return _package(id: 'photo-created', request: request);
  }

  @override
  Future<String> uploadImage(
    String id, {
    required List<int> bytes,
    required String filename,
  }) async {
    photoUploads++;
    if (photoFails) throw const ApiException(message: 'Storage unavailable');
    return 'https://example.com/uploaded.png';
  }

  _FakePackageApiService({
    List<PackageModel>? initialPackages,
    this.getPackagesError,
    this.getPackageError,
    this.pendingPackages,
    this.deletePackageError,
    this.createPackageError,
  }) : packages = initialPackages ?? [],
       super(Dio());

  final List<PackageModel> packages;
  Object? getPackagesError;
  Object? getPackageError;
  Completer<List<PackageModel>>? pendingPackages;
  Object? deletePackageError;
  Object? createPackageError;
  PackageRequest? lastRequest;
  String? lastMutation;

  @override
  Future<List<PackageModel>> getPackages({bool? available}) {
    if (getPackagesError case final error?) {
      return Future.error(error);
    }
    if (pendingPackages case final pending?) return pending.future;
    return Future.value(List.unmodifiable(packages));
  }

  @override
  Future<PackageModel> getPackage(String id) async {
    if (getPackageError case final error?) throw error;
    return packages.firstWhere((package) => package.id == id);
  }

  @override
  Future<void> createPackage(PackageRequest package) async {
    lastMutation = 'create';
    lastRequest = package;
    if (createPackageError case final error?) throw error;
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

PackageModel _package({
  String id = 'package-1',
  String name = 'Parcel',
  bool fragile = false,
  PackageRequest? request,
}) => PackageModel(
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
      overrides: [
        packageApiServiceProvider.overrideWithValue(service),
        packageImagePickerProvider.overrideWithValue(_TestPhotoPicker()),
      ],
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
  testWidgets('same-URL replacement rebuilds the displayed image', (tester) async {
    final package = _package(request: const PackageRequest(
      packageName: 'Parcel', weightGram: 500, lengthCm: 20, widthCm: 10,
      heightCm: 8, isFragile: false, packageImageUrl: 'https://example.com/uploaded.png'));
    final service = _FakePackageApiService(initialPackages: [package])..photoFails = false;
    await _pumpPage(tester, AddEditPackagePage(package: package), service);
    expect(find.byKey(const ValueKey('package-photo-preview-0')), findsOneWidget);
    await tester.tap(find.byKey(const Key('package-upload-photo')));
    await tester.pumpAndSettle();
    expect(service.photoUploads, 1);
    expect(find.byKey(const ValueKey('package-photo-preview-1')), findsOneWidget);
    expect(find.byKey(const ValueKey('package-photo-preview-0')), findsNothing);
  });

  testWidgets('retrying a failed upload reuses the saved package', (
    tester,
  ) async {
    final service = _FakePackageApiService();
    await _pumpPage(tester, const AddEditPackagePage(), service);
    for (final entry in ['Parcel', '500', '20', '10', '8'].asMap().entries) {
      await tester.enterText(_field(entry.key), entry.value);
    }
    await tester.tap(find.byKey(const Key('package-upload-photo')));
    await tester.pumpAndSettle();
    expect(service.photoCreates, 1);
    expect(service.photoUploads, 1);
    expect(
      find.textContaining('Package saved; photo upload failed'),
      findsOneWidget,
    );
    await tester.tap(find.byKey(const Key('package-upload-photo')));
    await tester.pumpAndSettle();
    expect(service.photoCreates, 1);
    expect(service.photoUploads, 2);
    expect(find.byType(AddEditPackagePage), findsOneWidget);
  });
  testWidgets('offers a real package photo upload action', (tester) async {
    await _pumpPage(
      tester,
      const AddEditPackagePage(),
      _FakePackageApiService(),
    );
    expect(find.byKey(const Key('package-upload-photo')), findsOneWidget);
  });

  testWidgets('C18 shows loading indicator while packages load', (
    tester,
  ) async {
    final pending = Completer<List<PackageModel>>();
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
    expect(
      find.descendant(of: find.byType(Card), matching: find.text('Fragile')),
      findsOneWidget,
    );
    expect(find.byTooltip('Edit'), findsNWidgets(2));
    expect(find.byTooltip('Delete'), findsNWidgets(2));
    expect(find.text('Description (optional)'), findsNothing);
  });

  testWidgets('C18 confirms deletion before making the request', (
    tester,
  ) async {
    final service = _FakePackageApiService(initialPackages: [_package()]);

    await _pumpPage(tester, const PackagesListPage(), service);
    await tester.tap(find.byTooltip('Delete'));
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
    await tester.tap(find.byTooltip('Delete'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Delete'));
    await tester.pumpAndSettle();

    expect(
      find.text('This package is in use and cannot be deleted.'),
      findsOneWidget,
    );
    expect(find.text('Parcel'), findsNothing);
  });

  testWidgets('C18 displays owner-isolation 404 from package details', (
    tester,
  ) async {
    final service = _FakePackageApiService(
      getPackageError: const ApiException(
        message: 'Package not found.',
        statusCode: 404,
      ),
    );

    await _pumpPage(
      tester,
      const EditPackageRoute(packageId: 'not-owned'),
      service,
    );

    expect(find.text('Package not found.'), findsOneWidget);
    expect(find.text('Edit package'), findsOneWidget);
  });

  testWidgets('C19 validates name, required measurements, and capacity', (
    tester,
  ) async {
    final service = _FakePackageApiService();

    await _pumpPage(tester, const AddEditPackagePage(), service);
    await tester.ensureVisible(find.text('Save Package'));
    await tester.tap(find.text('Save Package'));
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
    await tester.tap(find.text('Save Package'));
    await tester.pumpAndSettle();
    expect(
      find.text('Weight and dimensions must be greater than zero.'),
      findsNWidgets(4),
    );

    await tester.enterText(_field(1), '3000.01');
    await tester.enterText(_field(2), '30');
    await tester.enterText(_field(3), '20');
    await tester.enterText(_field(4), '15');
    await tester.tap(find.text('Save Package'));
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
    await tester.tap(find.text('Save Package'));
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

  testWidgets('C19 rejects malformed image URL before calling the API', (
    tester,
  ) async {
    final service = _FakePackageApiService();

    await _pumpPage(tester, const AddEditPackagePage(), service);
    await tester.enterText(_field(0), 'Parcel');
    await tester.enterText(_field(1), '500');
    await tester.enterText(_field(2), '20');
    await tester.enterText(_field(3), '10');
    await tester.enterText(_field(4), '8');
    await tester.tap(find.text('Add package photo (optional)'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField).last, 'not a URL');
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();
    expect(find.text('Enter a valid public image URL.'), findsOneWidget);
    expect(service.lastMutation, isNull);
  });

  testWidgets('C19 displays the backend 400 Bad Request message', (
    tester,
  ) async {
    final service = _FakePackageApiService(
      createPackageError: const ApiException(
        message: 'Bad Request: Weight must be greater than 0.',
        statusCode: 400,
      ),
    );

    await _pumpPage(tester, const AddEditPackagePage(), service);
    await tester.enterText(_field(0), 'Parcel');
    await tester.enterText(_field(1), '500');
    await tester.enterText(_field(2), '20');
    await tester.enterText(_field(3), '10');
    await tester.enterText(_field(4), '8');
    await tester.ensureVisible(find.text('Save Package'));
    await tester.tap(find.text('Save Package'));
    await tester.pumpAndSettle();

    expect(
      find.text('Bad Request: Weight must be greater than 0.'),
      findsOneWidget,
    );
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
    await tester.tap(find.text('Add package photo (optional)'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byType(TextFormField).last,
      'https://example.com/package.jpg',
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Fragile package'));
    await tester.ensureVisible(find.text('Save Package'));
    await tester.tap(find.text('Save Package'));
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

    expect(find.text('Edit Package'), findsOneWidget);
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

    expect(find.text('Edit Package'), findsOneWidget);
    expect(find.text('Parcel'), findsOneWidget);
  });
}

Finder _field(int index) => find.byType(TextFormField).at(index);

class _TestPhotoPicker extends ImagePicker {
  @override
  Future<XFile?> pickImage({
    required ImageSource source,
    double? maxWidth,
    double? maxHeight,
    int? imageQuality,
    CameraDevice preferredCameraDevice = CameraDevice.rear,
    bool requestFullMetadata = true,
  }) async => XFile.fromData(
    Uint8List.fromList([137, 80, 78, 71]),
    name: 'parcel.png',
    mimeType: 'image/png',
  );
}
