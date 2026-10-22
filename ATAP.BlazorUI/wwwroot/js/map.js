window.atapMap = (function () {
    let map = null;
    const markers = [];
    const airportMarkers = [];

    return {
        init: function (containerId) {
            if (map) {
                map.remove();
                map = null;
            }
            markers.length = 0;

            map = L.map(containerId).setView([50.0, 15.0], 4);

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                maxZoom: 18
            }).addTo(map);
        },

        updateAirports: function (airports) {
            airportMarkers.forEach(function (m) { map.removeLayer(m); });
            airportMarkers.length = 0;

            if (!map || !airports) return;

            airports.forEach(function (a) {
                if (a.latitude == null || a.longitude == null) return;

                const marker = L.circleMarker([a.latitude, a.longitude], {
                    radius: 6,
                    color: '#1a1a2e',
                    fillColor: '#4361ee',
                    fillOpacity: 0.9,
                    weight: 2
                })
                    .bindPopup(
                        '<b>' + (a.icaoCode || '') + '</b> — ' + (a.name || '') +
                        '<br>Координаты: ' + a.latitude.toFixed(4) + ', ' + a.longitude.toFixed(4)
                    )
                    .addTo(map);

                airportMarkers.push(marker);
            });
        },

        updateFlights: function (flights) {
            markers.forEach(function (m) { map.removeLayer(m); });
            markers.length = 0;

            if (!map || !flights) return;

            const planeIcon = L.divIcon({
                html: '✈',
                className: 'plane-icon',
                iconSize: [20, 20],
                iconAnchor: [10, 10],
                popupAnchor: [0, -10]
            });

            flights.forEach(function (f) {
                if (f.latitude == null || f.longitude == null) return;
                if (f.latitude === 0 && f.longitude === 0) return;

                const callsign = f.callsign || 'N/A';
                const altitude = f.altitude != null ? f.altitude + ' м' : 'нет данных';

                const marker = L.marker([f.latitude, f.longitude], { icon: planeIcon })
                    .bindPopup(
                        '<b>' + callsign + '</b>' +
                        '<br>Высота: ' + altitude +
                        '<br>Координаты: ' + f.latitude.toFixed(4) + ', ' + f.longitude.toFixed(4)
                    )
                    .addTo(map);

                markers.push(marker);
            });
        }
    };
})();
