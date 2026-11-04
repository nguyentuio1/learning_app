import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/package_model.dart';
import 'package:smart_drone_delivery/data/providers/package_providers.dart';
import 'package:smart_drone_delivery/data/services/package_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/packages/package_notifier.dart';

class _FakePackageApiService extends PackageApiService {
  _FakePackageApiService() : super(Dio());

  final List<PackageModel> packages = [_package()];
  final List<String> calls = [];
  Object? getPackagesError;
  Object? getPackageError;
  Object? deletePackageError;

  @override
  Future<List<PackageModel>> getPackages({bool? available}) async {
    calls.add('get');
    if (getPackagesError case final error?) throw error;
    return List.unmodifiable(packages);
  }

  @override
  Future<PackageModel> getPackage(String id) async {
    if (getPackageError case final error?) throw error;
    return packages.firstWhere((package) => package.id == id);
  }

  @override
  Future<void> createPackage(PackageRequest package) async {
    calls.add('create');
    packages.add(_package(id: 'created', request: package));
  }

  @override
  Future<void> updatePackage(String id, PackageRequest package) async {
    calls.add('update:$id');
  }

  @override
  Future<void> deletePackage(String id) async {
    calls.add('delete:$id');
    if (deletePackageError case final error?) throw error;
    packages.removeWhere((package) => package.id == id);
  }
}

PackageModel _package({String id = 'package-1', PackageRequest? request}) =>
    PackageModel(
      id: id,
      packageName: request?.packageName ?? 'Parcel',
      weightGram: request?.weightGram ?? 500,
      lengthCm: request?.lengthCm ?? 20,
      widthCm: request?.widthCm ?? 10,
      heightCm: request?.heightCm ?? 8,
      packageImageUrl: request?.packageImageUrl,
      isFragile: request?.isFragile ?? false,
    );

