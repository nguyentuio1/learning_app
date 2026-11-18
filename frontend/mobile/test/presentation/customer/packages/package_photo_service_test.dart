import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/services/package_api_service.dart';

void main() {
  test(
    'photo upload posts bytes only to the owned package image route',
    () async {
      RequestOptions? sent;
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (request, handler) {
            sent = request;
            handler.resolve(
              Response(
                requestOptions: request,
                statusCode: 200,
                data: {'imageUrl': 'http://storage.example/photos/parcel.png'},
              ),
            );
          },
        ),
      );
      final result = await PackageApiService(dio).uploadImage(
        'parcel-1',
        bytes: [137, 80, 78, 71],
        filename: 'parcel.png',
      );
      expect(result, 'http://storage.example/photos/parcel.png');
      expect(sent!.path, '/packages/parcel-1/image');
      expect(sent!.method, 'POST');
      final form = sent!.data as FormData;
      expect(form.files.single.key, 'file');
      expect(form.files.single.value.filename, 'parcel.png');
      expect(form.files.single.value.length, 4);
    },
  );

  test(
    'empty and oversized photos are rejected before network traffic',
    () async {
      var calls = 0;
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (request, handler) {
            calls++;
            handler.resolve(Response(requestOptions: request, data: {}));
          },
        ),
      );
      final service = PackageApiService(dio);
      for (final bytes in [<int>[], List<int>.filled(5 * 1024 * 1024 + 1, 0)]) {
        await expectLater(
          service.uploadImage('p', bytes: bytes, filename: 'p.png'),
          throwsA(isA<ApiException>()),
        );
      }
      expect(calls, 0);
    },
  );

  test(
    'storage failure remains an error rather than a successful URL',
    () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (request, handler) {
            handler.reject(
              DioException(
                requestOptions: request,
                type: DioExceptionType.connectionError,
                error: 'storage unavailable',
              ),
            );
          },
        ),
      );
      await expectLater(
        PackageApiService(dio)
            .uploadImage('p', bytes: [137, 80, 78, 71], filename: 'p.png'),
        throwsA(isA<ApiException>()),
      );
    },
  );
}
