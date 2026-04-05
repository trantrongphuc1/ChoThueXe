-- Seed images for vehicles that were created before image upload support.
-- Run this script after placing files in wwwroot/imageVehicle.

-- 1) Optional preview: see which vehicles can be matched by name.
WITH image_map(keyword, image_url, priority) AS (
    SELECT 'ford ranger', '/imageVehicle/ford ranger.jpg', 1 FROM dual UNION ALL
    SELECT 'camry', '/imageVehicle/camry.jpg', 2 FROM dual UNION ALL
    SELECT 'kia seltos', '/imageVehicle/kia seltos.jpg', 3 FROM dual UNION ALL
    SELECT 'seltos', '/imageVehicle/kia seltos.jpg', 4 FROM dual UNION ALL
    SELECT 'mitsubishi xpander', '/imageVehicle/mitsubishi xpander.jpg', 5 FROM dual UNION ALL
    SELECT 'xpander', '/imageVehicle/mitsubishi xpander.jpg', 6 FROM dual UNION ALL
    SELECT 'mazda cx5', '/imageVehicle/mazda cx5.jpg', 7 FROM dual UNION ALL
    SELECT 'cx5', '/imageVehicle/mazda cx5.jpg', 8 FROM dual UNION ALL
    SELECT 'hyundai accent', '/imageVehicle/hyundai accent.jpg', 9 FROM dual UNION ALL
    SELECT 'accent', '/imageVehicle/accent 2.jpg', 10 FROM dual UNION ALL
    SELECT 'vios', '/imageVehicle/vios.jpg', 11 FROM dual UNION ALL
    SELECT 'vinfast lux', '/imageVehicle/vinfast lux.jpg', 12 FROM dual UNION ALL
    SELECT 'lux', '/imageVehicle/vinfast lux.jpg', 13 FROM dual UNION ALL
    SELECT 'crv', '/imageVehicle/crv.jpg', 14 FROM dual UNION ALL
    SELECT 'mazda cx-5', '/imageVehicle/mazda cx5.jpg', 15 FROM dual
), matched AS (
    SELECT
        v.vehicle_id,
        v.vehicle_name,
        m.image_url,
        ROW_NUMBER() OVER (PARTITION BY v.vehicle_id ORDER BY m.priority) AS rn
    FROM vehicles v
    JOIN image_map m ON INSTR(LOWER(v.vehicle_name), m.keyword) > 0
)
SELECT vehicle_id, vehicle_name, image_url
FROM matched
WHERE rn = 1
ORDER BY vehicle_id;


-- 2) Insert a primary image for matched vehicles that still have no image row.
INSERT INTO vehicle_images (image_id, vehicle_id, image_url)
WITH image_map(keyword, image_url, priority) AS (
    SELECT 'ford ranger', '/imageVehicle/ford ranger.jpg', 1 FROM dual UNION ALL
    SELECT 'camry', '/imageVehicle/camry.jpg', 2 FROM dual UNION ALL
    SELECT 'kia seltos', '/imageVehicle/kia seltos.jpg', 3 FROM dual UNION ALL
    SELECT 'seltos', '/imageVehicle/kia seltos.jpg', 4 FROM dual UNION ALL
    SELECT 'mitsubishi xpander', '/imageVehicle/mitsubishi xpander.jpg', 5 FROM dual UNION ALL
    SELECT 'xpander', '/imageVehicle/mitsubishi xpander.jpg', 6 FROM dual UNION ALL
    SELECT 'mazda cx5', '/imageVehicle/mazda cx5.jpg', 7 FROM dual UNION ALL
    SELECT 'cx5', '/imageVehicle/mazda cx5.jpg', 8 FROM dual UNION ALL
    SELECT 'hyundai accent', '/imageVehicle/hyundai accent.jpg', 9 FROM dual UNION ALL
    SELECT 'accent', '/imageVehicle/accent 2.jpg', 10 FROM dual UNION ALL
    SELECT 'vios', '/imageVehicle/vios.jpg', 11 FROM dual UNION ALL
    SELECT 'vinfast lux', '/imageVehicle/vinfast lux.jpg', 12 FROM dual UNION ALL
    SELECT 'lux', '/imageVehicle/vinfast lux.jpg', 13 FROM dual UNION ALL
    SELECT 'crv', '/imageVehicle/crv.jpg', 14 FROM dual UNION ALL
    SELECT 'mazda cx-5', '/imageVehicle/mazda cx5.jpg', 15 FROM dual
), matched AS (
    SELECT
        v.vehicle_id,
        m.image_url,
        ROW_NUMBER() OVER (PARTITION BY v.vehicle_id ORDER BY m.priority) AS rn
    FROM vehicles v
    JOIN image_map m ON INSTR(LOWER(v.vehicle_name), m.keyword) > 0
), chosen AS (
    SELECT vehicle_id, image_url
    FROM matched
    WHERE rn = 1
), to_insert AS (
    SELECT c.vehicle_id, c.image_url
    FROM chosen c
    WHERE NOT EXISTS (
        SELECT 1
        FROM vehicle_images vi
        WHERE vi.vehicle_id = c.vehicle_id
    )
)
SELECT
    (SELECT NVL(MAX(image_id), 0) FROM vehicle_images) + ROW_NUMBER() OVER (ORDER BY vehicle_id),
    vehicle_id,
    image_url
FROM to_insert;

COMMIT;


-- 3) Optional verify after insert.
SELECT v.vehicle_id, v.vehicle_name, vi.image_url
FROM vehicles v
LEFT JOIN (
    SELECT vehicle_id, MIN(image_url) AS image_url
    FROM vehicle_images
    GROUP BY vehicle_id
) vi ON vi.vehicle_id = v.vehicle_id
ORDER BY v.vehicle_id;
