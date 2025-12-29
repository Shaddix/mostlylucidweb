-- Create temp table with image URLs per category
CREATE TEMP TABLE category_images (
    category TEXT,
    image_url TEXT,
    row_num INT
);

-- Books
INSERT INTO category_images VALUES
('books', '/api/images/products/books/booklight_premium_e-readers_plus/main.png', 1),
('books', '/api/images/products/books/pageprop_adjustable_bookstands/main.png', 2),
('books', '/api/images/products/books/pageprop_cookbook_bookstands/main.png', 3),
('books', '/api/images/products/books/pageturn_premium_e-readers/main.png', 4),
('books', '/api/images/products/books/readmark_leather_bookmarks/main.png', 5),
('books', '/api/images/products/books/readmark_page-clip_bookmarks/main.png', 6);

-- Fashion
INSERT INTO category_images VALUES
('fashion', '/api/images/products/fashion/bootcraft_chelsea_boots_series/main.png', 1),
('fashion', '/api/images/products/fashion/bootcraft_combat_boots/main.png', 2),
('fashion', '/api/images/products/fashion/carryall_trifold_wallets_series/main.png', 3),
('fashion', '/api/images/products/fashion/carrypro_backpack_bags/main.png', 4),
('fashion', '/api/images/products/fashion/classicbelt_braided_belts_ultra/main.png', 5),
('fashion', '/api/images/products/fashion/classicbelt_dress_belts_ultra/main.png', 6),
('fashion', '/api/images/products/fashion/classicwatch_chronograph_watches/main.png', 7),
('fashion', '/api/images/products/fashion/classicwatch_dive_watches_elite/main.png', 8),
('fashion', '/api/images/products/fashion/horologist_dive_watches/main.png', 9),
('fashion', '/api/images/products/fashion/horologist_minimalist_watches_x/main.png', 10),
('fashion', '/api/images/products/fashion/lenscraft_aviator_sunglasses/main.png', 11),
('fashion', '/api/images/products/fashion/lenscraft_wayfarer_sunglasses_pro/main.png', 12),
('fashion', '/api/images/products/fashion/pocketpro_trifold_wallets_x/main.png', 13),
('fashion', '/api/images/products/fashion/precisiontime_minimalist_watches/main.png', 14),
('fashion', '/api/images/products/fashion/runflex_retro_sneakers/main.png', 15),
('fashion', '/api/images/products/fashion/runflex_slip-on_sneakers_pro/main.png', 16),
('fashion', '/api/images/products/fashion/slimfold_bifold_wallets/main.png', 17),
('fashion', '/api/images/products/fashion/streetwear_running_sneakers_elite/main.png', 18),
('fashion', '/api/images/products/fashion/stridepro_slip-on_sneakers/main.png', 19),
('fashion', '/api/images/products/fashion/stridepro_slip-on_sneakers_x/main.png', 20),
('fashion', '/api/images/products/fashion/sunstyle_wayfarer_sunglasses_elite/main.png', 21),
('fashion', '/api/images/products/fashion/trailmaster_chelsea_boots/main.png', 22),
('fashion', '/api/images/products/fashion/trailmaster_hiking_boots/main.png', 23),
('fashion', '/api/images/products/fashion/travelmate_messenger_bags_elite/main.png', 24),
('fashion', '/api/images/products/fashion/urbanboot_dress_boots/main.png', 25),
('fashion', '/api/images/products/fashion/urbanstep_slip-on_sneakers_pro/main.png', 26),
('fashion', '/api/images/products/fashion/visionwear_cat-eye_sunglasses/main.png', 27),
('fashion', '/api/images/products/fashion/walletworks_trifold_wallets_pro/main.png', 28);

-- Food
INSERT INTO category_images VALUES
('food', '/api/images/products/food/beancrush_blade_coffee-grinders_plus/main.png', 1),
('food', '/api/images/products/food/freshkeep_plastic_set_containers_plus/main.png', 2),
('food', '/api/images/products/food/grindmaster_smart_coffee-grinders/main.png', 3),
('food', '/api/images/products/food/grindmaster_travel_coffee-grinders_x/main.png', 4),
('food', '/api/images/products/food/leafbrew_bottle_tea-infusers_series/main.png', 5),
('food', '/api/images/products/food/mealbox_snack_containers_plus/main.png', 6),
('food', '/api/images/products/food/precisiongrind_burr_coffee-grinders_x/main.png', 7),
('food', '/api/images/products/food/rose_gold_infuser_quest/main.png', 8),
('food', '/api/images/products/food/sealfresh_commercial_vacuum-sealers/main.png', 9),
('food', '/api/images/products/food/taste_quest_co._clearstack_glass_set/main.png', 10),
('food', '/api/images/products/food/teatime_teapot_tea-infusers/main.png', 11),
('food', '/api/images/products/food/vacuumpro_handheld_vacuum-sealers_x/main.png', 12);

