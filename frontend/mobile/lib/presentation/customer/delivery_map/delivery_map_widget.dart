import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/theme/app_colors.dart';
import '../../../domain/entities/delivery_map_snapshot.dart';

class DeliveryMapWidget extends StatefulWidget {
  const DeliveryMapWidget({
    required this.snapshot,
    this.liveUnavailable = false,
    super.key,
  });

  final DeliveryMapSnapshot snapshot;
  final bool liveUnavailable;

  @override
  State<DeliveryMapWidget> createState() => _DeliveryMapWidgetState();
}

class _DeliveryMapWidgetState extends State<DeliveryMapWidget> {
  final MapController _mapController = MapController();

  List<LatLng> get _routeCoordinates {
    final snapshot = widget.snapshot;
    final coordinates = <LatLng>[
      LatLng(snapshot.originStation.latitude, snapshot.originStation.longitude),
    ];
    final position = snapshot.lastPosition;
    if (snapshot.status == DeliveryMapOrderStatus.inTransit &&
        position != null) {
      coordinates.add(LatLng(position.latitude, position.longitude));
    }
    coordinates.add(
      LatLng(
        snapshot.destinationStation.latitude,
        snapshot.destinationStation.longitude,
      ),
    );
    return coordinates;
  }

  CameraFit get _routeFit => CameraFit.coordinates(
    coordinates: _routeCoordinates,
    padding: const EdgeInsets.all(48),
    maxZoom: 15,
  );

  @override
  Widget build(BuildContext context) {
    final snapshot = widget.snapshot;
    final position = snapshot.lastPosition;
    final hasDrone =
        snapshot.status == DeliveryMapOrderStatus.inTransit && position != null;
    final origin = LatLng(
      snapshot.originStation.latitude,
      snapshot.originStation.longitude,
    );
    final destination = LatLng(
      snapshot.destinationStation.latitude,
      snapshot.destinationStation.longitude,
    );
    final markers = <Marker>[
      Marker(
        key: const ValueKey('delivery-map-origin-marker'),
        point: origin,
        width: 130,
        height: 72,
        child: _StationMarker(
          icon: Icons.flight_takeoff,
          label: snapshot.originStation.name,
          color: AppColors.operatorSeed,
        ),
      ),
      Marker(
        key: const ValueKey('delivery-map-destination-marker'),
        point: destination,
        width: 130,
        height: 72,
        child: _StationMarker(
          icon: Icons.location_on,
          label: snapshot.destinationStation.name,
          color: Theme.of(context).colorScheme.error,
        ),
      ),
      if (hasDrone)
        Marker(
          key: const ValueKey('delivery-map-drone-marker'),
          point: LatLng(position.latitude, position.longitude),
          width: 48,
          height: 48,
          child: Icon(
            Icons.airplanemode_active,
            color: snapshot.telemetryStale
                ? Colors.orange
                : AppColors.operatorSeed,
            size: 36,
          ),
        ),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (widget.liveUnavailable)
          const _MapBanner(
            text: 'Live tracking is temporarily unavailable. Reconnecting…',
            color: Colors.orange,
          ),
        if (snapshot.telemetryStale)
          const _MapBanner(
            text: 'Live position stale (>60s)',
            color: Colors.red,
          ),
        SizedBox(
          height: 320,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: Stack(
              children: [
                FlutterMap(
                  mapController: _mapController,
                  options: MapOptions(
                    initialCenter: origin,
                    initialZoom: 12,
                    initialCameraFit: _routeFit,
                  ),
                  children: [
                    TileLayer(
                      urlTemplate:
                          'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                      userAgentPackageName:
                          'com.smartdrone.smart_drone_delivery',
                    ),
                    PolylineLayer(
                      polylines: [
                        Polyline(
                          points: _routeCoordinates,
                          color: AppColors.operatorSeed,
                          strokeWidth: 4,
                        ),
                      ],
                    ),
                    MarkerLayer(markers: markers),
                  ],
                ),
                Positioned(
                  right: 12,
                  bottom: 64,
                  child: FloatingActionButton.small(
                    key: const Key('map-focus-drone-button'),
                    heroTag: 'delivery-map-focus-drone',
                    tooltip: 'Focus drone',
                    onPressed: hasDrone
                        ? () => _mapController.move(
                            LatLng(position.latitude, position.longitude),
                            15,
                          )
                        : null,
                    child: const Icon(Icons.my_location),
                  ),
                ),
                Positioned(
                  right: 12,
                  bottom: 12,
                  child: FloatingActionButton.small(
                    key: const Key('map-fit-route-button'),
                    heroTag: 'delivery-map-fit-route',
                    tooltip: 'Fit route',
                    onPressed: () => _mapController.fitCamera(_routeFit),
                    child: const Icon(Icons.fit_screen),
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _StationMarker extends StatelessWidget {
  const _StationMarker({
    required this.icon,
    required this.label,
    required this.color,
  });

  final IconData icon;
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Column(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(icon, color: color, size: 30),
      Container(
        constraints: const BoxConstraints(maxWidth: 124),
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surface,
          borderRadius: BorderRadius.circular(6),
        ),
        child: Text(
          label,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.labelSmall,
        ),
      ),
    ],
  );
}

class _MapBanner extends StatelessWidget {
  const _MapBanner({required this.text, required this.color});

  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.only(bottom: 8),
    padding: const EdgeInsets.all(10),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.12),
      borderRadius: BorderRadius.circular(8),
    ),
    child: Text(text, style: TextStyle(color: color)),
  );
}