void main() {
  late _FakePackageApiService service;
  late ProviderContainer container;

  setUp(() {
    service = _FakePackageApiService();
    container = ProviderContainer(
      overrides: [packageApiServiceProvider.overrideWithValue(service)],
      retry: (_, _) => null,
    );
  });

  tearDown(() => container.dispose());

  test('maps API package fields without changing JSON property names', () {
    const createdAt = '2026-10-22T10:00:00+07:00';
    final package = PackageModel.fromJson({
      'id': 'package-1',
      'packageName': 'Parcel',
      'weightGram': 12,
      'lengthCm': 11.5,
      'widthCm': 8,
      'heightCm': 4.25,
      'packageImageUrl': 'https://example.com/parcel.jpg',
      'isFragile': true,
      'createdAt': createdAt,
    });

    expect(package.weightGram, 12.0);
    expect(package.lengthCm, 11.5);
    expect(package.widthCm, 8.0);
    expect(package.heightCm, 4.25);
    expect(package.createdAt, createdAt);
    expect(
      package.toJson().keys,
      allOf(isNot(contains('id')), isNot(contains('createdAt'))),
    );
    expect(
      package.toJson().keys,
      containsAll([
        'packageName',
        'weightGram',
        'lengthCm',
        'widthCm',
        'heightCm',
        'packageImageUrl',
        'isFragile',
      ]),
    );
  });

  test('loads packages into AsyncData', () async {
    final packages = await container.read(packageNotifierProvider.future);

    expect(packages, hasLength(1));
    expect(packages.single.packageName, 'Parcel');
    expect(service.calls, ['get']);
  });

  test('exposes errors from initial package request', () async {
    service.getPackagesError = const ApiException(
      message: 'failed to load',
      statusCode: 503,
    );

    await expectLater(
      container.read(packageNotifierProvider.future),
      throwsA(isA<ApiException>()),
    );
    expect(container.read(packageNotifierProvider).hasError, isTrue);
    expect(container.read(packageNotifierProvider).error, isA<ApiException>());
  });

  test('package detail preserves owner-isolation 404 errors', () async {
    service.getPackageError = const ApiException(
      message: 'Package not found.',
      statusCode: 404,
    );
    final provider = packageByIdProvider('not-owned');

    await expectLater(
      container.read(provider.future),
      throwsA(isA<ApiException>()),
    );

    final error = container.read(provider).error! as ApiException;
    expect(error.statusCode, 404);
    expect(error.message, 'Package not found.');
  });

  test('fetchPackages reloads packages into AsyncData', () async {
    await container.read(packageNotifierProvider.future);
    service.packages.add(
      _package(
        id: 'package-2',
        request: const PackageRequest(
          packageName: 'Books',
          weightGram: 200,
          lengthCm: 15,
          widthCm: 10,
          heightCm: 5,
          isFragile: false,
        ),
      ),
    );

    await container.read(packageNotifierProvider.notifier).fetchPackages();

    expect(container.read(packageNotifierProvider).requireValue, hasLength(2));
    expect(service.calls, ['get', 'get']);
  });

  test('fetchPackages captures ApiException in AsyncError', () async {
    await container.read(packageNotifierProvider.future);
    service.getPackagesError = const ApiException(
      message: 'Service unavailable',
      statusCode: 503,
    );

    await container.read(packageNotifierProvider.notifier).fetchPackages();

    final state = container.read(packageNotifierProvider);
    expect(state.hasError, isTrue);
    expect(state.error, isA<ApiException>());
  });

  test('creates a package then reloads the list', () async {
    await container.read(packageNotifierProvider.future);
    const request = PackageRequest(
      packageName: 'Books',
      weightGram: 300,
      lengthCm: 18,
      widthCm: 10,
      heightCm: 6,
      isFragile: true,
    );

    await container
        .read(packageNotifierProvider.notifier)
        .createPackage(request);

    expect(service.calls, ['get', 'create', 'get']);
    expect(
      container
          .read(packageNotifierProvider)
          .requireValue
          .map((package) => package.id),
      ['package-1', 'created'],
    );
  });

  test(
    'rejects invalid package requests as 400 without sending them',
    () async {
      await container.read(packageNotifierProvider.future);
      final notifier = container.read(packageNotifierProvider.notifier);
      final invalidRequests = [
        const PackageRequest(
          packageName: '  ',
          weightGram: 500,
          lengthCm: 20,
          widthCm: 10,
          heightCm: 8,
          isFragile: false,
        ),
        const PackageRequest(
          packageName: 'Parcel',
          weightGram: 3000.01,
          lengthCm: 20,
          widthCm: 10,
          heightCm: 8,
          isFragile: false,
        ),
        const PackageRequest(
          packageName: 'Parcel',
          weightGram: 500,
          lengthCm: 30.01,
          widthCm: 10,
          heightCm: 8,
          isFragile: false,
        ),
        const PackageRequest(
          packageName: 'Parcel',
          weightGram: 500,
          lengthCm: 20,
          widthCm: 20.01,
          heightCm: 8,
          isFragile: false,
        ),
        const PackageRequest(
          packageName: 'Parcel',
          weightGram: 500,
          lengthCm: 20,
          widthCm: 10,
          heightCm: 15.01,
          isFragile: false,
        ),
        const PackageRequest(
          packageName: 'Parcel',
          weightGram: 0,
          lengthCm: 20,
          widthCm: 10,
          heightCm: 8,
          isFragile: false,
        ),
        const PackageRequest(
          packageName: 'Parcel',
          weightGram: 500,
          lengthCm: 20,
          widthCm: 10,
          heightCm: 8,
          packageImageUrl: 'not a URL',
          isFragile: false,
        ),
      ];

      for (final request in invalidRequests) {
        await notifier.createPackage(request);
        final error = container.read(packageNotifierProvider).error;
        expect(error, isA<ApiException>());
        expect((error! as ApiException).statusCode, 400);
      }

      expect(service.calls, ['get']);
    },
  );

  test('updates and deletes packages before reloading', () async {
    await container.read(packageNotifierProvider.future);
    const request = PackageRequest(
      packageName: 'Updated parcel',
      weightGram: 400,
      lengthCm: 20,
      widthCm: 12,
      heightCm: 9,
      isFragile: false,
    );
    final notifier = container.read(packageNotifierProvider.notifier);

    await notifier.updatePackage('package-1', request);
    await notifier.deletePackage('package-1');

    expect(service.calls, [
      'get',
      'update:package-1',
      'get',
      'delete:package-1',
      'get',
    ]);
    expect(container.read(packageNotifierProvider).requireValue, isEmpty);
  });

  test('keeps mutation failures in AsyncError', () async {
    await container.read(packageNotifierProvider.future);
    service.getPackagesError = StateError('failed after mutation');

    await container
        .read(packageNotifierProvider.notifier)
        .deletePackage('package-1');

    expect(container.read(packageNotifierProvider).hasError, isTrue);
    expect(container.read(packageNotifierProvider).error, isA<StateError>());
  });

  test(
    'delete conflict is represented as AsyncError without throwing',
    () async {
      await container.read(packageNotifierProvider.future);
      service.deletePackageError = const ApiException(
        message: 'Conflict',
        statusCode: 409,
      );

      await expectLater(
        container
            .read(packageNotifierProvider.notifier)
            .deletePackage('package-1'),
        completes,
      );

      final state = container.read(packageNotifierProvider);
      expect(state.hasError, isTrue);
      expect(state.error, isA<ApiException>());
      expect((state.error! as ApiException).statusCode, 409);
      expect(service.calls, ['get', 'delete:package-1']);
    },
  );

  test('package service uses the API routes and available query', () async {
    final requests = <RequestOptions>[];
    final dio = Dio(BaseOptions(baseUrl: 'https://api.example.com/api'));
    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) {
          requests.add(options);
          final responseData = options.method == 'GET'
              ? options.path.endsWith('/package-1')
                    ? {
                        'id': 'package-1',
                        'packageName': 'Parcel',
                        'weightGram': 500,
                        'lengthCm': 20,
                        'widthCm': 10,
                        'heightCm': 8,
                        'isFragile': false,
                      }
                    : <dynamic>[]
              : <String, dynamic>{};
          handler.resolve(
            Response(requestOptions: options, data: responseData),
          );
        },
      ),
    );
    final api = PackageApiService(dio);
    const request = PackageRequest(
      packageName: 'Parcel',
      weightGram: 500,
      lengthCm: 20,
      widthCm: 10,
      heightCm: 8,
      isFragile: false,
    );

    await api.getPackages(available: true);
    await api.getPackage('package-1');
    await api.createPackage(request);
    await api.updatePackage('package-1', request);
    await api.deletePackage('package-1');

    expect(requests.map((options) => '${options.method} ${options.uri.path}'), [
      'GET /api/packages',
      'GET /api/packages/package-1',
      'POST /api/packages',
      'PUT /api/packages/package-1',
      'DELETE /api/packages/package-1',
    ]);
    expect(requests.first.queryParameters, {'available': true});
    expect(requests[2].data, request.toJson());
    expect(requests[3].data, request.toJson());
  });

  test('package service maps RFC 7807 errors to ApiException', () async {
    final dio = Dio(BaseOptions(baseUrl: 'https://api.example.com/api'));
    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) {
          handler.reject(
            DioException(
              requestOptions: options,
              type: DioExceptionType.badResponse,
              response: Response<dynamic>(
                requestOptions: options,
                statusCode: 404,
                data: {
                  'type': 'https://example.com/problems/not-found',
                  'title': 'Not Found',
                  'status': 404,
                  'detail': 'Package not found.',
                },
              ),
            ),
          );
        },
      ),
    );

    await expectLater(
      PackageApiService(dio).getPackage('not-owned'),
      throwsA(
        isA<ApiException>()
            .having((error) => error.statusCode, 'statusCode', 404)
            .having((error) => error.message, 'message', 'Package not found.')
            .having(
              (error) => error.code,
              'code',
              'https://example.com/problems/not-found',
            ),
      ),
    );
  });
}