-- Home
INSERT INTO category_images VALUES
('home', '/api/images/products/home/boilfast_gooseneck_kettles/main.png', 1),
('home', '/api/images/products/home/brewtemp_electric_kettles/main.png', 2),
('home', '/api/images/products/home/brighttask_smart_desk-lamps/main.png', 3),
('home', '/api/images/products/home/ergoseat_ergonomic_chairs_ultra/main.png', 4),
('home', '/api/images/products/home/floorlux_smart_floor-lamps_ultra/main.png', 5),
('home', '/api/images/products/home/lightworks_led_desk-lamps/main.png', 6),
('home', '/api/images/products/home/officelux_ergonomic_chairs/main.png', 7),
('home', '/api/images/products/home/sitright_kneeling_chairs/main.png', 8),
('home', '/api/images/products/home/smoothiemaker_personal_blenders/main.png', 9),
('home', '/api/images/products/home/teamaster_travel_kettles_plus/main.png', 10),
('home', '/api/images/products/home/tidydesk_laptop_stand_desk-accessories/main.png', 11);

-- Sport
INSERT INTO category_images VALUES
('sport', '/api/images/products/sport/deeptissue_textured_foam-rollers/main.png', 1),
('sport', '/api/images/products/sport/homefit_adjustable_dumbbells_x/main.png', 2),
('sport', '/api/images/products/sport/liftpro_smart_dumbbells_plus/main.png', 3),
('sport', '/api/images/products/sport/purehydrate_smart_water-bottles/main.png', 4),
('sport', '/api/images/products/sport/shakepro_multi-compartment_shaker-bottles_series/main.png', 5),
('sport', '/api/images/products/sport/strengthloop_fabric_resistance-bands/main.png', 6),
('sport', '/api/images/products/sport/strengthloop_therapy_resistance-bands/main.png', 7),
('sport', '/api/images/products/sport/zenmat_cork_yoga-mats/main.png', 8);

-- Tech
INSERT INTO category_images VALUES
('tech', '/api/images/products/tech/audiocapture_shotgun_microphones/main.png', 1),
('tech', '/api/images/products/tech/clearsound_over_ear_headphones_max/main.png', 2),
('tech', '/api/images/products/tech/clearview_1080p_webcams_pro/main.png', 3),
('tech', '/api/images/products/tech/clearview_streaming_webcams/main.png', 4),
('tech', '/api/images/products/tech/echobeats_bone-conduction_headphones/main.png', 5),
('tech', '/api/images/products/tech/elitekidx_blacksmart/main.png', 6),
('tech', '/api/images/products/tech/g-force_nvme_enclosure/main.png', 7),
('tech', '/api/images/products/tech/glidepro_travel_mice_max/main.png', 8),
('tech', '/api/images/products/tech/juicepro_wall_charger_chargers/main.png', 9),
('tech', '/api/images/products/tech/juicepro_wireless_pad_chargers_x/main.png', 10),
('tech', '/api/images/products/tech/lumismart_outdoor_smart_bulbs_series/main.png', 11),
('tech', '/api/images/products/tech/lumismart_strip_smart_bulbs/main.png', 12),
('tech', '/api/images/products/tech/mountmaster_tripod_phone_stands/main.png', 13),
('tech', '/api/images/products/tech/nestview_8-inch_smart-displays_series/main.png', 14),
('tech', '/api/images/products/tech/podcast_pro_dynamic_microphones/main.png', 15),
('tech', '/api/images/products/tech/roomfill_bookshelf_speakers_series/main.png', 16),
('tech', '/api/images/products/tech/screenmaster_27_inch_monitors/main.png', 17),
('tech', '/api/images/products/tech/screenmaster_portable_monitors_x/main.png', 18),
('tech', '/api/images/products/tech/smartframe_8_inch_smart_displays/main.png', 19),
('tech', '/api/images/products/tech/smartsocket_mini_smart_plugs/main.png', 20),
('tech', '/api/images/products/tech/smartspecs_audio_smart_glasses_edition/main.png', 21),
('tech', '/api/images/products/tech/viewsmart_5_inch_smart_displays/main.png', 22),
('tech', '/api/images/products/tech/visiontech_fashion_smart_glasses_2.0/main.png', 23),
('tech', '/api/images/products/tech/voxpro_wireless_system_microphones_pro/main.png', 24);

-- Get max row_num per category
CREATE TEMP TABLE category_max AS
SELECT category, MAX(row_num) as max_num FROM category_images GROUP BY category;

-- Update products with round-robin image assignment
UPDATE products p
SET image_url = ci.image_url
FROM (
    SELECT 
        p2.id,
        p2.category,
        ((ROW_NUMBER() OVER (PARTITION BY p2.category ORDER BY p2.id) - 1) % cm.max_num) + 1 as assigned_row
    FROM products p2
    JOIN category_max cm ON p2.category = cm.category
) ranked
JOIN category_images ci ON ci.category = ranked.category AND ci.row_num = ranked.assigned_row
WHERE p.id = ranked.id;

-- Show results
SELECT category, COUNT(*) as updated FROM products WHERE image_url LIKE '/api/images/%' GROUP BY category ORDER BY category;
