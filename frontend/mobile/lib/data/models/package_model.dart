class PackageModel {
  const PackageModel({
    required this.id,
    required this.packageName,
    required this.weightGram,
    required this.lengthCm,
    required this.widthCm,
    required this.heightCm,
    required this.isFragile,
    this.packageImageUrl,
    this.createdAt,
  });

  final String id;
  final String packageName;
  final double weightGram;
  final double lengthCm;
  final double widthCm;
  final double heightCm;
  final String? packageImageUrl;
  final bool isFragile;
  final String? createdAt;

  factory PackageModel.fromJson(Map<String, dynamic> json) => PackageModel(
    id: json['id'] as String,
    packageName: json['packageName'] as String,
    weightGram: (json['weightGram'] as num).toDouble(),
    lengthCm: (json['lengthCm'] as num).toDouble(),
    widthCm: (json['widthCm'] as num).toDouble(),
    heightCm: (json['heightCm'] as num).toDouble(),
    packageImageUrl: json['packageImageUrl'] as String?,
    isFragile: json['isFragile'] as bool? ?? false,
    createdAt: json['createdAt'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'packageName': packageName,
    'weightGram': weightGram,
    'lengthCm': lengthCm,
    'widthCm': widthCm,
    'heightCm': heightCm,
    'packageImageUrl': packageImageUrl,
    'isFragile': isFragile,
  };
}

class PackageRequest {
  const PackageRequest({
    required this.packageName,
    required this.weightGram,
    required this.lengthCm,
    required this.widthCm,
    required this.heightCm,
    required this.isFragile,
    this.packageImageUrl,
  });

  final String packageName;
  final double weightGram;
  final double lengthCm;
  final double widthCm;
  final double heightCm;
  final String? packageImageUrl;
  final bool isFragile;

  Map<String, dynamic> toJson() => {
    'packageName': packageName,
    'weightGram': weightGram,
    'lengthCm': lengthCm,
    'widthCm': widthCm,
    'heightCm': heightCm,
    'packageImageUrl': packageImageUrl,
    'isFragile': isFragile,
  };
}
